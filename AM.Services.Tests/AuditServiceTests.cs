// -------------------------------------------------------
// File:    AuditServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Test P3.2 — audit lưu bền JSONL theo ngày: Record→Query, lọc user, retention
// -------------------------------------------------------

using AM.Core.Models;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AM.Services.Tests;

public sealed class AuditServiceTests
{
    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"am-test-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Record_ThenQuery_ReturnsNewestFirst_AndFiltersByUser()
    {
        string dir = TempDir();
        var sut = new AuditService(NullLogger<AuditService>.Instance, dir);

        sut.Record("engineer", "Jog AX_0", allowed: true);
        sut.Record("operator", "Start", allowed: true);
        sut.Record("engineer", "Force DO_1", allowed: false, detail: "cần SuperUser");

        var all = sut.Query(DateTime.Today, DateTime.Today);
        all.Should().HaveCount(3);
        all[0].Action.Should().Be("Force DO_1", "mới nhất trước");
        all[0].Allowed.Should().BeFalse();

        var engineerOnly = sut.Query(DateTime.Today, DateTime.Today, userFilter: "engi");
        engineerOnly.Should().HaveCount(2).And.OnlyContain(e => e.User == "engineer");
    }

    [Fact]
    public void Query_ReadsAcrossDays_AndSurvivesReload()
    {
        string dir = TempDir();
        var sut = new AuditService(NullLogger<AuditService>.Instance, dir);
        sut.Record("admin", "Login", allowed: true);

        // Giả lập file của HÔM QUA (Record luôn ghi hôm nay — tạo tay để test range)
        var yesterday = DateTime.Now.AddDays(-1);
        var oldEntry = new AuditEntry(yesterday, "linelead", "Recovery.ConveyorToggle", true, null);
        string yesterdayFile = Path.Combine(dir,
            $"audit-{DateOnly.FromDateTime(yesterday):yyyyMMdd}.jsonl");
        File.WriteAllText(yesterdayFile, System.Text.Json.JsonSerializer.Serialize(oldEntry) + Environment.NewLine);

        var reloaded = new AuditService(NullLogger<AuditService>.Instance, dir);
        var twoDays = reloaded.Query(yesterday, DateTime.Today);
        twoDays.Should().HaveCount(2, "đọc gộp cả hai ngày");
        twoDays[0].User.Should().Be("admin", "hôm nay mới hơn hôm qua");

        reloaded.Query(yesterday, yesterday).Should().ContainSingle()
            .Which.User.Should().Be("linelead");
    }

    [Fact]
    public void Cleanup_DeletesFilesOlderThanRetention_KeepsRecent()
    {
        string dir = TempDir();
        string oldFile = Path.Combine(dir,
            $"audit-{DateOnly.FromDateTime(DateTime.Now.AddDays(-40)):yyyyMMdd}.jsonl");
        string recentFile = Path.Combine(dir,
            $"audit-{DateOnly.FromDateTime(DateTime.Now.AddDays(-5)):yyyyMMdd}.jsonl");
        File.WriteAllText(oldFile, "{}" + Environment.NewLine);
        File.WriteAllText(recentFile, "{}" + Environment.NewLine);

        _ = new AuditService(NullLogger<AuditService>.Instance, dir, retentionDays: 30);

        File.Exists(oldFile).Should().BeFalse("quá 30 ngày → xoá lúc khởi động");
        File.Exists(recentFile).Should().BeTrue("còn trong hạn → giữ");
    }
}
