// -------------------------------------------------------
// File:    BackupViewModel.cs
// Project: AM.Modules.Settings
// Purpose: VM màn "Sao lưu & phục hồi" (P3.3) — backup tay, danh sách bản lưu,
//          phục hồi confirm 2 bước (Administrator)
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AM.Modules.Settings;

/// <summary>
/// Màn Sao lưu &amp; phục hồi: tạo backup zip (chọn thư mục), xem danh sách bản lưu,
/// phục hồi qua XÁC NHẬN 2 BƯỚC (chọn bản → cảnh báo đỏ → xác nhận lần 2). Service tự
/// sao lưu trạng thái hiện tại trước khi đè. Cần Administrator.
/// </summary>
public sealed partial class BackupViewModel : ObservableObject
{
    private readonly IBackupService _backup;
    private readonly IUserService _user;

    /// <summary>Các bản sao lưu (mới nhất trước).</summary>
    public ObservableCollection<BackupRowVm> Backups { get; } = [];

    /// <summary>Các mục dữ liệu sẽ vào bản sao lưu (hiển thị cho người dùng biết).</summary>
    public string TargetsText { get; }

    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isBusy;

    // Confirm 2 bước: chọn bản → hiện cảnh báo + nút xác nhận lần 2
    [ObservableProperty] private BackupRowVm? _pendingRestore;
    [ObservableProperty] private bool _isConfirmingRestore;

    /// <summary>Tạo VM.</summary>
    public BackupViewModel(IBackupService backup, IUserService user)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(user);
        _backup = backup;
        _user = user;
        TargetsText = string.Join(" · ", backup.Targets);
        RefreshGate();
        _user.UserChanged += (_, _) => RefreshGate();
    }

    private void RefreshGate()
    {
        IsAdmin = _user.CurrentLevel >= UserLevel.Administrator;
        if (IsAdmin) RefreshList();
        else { Backups.Clear(); CancelRestore(); }
    }

    /// <summary>Nạp lại danh sách bản sao lưu.</summary>
    [RelayCommand]
    private void RefreshList()
    {
        Backups.Clear();
        foreach (var b in _backup.ListBackups())
            Backups.Add(new BackupRowVm(b));
    }

    /// <summary>Sao lưu ngay vào thư mục do người dùng chọn (mặc định backups cạnh app).</summary>
    [RelayCommand]
    private async Task BackupNow()
    {
        if (!IsAdmin || IsBusy) return;
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Loc.Strings["Backup.PickFolder"],
        };
        string? target = dialog.ShowDialog() == true ? dialog.FolderName : null;
        if (target is null) return;

        IsBusy = true;
        try
        {
            string zip = await _backup.CreateBackupAsync(target).ConfigureAwait(true); // về UI thread cập nhật list
            StatusText = string.Format(CultureInfo.CurrentCulture, Loc.Strings["Backup.Created"], zip);
            RefreshList();
        }
#pragma warning disable CA1031 // lỗi IO (đĩa đầy/không quyền) → báo status, không sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            StatusText = string.Format(CultureInfo.CurrentCulture, Loc.Strings["Backup.Error"], ex.Message);
        }
        finally { IsBusy = false; }
    }

    /// <summary>Bước 1: chọn bản để phục hồi → hiện cảnh báo xác nhận.</summary>
    [RelayCommand]
    private void StartRestore(BackupRowVm? row)
    {
        if (!IsAdmin || row is null) return;
        PendingRestore = row;
        IsConfirmingRestore = true;
    }

    /// <summary>Huỷ xác nhận phục hồi.</summary>
    [RelayCommand]
    private void CancelRestore()
    {
        PendingRestore = null;
        IsConfirmingRestore = false;
    }

    /// <summary>Bước 2: xác nhận phục hồi thật — service tự sao lưu trước khi đè.</summary>
    [RelayCommand]
    private async Task ConfirmRestore()
    {
        var target = PendingRestore;
        if (!IsAdmin || IsBusy || target is null) return;
        IsBusy = true;
        try
        {
            await _backup.RestoreAsync(target.Path).ConfigureAwait(true);
            StatusText = Loc.Strings["Backup.RestoredRestart"];
            RefreshList(); // có thêm bản am-prerestore-*
        }
#pragma warning disable CA1031 // zip hỏng/file bị khoá → báo status, dữ liệu hiện tại đã có bản prerestore
        catch (Exception ex)
#pragma warning restore CA1031
        {
            StatusText = string.Format(CultureInfo.CurrentCulture, Loc.Strings["Backup.Error"], ex.Message);
        }
        finally
        {
            IsBusy = false;
            CancelRestore();
        }
    }
}

/// <summary>Một dòng danh sách bản sao lưu.</summary>
public sealed class BackupRowVm(BackupInfo info)
{
    /// <summary>Đường dẫn zip.</summary>
    public string Path { get; } = info.Path;

    /// <summary>Tên file.</summary>
    public string Name { get; } = System.IO.Path.GetFileName(info.Path);

    /// <summary>Thời điểm + kích thước hiển thị.</summary>
    public string Meta { get; } = string.Create(CultureInfo.InvariantCulture,
        $"{info.CreatedAt:HH:mm dd/MM/yyyy} · {info.SizeBytes / 1024.0:F0} KB");
}
