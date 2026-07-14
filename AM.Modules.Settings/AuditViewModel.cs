// -------------------------------------------------------
// File:    AuditViewModel.cs
// Project: AM.Modules.Settings
// Purpose: VM màn Audit (P3.2) — xem bảng audit + lọc ngày/user + export CSV (Administrator)
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AM.Modules.Settings;

/// <summary>
/// Màn Audit trong Cài đặt: đọc <see cref="IAuditService.Query"/> (JSONL theo ngày),
/// lọc từ/đến ngày + user, xuất CSV. Cần Administrator — dưới quyền chỉ hiện thông điệp gate.
/// </summary>
public sealed partial class AuditViewModel : ObservableObject
{
    private const int MaxRows = 500;

    private readonly IAuditService _audit;
    private readonly IUserService _user;

    /// <summary>Các dòng audit đang hiển thị (mới nhất trước).</summary>
    public ObservableCollection<AuditRowVm> Rows { get; } = [];

    [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime _toDate = DateTime.Today;
    [ObservableProperty] private string _userFilter = string.Empty;
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private string _statusText = string.Empty;

    private readonly SynchronizationContext? _uiContext;

    /// <summary>Tạo VM màn Audit.</summary>
    public AuditViewModel(IAuditService audit, IUserService user)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(user);
        _audit = audit;
        _user = user;
        _uiContext = SynchronizationContext.Current;
        RefreshGate();
        // UserChanged bắn trên thread nền (LoginAsync) — RefreshGate đụng ObservableCollection Rows
        // nên PHẢI marshal về UI thread; nếu không handler ném cross-thread và CHẶN các subscriber
        // sau nó (nav/gate các màn khác không cập nhật sau khi đổi user).
        _user.UserChanged += (_, _) => RunOnUIThread(RefreshGate);
    }

    private void RunOnUIThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext) action();
        else _uiContext.Post(_ => action(), null);
    }

    private void RefreshGate()
    {
        IsAdmin = _user.CurrentLevel >= UserLevel.Administrator;
        if (IsAdmin) Refresh();
        else Rows.Clear();
    }

    /// <summary>Nạp lại bảng theo bộ lọc hiện tại.</summary>
    [RelayCommand]
    private void Refresh()
    {
        if (!IsAdmin) return;
        Rows.Clear();
        foreach (var e in _audit.Query(FromDate, ToDate, UserFilter, MaxRows))
            Rows.Add(new AuditRowVm(e));
        StatusText = string.Format(CultureInfo.CurrentCulture, Loc.Strings["Audit.Count"], Rows.Count);
    }

    /// <summary>Xuất bảng đang hiển thị ra CSV (chọn nơi lưu).</summary>
    [RelayCommand]
    private void ExportCsv()
    {
        if (!IsAdmin || Rows.Count == 0) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = string.Create(CultureInfo.InvariantCulture, $"audit-{DateTime.Now:yyyyMMdd-HHmm}.csv"),
        };
        if (dialog.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("Time,User,Action,Result,Detail");
        foreach (var r in Rows)
        {
            sb.Append(Csv(r.TimeText)).Append(',').Append(Csv(r.User)).Append(',')
              .Append(Csv(r.Action)).Append(',').Append(Csv(r.ResultText)).Append(',')
              .AppendLine(Csv(r.Detail));
        }
        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
        StatusText = string.Format(CultureInfo.CurrentCulture, Loc.Strings["Audit.Exported"], dialog.FileName);
    }

    // Escape CSV: bọc ngoặc kép khi có ký tự đặc biệt, nhân đôi ngoặc kép bên trong.
    private static string Csv(string value)
        => value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
}

/// <summary>Một dòng bảng audit (đã format hiển thị).</summary>
public sealed class AuditRowVm(AuditEntry entry)
{
    /// <summary>Thời điểm (HH:mm:ss dd/MM/yyyy).</summary>
    public string TimeText { get; } = entry.Timestamp.ToString("HH:mm:ss dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Người thực hiện.</summary>
    public string User { get; } = entry.User;

    /// <summary>Thao tác.</summary>
    public string Action { get; } = entry.Action;

    /// <summary>Kết quả OK/DENIED.</summary>
    public string ResultText { get; } = entry.Allowed ? "OK" : "DENIED";

    /// <summary>True nếu bị từ chối — tô màu cảnh báo.</summary>
    public bool IsDenied { get; } = !entry.Allowed;

    /// <summary>Chi tiết.</summary>
    public string Detail { get; } = entry.Detail ?? string.Empty;
}
