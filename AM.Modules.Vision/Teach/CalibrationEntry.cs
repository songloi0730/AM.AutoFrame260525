// -------------------------------------------------------
// File:    CalibrationEntry.cs
// Project: AM.Modules.Vision
// Purpose: Một mục lịch sử hiệu chuẩn px→mm (thời điểm + hệ số + ghi chú).
// -------------------------------------------------------

namespace AM.Modules.Vision.Teach;

/// <summary>
/// Một lần hiệu chuẩn px→mm đã ghi nhận: thời điểm + hệ số mm/pixel + ghi chú tuỳ chọn.
/// Lưu danh sách để truy vết drift theo thời gian (ADR 0007 — "lịch sử offset").
/// </summary>
/// <param name="Timestamp">Thời điểm hiệu chuẩn (UTC).</param>
/// <param name="MmPerPixel">Hệ số quy đổi: 1 pixel = bao nhiêu mm.</param>
/// <param name="Note">Ghi chú tuỳ chọn (vd cách đo, mẫu chuẩn).</param>
public sealed record CalibrationEntry(DateTime Timestamp, double MmPerPixel, string? Note);
