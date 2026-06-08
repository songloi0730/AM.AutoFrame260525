// -------------------------------------------------------
// File:    AlarmPolicy.cs
// Project: AM.Core
// Purpose: Suy ra Level/Category/Action mặc định của alarm từ mã (dùng chung mọi máy).
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Constants;

/// <summary>
/// Chính sách alarm dùng chung: suy ra <see cref="AlarmLevel"/>, <see cref="AlarmCategory"/>,
/// <see cref="AlarmAction"/> mặc định từ dải mã alarm (xem <see cref="AlarmCodes"/>). Máy có thể
/// override Action cho từng mã qua <c>[AlarmInfo(..., action)]</c>.
/// </summary>
public static class AlarmPolicy
{
    /// <summary>Suy ra category từ dải mã.</summary>
    public static AlarmCategory ResolveCategory(int code) => code switch
    {
        >= 10000 and <= 10999 => AlarmCategory.Motion,
        >= 20000 and <= 20999 => AlarmCategory.Vision,
        >= 30000 and <= 30999 => AlarmCategory.Io,
        >= 40000 and <= 49999 => AlarmCategory.System,
        >= 50000 and <= 59999 => AlarmCategory.Communication,
        >= 60000 and <= 69999 => AlarmCategory.Production,
        >= 70000 and <= 79999 => AlarmCategory.Safety,
        _ => AlarmCategory.General
    };

    /// <summary>Suy ra mức nghiêm trọng từ dải mã: Safety/System = Critical, hardware = High, khác = Medium.</summary>
    public static AlarmLevel ResolveLevel(int code) => code switch
    {
        >= 70000 and <= 79999 => AlarmLevel.Critical,  // Safety / Interlock
        >= 40000 and <= 49999 => AlarmLevel.Critical,  // System / Application
        >= 10000 and <= 69999 => AlarmLevel.High,      // Motion/Vision/IO/Comm/Production
        _ => AlarmLevel.Medium
    };

    /// <summary>
    /// Suy ra hành động mặc định: Safety/Critical → ResetRequired; High → Stop; còn lại → Continue.
    /// </summary>
    public static AlarmAction ResolveAction(int code, AlarmLevel level)
    {
        if (ResolveCategory(code) == AlarmCategory.Safety || level == AlarmLevel.Critical)
            return AlarmAction.ResetRequired;
        return level switch
        {
            AlarmLevel.High => AlarmAction.Stop,
            _ => AlarmAction.Continue   // Medium (warning) / Low (info)
        };
    }
}
