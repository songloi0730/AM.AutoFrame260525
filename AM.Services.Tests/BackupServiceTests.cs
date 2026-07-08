// -------------------------------------------------------
// File:    BackupServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Test P3.3 — backup zip đúng nội dung, restore khôi phục file đã mất + có đường lùi
// -------------------------------------------------------

using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AM.Services.Tests;

public sealed class BackupServiceTests
{
    // Dựng thư mục máy giả: vài file dữ liệu + recipes/ có file con
    private static string CreateFakeMachineDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"am-test-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "recipes"));
        File.WriteAllText(Path.Combine(dir, "users.json"), """{"SchemaVersion":2,"Users":[]}""");
        File.WriteAllText(Path.Combine(dir, "points.json"), "[]");
        File.WriteAllText(Path.Combine(dir, "recipes", "Default.sequence.json"), """{"steps":[]}""");
        return dir;
    }

    private static BackupService Create(string dir)
        => new(NullLogger<BackupService>.Instance, audit: null, baseDir: dir);

    [Fact]
    public async Task CreateBackup_ZipContainsExistingTargets_SkipsMissing()
    {
        string dir = CreateFakeMachineDir();
        var sut = Create(dir);

        string zipPath = await sut.CreateBackupAsync();

        File.Exists(zipPath).Should().BeTrue();
        using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        names.Should().Contain(["users.json", "points.json", "recipes/Default.sequence.json"]);
        names.Should().NotContain("automachine.db", "file không tồn tại thì không vào zip");
    }

    [Fact]
    public async Task Restore_BringsDeletedFileBack_AndCreatesPreRestoreSafety()
    {
        string dir = CreateFakeMachineDir();
        var sut = Create(dir);
        string zipPath = await sut.CreateBackupAsync();

        // Mất dữ liệu: xoá users.json + sửa points.json
        File.Delete(Path.Combine(dir, "users.json"));
        File.WriteAllText(Path.Combine(dir, "points.json"), "HỎNG");

        await sut.RestoreAsync(zipPath);

        File.Exists(Path.Combine(dir, "users.json")).Should().BeTrue("restore phải khôi phục file đã xoá");
        File.ReadAllText(Path.Combine(dir, "points.json")).Should().Be("[]", "restore đè nội dung hỏng");
        Directory.EnumerateFiles(Path.Combine(dir, "backups"), "am-prerestore-*.zip")
            .Should().NotBeEmpty("trước khi đè phải tự sao lưu trạng thái hiện tại (đường lùi)");
    }

    [Fact]
    public async Task Restore_MissingZip_Throws_AndListBackupsNewestFirst()
    {
        string dir = CreateFakeMachineDir();
        var sut = Create(dir);
        var act = () => sut.RestoreAsync(Path.Combine(dir, "khong-ton-tai.zip"));
        await act.Should().ThrowAsync<FileNotFoundException>();

        await sut.CreateBackupAsync();
        await Task.Delay(50);
        await sut.CreateBackupAsync();
        var list = sut.ListBackups();
        list.Should().HaveCountGreaterThanOrEqualTo(2);
        list[0].CreatedAt.Should().BeOnOrAfter(list[^1].CreatedAt, "mới nhất trước");
    }
}
