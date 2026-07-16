// -------------------------------------------------------
// File:    ConfigIntegrity.cs
// Project: AM.Core
// Purpose: Trạng thái toàn vẹn file cấu hình theo manifest SHA-256 (S93, design-notes/0014)
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>Kết quả đối chiếu một file cấu hình với manifest.</summary>
public enum ConfigFileState
{
    /// <summary>Khớp hash đã ký.</summary>
    Ok = 0,

    /// <summary>File ĐÃ BỊ SỬA ngoài app sau lần ký gần nhất.</summary>
    Modified = 1,

    /// <summary>File có trong manifest nhưng không còn trên đĩa.</summary>
    Missing = 2,

    /// <summary>File tồn tại nhưng chưa được ký (mới thêm / manifest chưa có).</summary>
    NotSigned = 3,
}

/// <summary>Trạng thái toàn vẹn của một file cấu hình.</summary>
/// <param name="FileName">Tên file (tương đối thư mục app).</param>
/// <param name="State">Kết quả đối chiếu.</param>
public sealed record ConfigFileStatus(string FileName, ConfigFileState State);
