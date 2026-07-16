// -------------------------------------------------------
// File:    MachineConfigViewModel.cs
// Project: AM.Modules.Settings
// Purpose: VM thẻ "Thông số máy" (S93): nhận diện máy (machine.json) + endpoint thiết bị
//          (appsettings.json) — Administrator, audit, tự ký lại manifest; bảng toàn vẹn SHA-256.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Settings;

/// <summary>
/// Thẻ "Thông số máy" trong Cài đặt: sửa tên máy/line/vị trí (machine.json) + IP/cổng thiết bị
/// (appsettings.json) — cần Administrator, mỗi lần lưu có audit + tự ký lại manifest SHA-256;
/// đổi endpoint cần KHỞI ĐỘNG LẠI mới áp dụng (DI đọc config lúc boot). Kèm bảng toàn vẹn
/// file cấu hình + nút "Ký lại" chấp nhận thay đổi chỉnh tay có chủ đích (design-notes/0014).
/// </summary>
public sealed partial class MachineConfigViewModel : ObservableObject
{
    private readonly IUserService _user;
    private readonly IAuditService _audit;
    private readonly IConfigIntegrityService _integrity;
    private readonly ILogger<MachineConfigViewModel> _logger;
    private readonly string _machinePath;
    private readonly string _appSettingsPath;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Trạng thái toàn vẹn từng file cấu hình.</summary>
    public ObservableCollection<ConfigRowVm> IntegrityRows { get; } = [];

    // ── Nhận diện máy (machine.json) ──
    [ObservableProperty] private string _machineName = string.Empty;
    [ObservableProperty] private string _line = string.Empty;
    [ObservableProperty] private string _location = string.Empty;

    /// <summary>Mã máy (AutoMachine:Security:MachineId — dùng cho day-code, chỉ hiển thị).</summary>
    public string MachineId { get; }

    // ── Kết nối thiết bị (appsettings.json — đổi xong cần khởi động lại) ──
    [ObservableProperty] private bool _useSimulation;
    [ObservableProperty] private string _modbusHost = string.Empty;
    [ObservableProperty] private string _modbusPort = string.Empty;
    [ObservableProperty] private string _opcUaEndpoint = string.Empty;
    [ObservableProperty] private string _ethernetIpHost = string.Empty;
    [ObservableProperty] private string _plcHost = string.Empty;
    [ObservableProperty] private string _plcPort = string.Empty;
    [ObservableProperty] private string _robotHost = string.Empty;
    [ObservableProperty] private string _robotPort = string.Empty;
    [ObservableProperty] private string _scannerHost = string.Empty;
    [ObservableProperty] private string _scannerPort = string.Empty;
    [ObservableProperty] private string _adamHost = string.Empty;
    [ObservableProperty] private string _adamPort = string.Empty;

    [ObservableProperty] private bool _canEdit;
    [ObservableProperty] private bool _restartRequired;
    [ObservableProperty] private string _statusText = string.Empty;

