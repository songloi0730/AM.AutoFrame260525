// -------------------------------------------------------
// File:    ProductionOptionsTests.cs
// Project: AM.Services.Tests
// Purpose: Test P4.4 — tính mốc ca hiện tại theo lịch ca config + mức màu yield
// -------------------------------------------------------

using AM.Core.Models;
using FluentAssertions;
using Xunit;

namespace AM.Services.Tests;

public sealed class ProductionOptionsTests
{
    [Theory]
    // Ca 8h bắt đầu 8h sáng: 8-16 / 16-0 / 0-8
    [InlineData(2026, 7, 11, 10, 0, 8, 8, "2026-07-11 08:00")] // giữa ca sáng
    [InlineData(2026, 7, 11, 17, 30, 8, 8, "2026-07-11 16:00")] // ca chiều
    [InlineData(2026, 7, 11, 2, 0, 8, 8, "2026-07-11 00:00")]  // ca đêm (mốc 0h của chuỗi ca từ 8h hôm trước)
    [InlineData(2026, 7, 11, 7, 59, 8, 8, "2026-07-11 00:00")] // ngay trước mốc 8h — vẫn ca đêm
    // Ca 12h bắt đầu 6h: 6-18 / 18-6
    [InlineData(2026, 7, 11, 12, 0, 6, 12, "2026-07-11 06:00")]
    [InlineData(2026, 7, 11, 5, 0, 6, 12, "2026-07-10 18:00")] // rạng sáng thuộc ca đêm hôm trước
    public void GetShiftStartLocal_ComputesCurrentShiftAnchor(
        int y, int mo, int d, int h, int mi, int startHour, int lengthHours, string expected)
    {
        var options = new ProductionOptions { ShiftStartHour = startHour, ShiftLengthHours = lengthHours };
        var now = new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Local);

        var shiftStart = options.GetShiftStartLocal(now);

        shiftStart.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(expected);
        shiftStart.Should().BeOnOrBefore(now);
        shiftStart.AddHours(lengthHours).Should().BeAfter(now, "now phải nằm TRONG ca trả về");
    }

    [Theory]
    [InlineData(98.0, 100, 0)] // trên Warn → thường
    [InlineData(93.0, 100, 1)] // dưới Warn 95 → vàng
    [InlineData(85.0, 100, 2)] // dưới Alarm 90 → đỏ
    [InlineData(0.0, 0, 0)]    // chưa có sản phẩm → chưa có nghĩa, không tô màu
    public void GetYieldLevel_MapsThresholds(double yield, int total, int expected)
    {
        var options = new ProductionOptions(); // Warn 95 / Alarm 90 mặc định
        options.GetYieldLevel(yield, total).Should().Be(expected);
    }
}
