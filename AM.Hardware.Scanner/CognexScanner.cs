// -------------------------------------------------------
// File:    CognexScanner.cs
// Project: AM.Hardware.Scanner
// Purpose: Driver scanner Cognex DataMan qua TCP — trigger qua host command.
// -------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace AM.Hardware.Scanner;

/// <summary>
/// Scanner Cognex DataMan qua TCP. Trigger bằng host-command (mặc định "TRIGGER ON"), nhận chuỗi mã.
/// </summary>
/// <remarks>DataMan trả "NO READ" khi không decode được — map thành alarm.</remarks>
public sealed class CognexScanner : TcpBarcodeScannerBase
{
    /// <summary>Tạo scanner Cognex DataMan.</summary>
    public CognexScanner(ILogger<CognexScanner> logger, string host, int port = 23,
        string name = "Cognex-DataMan", int timeoutMs = 3_000, string triggerCommand = "TRIGGER ON")
        : base(logger, host, port, name, timeoutMs)
    {
        TriggerCommand = triggerCommand;
    }

    /// <inheritdoc/>
    protected override string TriggerCommand { get; }

    /// <inheritdoc/>
    protected override string NoReadToken => "NO READ";
}
