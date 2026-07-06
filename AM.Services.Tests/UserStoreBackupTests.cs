// -------------------------------------------------------
// File:    UserStoreBackupTests.cs
// Project: AM.Services.Tests
// Purpose: Test P0.3 — store users hỏng/schema cũ phải được BACKUP trước khi seed lại ghi đè.
// -------------------------------------------------------

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AM.Services.Tests;

public sealed class UserStoreBackupTests
{
    [Fact]
    public void Load_CorruptStore_BacksUpOldFileBeforeReseed()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"amuser-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string store = Path.Combine(dir, "users.json");
        const string corruptContent = "[ \"mảng trần schema cũ — không envelope\" ";
        File.WriteAllText(store, corruptContent);

        try
        {
            _ = new UserService(NullLogger<UserService>.Instance, store);

            // 1) File cũ được backup NGUYÊN VẸN trước khi seed ghi đè
            string[] backups = Directory.GetFiles(dir, "users.json.bak-*");
            backups.Should().ContainSingle("re-seed phải backup store cũ — user đã tạo không mất im lặng");
            File.ReadAllText(backups[0]).Should().Be(corruptContent);

            // 2) Store mới đã seed mặc định hợp lệ
            string reseeded = File.ReadAllText(store);
            reseeded.Should().Contain("operator").And.Contain("SchemaVersion");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_FirstRunNoFile_SeedsWithoutBackup()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"amuser-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string store = Path.Combine(dir, "users.json");

        try
        {
            _ = new UserService(NullLogger<UserService>.Instance, store);

            File.Exists(store).Should().BeTrue("lần đầu chạy seed bình thường");
            Directory.GetFiles(dir, "users.json.bak-*")
                .Should().BeEmpty("không có gì để backup khi chưa từng có file");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
