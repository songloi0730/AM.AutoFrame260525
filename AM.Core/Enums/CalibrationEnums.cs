// -------------------------------------------------------
// File:    CalibrationEnums.cs
// Project: AM.Core
// Purpose: Enum cho mô hình hiệu chỉnh (HMI_Calibration_Model_v1.0)
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>
/// Tần suất hiệu chỉnh — quyết định CHỖ ĐỨNG trên UI (không quyết định quyền).
/// Routine → sub-tab "Hiệu chỉnh" màn Vận hành tay; Rare → Cài đặt → Bảo trì &amp; Hiệu chuẩn.
/// </summary>
public enum CalibrationFrequency
{
    /// <summary>Định kỳ ca/lô — operator/linelead thực hiện thường xuyên.</summary>
    Routine,

    /// <summary>Hiếm — sau thay cơ khí/định kỳ dài, engineer/admin thực hiện.</summary>
    Rare,
}

/// <summary>
/// Trạng thái wizard hiệu chỉnh 2 nhánh (HMI_Calibration_Model §3):
/// đo → trong ngưỡng thì áp một chạm; vượt ngưỡng thì hướng dẫn chỉnh tay → đo lại.
/// </summary>
public enum CalibrationWizardState
{
    /// <summary>Chưa đo — chỉ cho phép Đo.</summary>
    Idle,

    /// <summary>Đang đo (MeasureAsync chạy).</summary>
    Measuring,

    /// <summary>Kết quả trong ngưỡng — cho phép Áp bù (một chạm).</summary>
    WithinThreshold,

    /// <summary>Vượt ngưỡng — hiện hướng dẫn chỉnh tay, chỉ cho phép Đo lại.</summary>
    OutOfThreshold,

    /// <summary>Đang áp bù (ApplyAsync chạy).</summary>
    Applying,

    /// <summary>Đã áp xong — recipe + audit + lịch sử đã ghi.</summary>
    Completed,

    /// <summary>Đo/áp lỗi — Reset để làm lại.</summary>
    Failed,
}
