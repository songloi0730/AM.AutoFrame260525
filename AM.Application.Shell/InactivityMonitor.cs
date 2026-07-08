// -------------------------------------------------------
// File:    InactivityMonitor.cs
// Project: AM.Application.Shell
// Purpose: Tự đăng xuất khi idle (P3.2) — đếm input toàn cửa sổ qua InputManager,
//          quá AutoLogoutMinutes thì hạ quyền về "Chưa đăng nhập" (máy VẪN chạy)
// -------------------------------------------------------

using System.Windows.Input;
using System.Windows.Threading;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Application.Shell;

/// <summary>
/// Theo dõi input toàn app (chuột/bàn phím/cảm ứng qua <see cref="InputManager.PreProcessInput"/>).
/// Idle quá <see cref="SecurityOptions.AutoLogoutMinutes"/> phút và đang có phiên đăng nhập →
/// <see cref="IUserService.Logout"/> + audit. KHÔNG đụng state machine — máy đang chạy vẫn chạy,
/// chỉ quyền thao tác bị thu về (0012: an toàn phiên không được gây downtime).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
    Justification = "Khởi tạo qua DI (AddSingleton) + Start() ở App.OnStartup")]
internal sealed class InactivityMonitor : IDisposable
{
    private readonly IUserService _user;
    private readonly IAuditService _audit;
    private readonly SecurityOptions _security;
    private readonly ILogger<InactivityMonitor> _logger;
    private readonly DispatcherTimer _timer;
    private DateTime _lastInputUtc = DateTime.UtcNow;
    private bool _started;
    private bool _disposed;

    /// <summary>Tạo monitor (chưa chạy — gọi <see cref="Start"/> trên UI thread).</summary>
    public InactivityMonitor(IUserService user, IAuditService audit, SecurityOptions security,
        ILogger<InactivityMonitor> logger)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(logger);
        _user = user;
        _audit = audit;
        _security = security;
        _logger = logger;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => CheckIdle();
    }

    /// <summary>Bắt đầu theo dõi. Gọi một lần trên UI thread (InputManager gắn với Dispatcher).</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        if (_security.AutoLogoutMinutes <= 0)
        {
            _logger.LogInformation("[AutoLogout] TẮT (AutoLogoutMinutes = 0)");
            return;
        }

        InputManager.Current.PreProcessInput += OnAnyInput;
        _user.UserChanged += OnUserChanged; // đăng nhập xong tính idle lại từ đầu
        _timer.Start();
        _logger.LogInformation("[AutoLogout] Bật — idle {Minutes} phút sẽ tự đăng xuất (máy vẫn chạy)",
            _security.AutoLogoutMinutes);
    }

    private void OnAnyInput(object sender, PreProcessInputEventArgs e) => _lastInputUtc = DateTime.UtcNow;

    private void OnUserChanged(object? sender, AM.Core.Models.EventArgs.UserChangedEventArgs e)
        => _lastInputUtc = DateTime.UtcNow;

    private void CheckIdle()
    {
        if (!_user.IsLoggedIn) return;
        var idle = DateTime.UtcNow - _lastInputUtc;
        if (idle < TimeSpan.FromMinutes(_security.AutoLogoutMinutes)) return;

        string who = _user.CurrentUser ?? "?";
        _logger.LogInformation("[AutoLogout] {User} idle {Idle:F1} phút → tự đăng xuất (hạ quyền, máy vẫn chạy)",
            who, idle.TotalMinutes);
        _audit.Record(who, "AutoLogout", allowed: true,
            detail: $"idle {idle.TotalMinutes:F1} phút ≥ ngưỡng {_security.AutoLogoutMinutes} phút");
        _user.Logout();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        if (_started && _security.AutoLogoutMinutes > 0)
        {
            InputManager.Current.PreProcessInput -= OnAnyInput;
            _user.UserChanged -= OnUserChanged;
        }
    }
}