    /// <summary>Tạo VM. Shell bơm machineId + đường dẫn file (module không đụng IConfiguration).</summary>
    public MachineConfigViewModel(IUserService user, IAuditService audit,
        IConfigIntegrityService integrity, ILogger<MachineConfigViewModel> logger,
        string machineId, string baseDir = ".")
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(integrity);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);
        _user = user;
        _audit = audit;
        _integrity = integrity;
        _logger = logger;
        MachineId = machineId ?? string.Empty;
        _machinePath = Path.Combine(baseDir, "machine.json");
        _appSettingsPath = Path.Combine(baseDir, "appsettings.json");

        Load();
        RefreshIntegrity();
        RefreshGate();
        _user.UserChanged += (_, _) => RefreshGate(); // chỉ set bool — an toàn cross-thread
    }

    private void RefreshGate() => CanEdit = _user.CurrentLevel >= UserLevel.Administrator;

    // ── Nạp giá trị hiện tại từ 2 file ──
    private void Load()
    {
        try
        {
            if (File.Exists(_machinePath))
            {
                var m = JsonNode.Parse(File.ReadAllText(_machinePath));
                MachineName = m?["machineName"]?.GetValue<string>() ?? string.Empty;
                Line = m?["line"]?.GetValue<string>() ?? string.Empty;
                Location = m?["location"]?.GetValue<string>() ?? string.Empty;
            }
            if (File.Exists(_appSettingsPath))
            {
                var root = JsonNode.Parse(File.ReadAllText(_appSettingsPath));
                var am = root?["AutoMachine"];
                UseSimulation = am?[nameof(UseSimulation)]?.GetValue<bool>() ?? true;
                ModbusHost = Str(am, "Comm", "ModbusHost");
                ModbusPort = Str(am, "Comm", "ModbusPort");
                OpcUaEndpoint = Str(am, "Comm", "OpcUaEndpoint");
                EthernetIpHost = Str(am, "Comm", "EthernetIpHost");
                PlcHost = Str(am, "Plc", "Host");
                PlcPort = Str(am, "Plc", "Port");
                RobotHost = Str(am, "Robot", "Host");
                RobotPort = Str(am, "Robot", "Port");
                ScannerHost = Str(am, "Scanner", "Host");
                ScannerPort = Str(am, "Scanner", "Port");
                AdamHost = Str(am, "Io", "AdamHost");
                AdamPort = Str(am, "Io", "AdamPort");
            }
        }
#pragma warning disable CA1031 // file hỏng → màn vẫn mở với giá trị trống, báo status
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[MachineConfig] Lỗi nạp cấu hình");
            StatusText = ex.Message;
        }
    }

    private static string Str(JsonNode? am, string section, string key)
        => am?[section]?[key]?.ToString() ?? string.Empty;

    /// <summary>Lưu cả 2 file (Administrator): validate cổng → ghi → audit → ký lại manifest.</summary>
    [RelayCommand]
    private void Save()
    {
        if (!CanEdit)
        {
            StatusText = Loc.Strings["Machine.NeedAdmin"];
            return;
        }
        if (!TryParsePorts(out string? badField))
        {
            StatusText = string.Format(CultureInfo.CurrentCulture, Loc.Strings["Machine.BadPort"], badField);
            return;
        }

        try
        {
            SaveMachineJson();
            SaveAppSettings();
            string who = _user.CurrentUser ?? "?";
            _audit.Record(who, "Machine.SaveConfig", allowed: true,
                detail: $"name={MachineName} line={Line} sim={UseSimulation} " +
                        $"modbus={ModbusHost}:{ModbusPort} plc={PlcHost}:{PlcPort} opcua={OpcUaEndpoint}");
            _integrity.Resign(who); // sửa hợp lệ qua app → manifest khớp lại ngay
            RefreshIntegrity();
            RestartRequired = true;
            StatusText = Loc.Strings["Machine.Saved"];
        }
#pragma warning disable CA1031 // lỗi IO → báo status, không sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[MachineConfig] Lưu cấu hình thất bại");
            StatusText = ex.Message;
        }
    }

    private bool TryParsePorts(out string? badField)
    {
        badField = null;
        foreach (var (label, value) in new[]
        {
            ("Modbus", ModbusPort), ("PLC", PlcPort), ("Robot", RobotPort),
            ("Scanner", ScannerPort), ("ADAM", AdamPort),
        })
        {
            if (string.IsNullOrWhiteSpace(value)) continue; // trống = giữ nguyên không ghi
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p)
                || p is < 1 or > 65535)
            {
                badField = label;
                return false;
            }
        }
        return true;
    }

    // Ghi machine.json GIỮ NGUYÊN phần stations — chỉ thay 3 trường nhận diện.
    private void SaveMachineJson()
    {
        var root = File.Exists(_machinePath)
            ? JsonNode.Parse(File.ReadAllText(_machinePath)) ?? new JsonObject()
            : new JsonObject();
        root["machineName"] = MachineName;
        root["line"] = Line;
        root["location"] = Location;
        File.WriteAllText(_machinePath, root.ToJsonString(WriteOptions));
    }

    // Ghi appsettings.json: chỉ set các key màn này quản — phần còn lại giữ nguyên.
    private void SaveAppSettings()
    {
        if (!File.Exists(_appSettingsPath)) return;
        var root = JsonNode.Parse(File.ReadAllText(_appSettingsPath)) ?? new JsonObject();
        var am = root["AutoMachine"] ?? (root["AutoMachine"] = new JsonObject());
        am[nameof(UseSimulation)] = UseSimulation;
        SetStr(am, "Comm", "ModbusHost", ModbusHost);
        SetInt(am, "Comm", "ModbusPort", ModbusPort);
        SetStr(am, "Comm", "OpcUaEndpoint", OpcUaEndpoint);
        SetStr(am, "Comm", "EthernetIpHost", EthernetIpHost);
        SetStr(am, "Plc", "Host", PlcHost);
        SetInt(am, "Plc", "Port", PlcPort);
        SetStr(am, "Robot", "Host", RobotHost);
        SetInt(am, "Robot", "Port", RobotPort);
        SetStr(am, "Scanner", "Host", ScannerHost);
        SetInt(am, "Scanner", "Port", ScannerPort);
        SetStr(am, "Io", "AdamHost", AdamHost);
        SetInt(am, "Io", "AdamPort", AdamPort);
        File.WriteAllText(_appSettingsPath, root.ToJsonString(WriteOptions));
    }

    private static void SetStr(JsonNode am, string section, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var s = am[section] ?? (am[section] = new JsonObject());
        s[key] = value.Trim();
    }

    private static void SetInt(JsonNode am, string section, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var s = am[section] ?? (am[section] = new JsonObject());
        s[key] = int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    // ── Toàn vẹn cấu hình ──

    /// <summary>Ký lại manifest — chấp nhận thay đổi chỉnh tay có chủ đích (Administrator).</summary>
    [RelayCommand]
    private void Resign()
    {
        if (!CanEdit)
        {
            StatusText = Loc.Strings["Machine.NeedAdmin"];
            return;
        }
        _integrity.Resign(_user.CurrentUser ?? "?");
        RefreshIntegrity();
        StatusText = Loc.Strings["Machine.Resigned"];
    }

    /// <summary>Nạp lại bảng toàn vẹn.</summary>
    [RelayCommand]
    private void RefreshIntegrity()
    {
        IntegrityRows.Clear();
        foreach (var s in _integrity.VerifyAll())
            IntegrityRows.Add(new ConfigRowVm(s));
    }
}

/// <summary>Một dòng bảng toàn vẹn file cấu hình.</summary>
public sealed class ConfigRowVm(ConfigFileStatus status)
{
    /// <summary>Tên file.</summary>
    public string FileName { get; } = status.FileName;

    /// <summary>Trạng thái (i18n).</summary>
    public string StateText { get; } = Loc.Strings[status.State switch
    {
        ConfigFileState.Ok => "Machine.StateOk",
        ConfigFileState.Modified => "Machine.StateModified",
        ConfigFileState.Missing => "Machine.StateMissing",
        _ => "Machine.StateUnsigned",
    }];

    /// <summary>Đáng chú ý (sửa/mất) → tô đỏ.</summary>
    public bool IsBad { get; } = status.State is ConfigFileState.Modified or ConfigFileState.Missing;

    /// <summary>Chưa ký → tô vàng.</summary>
    public bool IsUnsigned { get; } = status.State == ConfigFileState.NotSigned;
}
