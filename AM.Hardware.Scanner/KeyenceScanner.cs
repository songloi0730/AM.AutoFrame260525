// -------------------------------------------------------
// File:    KeyenceScanner.cs
// Project: AM.Hardware.Scanner
// Purpose: Driver scanner Keyence SR series qua TCP — lệnh "LON" để đọc một lần.
// -------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace AM.Hardware.Scanner;

/// <summary>
/// Scanner Keyence SR-1000/SR-2000 qua TCP. Trigger bằng lệnh "LON" (read on), nhận chuỗi mã.
/// </summary>
/// <remarks>Lệnh trigger có thể đổi qua cấu hình nếu host-command khác (vd "LON,1").</remarks>
public sealed class KeyenceScanner : TcpBarcodeScannerBase
{
    /// <summary>Tạo scanner Keyence.</summary>
    public KeyenceScanner(ILogger<KeyenceScanner> logger, string host, int port = 9004,
        string name = "Keyence-SR", int timeoutMs = 3_000, string triggerCommand = "LON")
        : base(logger, host, port, name, timeoutMs)
    {
        TriggerCommand = triggerCommand;
    }

    /// <inheritdoc/>
    protected override string TriggerCommand { get; }

    /// <inheritdoc/>
    protected override string NoReadToken => "ERROR";
}
