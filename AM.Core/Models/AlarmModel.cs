// -------------------------------------------------------
// File:    AlarmModel.cs
// Project: AM.Core
// Purpose: Domain model đại diện cho một alarm đang active
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Models;

/// <summary>
/// Domain model cho alarm — dùng trong AlarmService và ViewModels.
/// </summary>
public sealed class AlarmModel
{
    public int AlarmCode { get; init; }
    public string Station { get; init; } = string.Empty;
    public AlarmLevel Level { get; init; }

    /// <summary>Phân loại hệ con (suy ra từ dải mã).</summary>
    public AlarmCategory Category { get; init; } = AlarmCategory.General;

    /// <summary>Hành động máy cần khi alarm này phát sinh.</summary>
    public AlarmAction Action { get; init; } = AlarmAction.Stop;

    public string Message { get; init; } = string.Empty;
    public DateTime RaisedAt { get; init; } = DateTime.UtcNow;
    public bool IsAcknowledged { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }
    public string? AcknowledgedBy { get; private set; }

    /// <summary>
    /// Operator acknowledge alarm này.
    /// </summary>
    /// <param name="operatorId">ID người acknowledge.</param>
    /// <param name="acknowledgedAt">
    /// Thời điểm acknowledge (UTC). Khi null sẽ dùng DateTime.UtcNow.
    /// Dùng giá trị cụ thể khi load từ database để giữ đúng timestamp gốc.
    /// </param>
    public void Acknowledge(string operatorId, DateTime? acknowledgedAt = null)
    {
        IsAcknowledged = true;
        AcknowledgedAt = acknowledgedAt ?? DateTime.UtcNow;
        AcknowledgedBy = operatorId;
    }
}
