// -------------------------------------------------------
// File:    StepEventArgs.cs
// Project: AM.Core.Sequencing
// Purpose: EventArgs cho StepStarted/StepCompleted — một nguồn nuôi dashboard + log + persist
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Dữ liệu sự kiện một bước. <see cref="Result"/>/<see cref="Duration"/> = null với
/// <c>StepStarted</c>, có giá trị với <c>StepCompleted</c>. Bất biến — consumer
/// (dashboard, log sink) tự marshal thread.
/// </summary>
public sealed class StepEventArgs : EventArgs
{
    /// <summary>Id bước trong file sequence.</summary>
    public string StepId { get; }

    /// <summary>Tên station thực thi.</summary>
    public string StationName { get; }

    /// <summary>Nhóm order của bước.</summary>
    public int Order { get; }

    /// <summary>Lần thử hiện tại (0 = lần đầu, tăng theo retry).</summary>
    public int Attempt { get; }

    /// <summary>SN sản phẩm tại thời điểm sự kiện (null nếu chưa scan).</summary>
    public string? SerialNumber { get; }

    /// <summary>Kết quả bước — null khi StepStarted.</summary>
    public StationResult? Result { get; }

    /// <summary>Thời gian chạy bước (engine đo) — null khi StepStarted.</summary>
    public TimeSpan? Duration { get; }

    /// <summary>Tạo event args cho một bước.</summary>
    /// <param name="stepId">Id bước.</param>
    /// <param name="stationName">Tên station.</param>
    /// <param name="order">Nhóm order.</param>
    /// <param name="attempt">Lần thử.</param>
    /// <param name="serialNumber">SN sản phẩm (nếu có).</param>
    /// <param name="result">Kết quả (null với StepStarted).</param>
    /// <param name="duration">Thời gian bước (null với StepStarted).</param>
    public StepEventArgs(string stepId, string stationName, int order, int attempt,
        string? serialNumber, StationResult? result, TimeSpan? duration)
    {
        ArgumentNullException.ThrowIfNull(stepId);
        ArgumentNullException.ThrowIfNull(stationName);
        StepId = stepId;
        StationName = stationName;
        Order = order;
        Attempt = attempt;
        SerialNumber = serialNumber;
        Result = result;
        Duration = duration;
    }
}
