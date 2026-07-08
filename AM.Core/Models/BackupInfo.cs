// -------------------------------------------------------
// File:    BackupInfo.cs
// Project: AM.Core
// Purpose: Thông tin một bản sao lưu (P3.3 — danh sách trên màn Sao lưu & phục hồi)
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>Một bản sao lưu zip đã tạo.</summary>
/// <param name="Path">Đường dẫn file zip.</param>
/// <param name="CreatedAt">Thời điểm tạo (local).</param>
/// <param name="SizeBytes">Kích thước file.</param>
public sealed record BackupInfo(string Path, DateTime CreatedAt, long SizeBytes);
