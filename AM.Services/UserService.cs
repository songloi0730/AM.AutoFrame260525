// -------------------------------------------------------
// File:    UserService.cs
// Project: AM.Services
// Purpose: Phiên đăng nhập + RBAC — user store JSON, mật khẩu băm BCrypt.
// -------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Quản lý đăng nhập/đăng xuất + phân quyền. User store là file JSON (username, hash BCrypt, level).
/// Lần đầu chạy (chưa có file) sẽ seed user mặc định — <b>đổi mật khẩu trước khi đưa vào sản xuất</b>.
/// Chính sách nhà máy (design-notes/0012): KHÔNG lockout — sai nhiều lần chỉ audit + alarm;
/// break-glass = user <c>service</c> (mã theo ngày) và user <c>recovery</c> (file khôi phục), cả hai ồn ào.
/// </summary>
public sealed class UserService : IUserService
{
    // Phiên bản schema store. Tăng khi đổi nghĩa dữ liệu (vd đổi thứ tự enum UserLevel) → file cũ tự re-seed.
    private const int CurrentSchemaVersion = 2;

    // Level lưu dạng CHUỖI tên enum (JsonStringEnumConverter) — đổi thứ tự enum sau này không phá nghĩa file.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // Username dành riêng cho break-glass (design-notes/0012) — không tạo được qua CreateUserAsync
    private const string ServiceUsername = "service";
    private const string RecoveryUsername = "recovery";
    private const string RecoveryPassword = "recovery";

    private readonly ILogger<UserService> _logger;
    private readonly string _storePath;
    private readonly SecurityOptions _security;
    private readonly IAlarmService? _alarmService;
    private readonly IAuditService? _audit;
    private readonly string _recoveryKeyPath;
    private readonly List<UserRecord> _users = [];
    private readonly Dictionary<string, int> _failStreaks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
    private string? _currentUser;
    private UserLevel _currentLevel = UserLevel.Null;
    private DateTime _recoveryWindowUntil = DateTime.MinValue;
    private bool? _hasDefaultPwdCache;

    /// <summary>Tạo service, nạp user store (seed mặc định nếu chưa có).</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="storePath">Đường dẫn file user store JSON.</param>
    /// <param name="security">Chính sách bảo mật (null = mặc định — day-code TẮT).</param>
    /// <param name="alarmService">Raise alarm bảo mật 40010–40012 (null = chỉ log).</param>
    /// <param name="auditService">Ghi audit đăng nhập/break-glass (null = chỉ log).</param>
    /// <param name="recoveryKeyPath">Đường dẫn file khôi phục (null = cạnh executable).</param>
    public UserService(ILogger<UserService> logger, string storePath = "users.json",
        SecurityOptions? security = null, IAlarmService? alarmService = null,
        IAuditService? auditService = null, string? recoveryKeyPath = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(storePath);
        _logger = logger;
        _storePath = storePath;
        _security = security ?? new SecurityOptions();
        _alarmService = alarmService;
        _audit = auditService;
        _recoveryKeyPath = recoveryKeyPath
            ?? Path.Combine(AppContext.BaseDirectory, _security.RecoveryKeyFileName);
        Load();
        ArmRecoveryWindowIfKeyPresent();
    }

    /// <inheritdoc/>
    public string? CurrentUser { get { lock (_lock) { return _currentUser; } } }

    /// <inheritdoc/>
    public UserLevel CurrentLevel { get { lock (_lock) { return _currentLevel; } } }

    /// <inheritdoc/>
    public bool IsLoggedIn { get { lock (_lock) { return _currentLevel != UserLevel.Null; } } }

    /// <inheritdoc/>
    public event EventHandler<UserChangedEventArgs>? UserChanged;

    /// <inheritdoc/>
    public async Task<bool> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        // Break-glass (design-notes/0012) — hai username dành riêng, không nằm trong store
        if (string.Equals(username, ServiceUsername, StringComparison.OrdinalIgnoreCase))
            return LoginWithDayCode(password);
        if (string.Equals(username, RecoveryUsername, StringComparison.OrdinalIgnoreCase))
            return LoginWithRecoveryWindow(password);

        UserRecord? user;
        lock (_lock)
        {
            user = _users.Find(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        }

        // BCrypt.Verify nặng CPU → chạy ngoài thread UI
        bool ok = user is not null
            && await Task.Run(() => BCrypt.Net.BCrypt.Verify(password, user.PasswordHash), ct).ConfigureAwait(false);

        if (!ok)
        {
            RegisterLoginFailure(username);
            return false;
        }

        lock (_lock)
        {
            _failStreaks.Remove(username);
            _currentUser = user!.Username;
            _currentLevel = user.Level;
        }
        _logger.LogInformation("[User] Đăng nhập: {User} ({Level})", user!.Username, user.Level);
        _audit?.Record(user.Username, "Login", allowed: true);
        UserChanged?.Invoke(this, new UserChangedEventArgs(user.Username, user.Level));
        return true;
    }

    // ─── Chính sách nhà máy: audit-only + break-glass (design-notes/0012) ────────

    /// <summary>
    /// Tính mã dịch vụ theo ngày: HMAC-SHA256(secret, machineId + yyyyMMdd) → 8 chữ số.
    /// Cùng thuật toán với tool <c>scripts/am-daycode.ps1</c> — đổi một bên phải đổi cả hai.
    /// </summary>
    /// <param name="secret">Secret của hãng máy (không commit vào repo).</param>
    /// <param name="machineId">Mã định danh máy.</param>
    /// <param name="date">Ngày hiệu lực của mã.</param>
    public static string ComputeDayCode(string secret, string machineId, DateOnly date)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);
        byte[] mac = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(machineId + date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)));
        uint value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(mac);
        return (value % 100_000_000u).ToString("D8", CultureInfo.InvariantCulture);
    }

    // Đăng nhập 'service' bằng day-code (chấp nhận ±1 ngày — lệch đồng hồ/ca đêm) → SuperUser tạm.
    private bool LoginWithDayCode(string code)
    {
        if (string.IsNullOrWhiteSpace(_security.DayCodeSecret))
        {
            _logger.LogWarning("[User] Từ chối 'service' — DayCodeSecret chưa cấu hình (day-code đang TẮT)");
            return false;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        bool ok = false;
        for (int offset = -1; offset <= 1 && !ok; offset++)
        {
            ok = string.Equals(code,
                ComputeDayCode(_security.DayCodeSecret, _security.MachineId, today.AddDays(offset)),
                StringComparison.Ordinal);
        }
        if (!ok)
        {
            RegisterLoginFailure(ServiceUsername);
            return false;
        }

        lock (_lock)
        {
            _failStreaks.Remove(ServiceUsername);
            _currentUser = ServiceUsername;
            _currentLevel = UserLevel.SuperUser;
        }
        _logger.LogWarning("[User] BREAK-GLASS: 'service' đăng nhập bằng mã theo ngày → SuperUser (máy {Machine})",
            _security.MachineId);
        _audit?.Record(ServiceUsername, "Login.DayCode", allowed: true, detail: $"máy {_security.MachineId}");
        RaiseSecurityAlarm(AlarmCodes.SecurityServiceLogin,
            "Đăng nhập quyền dịch vụ bằng mã theo ngày (SuperUser tạm thời)");
        UserChanged?.Invoke(this, new UserChangedEventArgs(ServiceUsername, UserLevel.SuperUser));
        return true;
    }

    // Đăng nhập 'recovery' trong cửa sổ mở bởi file khôi phục → Administrator tạm.
    private bool LoginWithRecoveryWindow(string password)
    {
        bool armed;
        lock (_lock) { armed = DateTime.UtcNow <= _recoveryWindowUntil; }
        if (!armed || !string.Equals(password, RecoveryPassword, StringComparison.Ordinal))
        {
            RegisterLoginFailure(RecoveryUsername);
            return false;
        }

        lock (_lock)
        {
            _failStreaks.Remove(RecoveryUsername);
            _currentUser = RecoveryUsername;
            _currentLevel = UserLevel.Administrator;
        }
        _logger.LogWarning("[User] BREAK-GLASS: 'recovery' đăng nhập trong cửa sổ khôi phục → Administrator tạm");
        _audit?.Record(RecoveryUsername, "Login.RecoveryWindow", allowed: true);
        UserChanged?.Invoke(this, new UserChangedEventArgs(RecoveryUsername, UserLevel.Administrator));
        return true;
    }

    // Lúc boot: file khôi phục tồn tại → XOÁ NGAY (một lần dùng) + mở cửa sổ 'recovery' + alarm ồn ào.
    private void ArmRecoveryWindowIfKeyPresent()
    {
        try
        {
            if (!File.Exists(_recoveryKeyPath)) return;
            File.Delete(_recoveryKeyPath);
            _recoveryWindowUntil = DateTime.UtcNow.AddMinutes(_security.RecoveryWindowMinutes);
            _logger.LogWarning(
                "[User] BREAK-GLASS: phát hiện {File} — 'recovery' đăng nhập được trong {Min} phút (file đã xoá)",
                _recoveryKeyPath, _security.RecoveryWindowMinutes);
            _audit?.Record(RecoveryUsername, "BreakGlass.KeyFileArmed", allowed: true, detail: _recoveryKeyPath);
            RaiseSecurityAlarm(AlarmCodes.SecurityRecoveryUsed,
                $"File khôi phục kích hoạt — tài khoản 'recovery' có hiệu lực {_security.RecoveryWindowMinutes} phút");
        }
#pragma warning disable CA1031 // lỗi IO khi xử lý key file không được chặn app khởi động
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[User] Lỗi xử lý file khôi phục {File}", _recoveryKeyPath);
        }
    }

    // Đếm chuỗi sai liên tiếp theo username — KHÔNG khoá (0012): tới ngưỡng thì alarm để ca trưởng biết.
    private void RegisterLoginFailure(string username)
    {
        int streak;
        lock (_lock)
        {
            _failStreaks.TryGetValue(username, out streak);
            streak++;
            _failStreaks[username] = streak;
        }
        _logger.LogWarning("[User] Đăng nhập thất bại: {User} (lần {Streak} liên tiếp)", username, streak);
        _audit?.Record(username, "Login", allowed: false, detail: $"sai liên tiếp lần {streak}");
        if (streak == _security.FailedLoginAlarmThreshold)
        {
            RaiseSecurityAlarm(AlarmCodes.SecurityLoginFailures,
                $"Tài khoản '{username}' sai mật khẩu {streak} lần liên tiếp — kiểm tra ai đang thao tác");
        }
    }

    // Alarm bảo mật fire-and-forget — luồng đăng nhập không chờ pipeline alarm/DB.
    private void RaiseSecurityAlarm(int code, string message)
    {
        if (_alarmService is null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await _alarmService.RaiseAsync(code, "SECURITY", message).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // alarm lỗi (DB...) không được phá luồng đăng nhập
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "[User] Không raise được alarm bảo mật {Code}", code);
            }
        });
    }

    /// <inheritdoc/>
    public void Logout()
    {
        lock (_lock)
        {
            if (_currentLevel == UserLevel.Null) return;
            _currentUser = null;
            _currentLevel = UserLevel.Null;
        }
        _logger.LogInformation("[User] Đăng xuất");
        UserChanged?.Invoke(this, new UserChangedEventArgs(null, UserLevel.Null));
    }

    /// <inheritdoc/>
    public bool HasPermission(UserLevel required) => CurrentLevel >= required;

    // ─── Quản trị tài khoản ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IReadOnlyList<UserAccount> GetUsers()
    {
        lock (_lock)
            return _users.Select(u => new UserAccount(u.Username, u.Level)).ToList();
    }

    /// <inheritdoc/>
    public async Task<bool> CreateUserAsync(string username, string password, UserLevel level,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return false;
        if (IsReservedUsername(username))
        {
            _logger.LogWarning("[User] Không tạo được '{User}' — tên dành riêng cho break-glass", username);
            return false;
        }
        if (password.Length < _security.MinPasswordLength)
        {
            _logger.LogWarning("[User] Không tạo được {User} — mật khẩu ngắn hơn {Min} ký tự",
                username, _security.MinPasswordLength);
            return false;
        }
        lock (_lock)
        {
            if (Exists(username)) return false;
        }
        string hash = await Task.Run(() => Hash(password), ct).ConfigureAwait(false);
        lock (_lock)
        {
            if (Exists(username)) return false; // re-check sau await
            _users.Add(new UserRecord(username.Trim(), hash, level));
            Save();
        }
        _logger.LogInformation("[User] Tạo tài khoản {User} ({Level})", username, level);
        // Audit thao tác quản trị (S90): hiện trong màn Nhật ký audit — ai thêm user nào, lúc nào
        _audit?.Record(CurrentUser ?? "?", "User.Create", allowed: true, detail: $"{username.Trim()} ({level})");
        return true;
    }

    /// <inheritdoc/>
    public Task<bool> DeleteUserAsync(string username, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            int i = _users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            if (i < 0) return Task.FromResult(false);
            var u = _users[i];
            if (string.Equals(u.Username, _currentUser, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(false); // không xoá user đang đăng nhập
            if (u.Level == UserLevel.Administrator && AdminCount() <= 1)
                return Task.FromResult(false); // không xoá Administrator cuối cùng
            _users.RemoveAt(i);
            Save();
        }
        _logger.LogInformation("[User] Xoá tài khoản {User}", username);
        _audit?.Record(CurrentUser ?? "?", "User.Delete", allowed: true, detail: username);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task<bool> ResetPasswordAsync(string username, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(newPassword)) return false;
        if (newPassword.Length < _security.MinPasswordLength)
        {
            _logger.LogWarning("[User] Không đặt lại mật khẩu {User} — ngắn hơn {Min} ký tự",
                username, _security.MinPasswordLength);
            return false;
        }
        lock (_lock)
        {
            if (!Exists(username)) return false;
        }
        string hash = await Task.Run(() => Hash(newPassword), ct).ConfigureAwait(false);
        lock (_lock)
        {
            int i = _users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            if (i < 0) return false;
            _users[i] = _users[i] with { PasswordHash = hash };
            Save();
        }
        _logger.LogInformation("[User] Đặt lại mật khẩu {User}", username);
        _audit?.Record(CurrentUser ?? "?", "User.ResetPassword", allowed: true, detail: username);
        return true;
    }

    /// <inheritdoc/>
    public Task<bool> SetLevelAsync(string username, UserLevel level, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            int i = _users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            if (i < 0) return Task.FromResult(false);
            var u = _users[i];
            if (u.Level == UserLevel.Administrator && level < UserLevel.Administrator && AdminCount() <= 1)
                return Task.FromResult(false); // không hạ quyền Administrator cuối cùng
            _users[i] = u with { Level = level };
            Save();
        }
        _logger.LogInformation("[User] Đổi quyền {User} → {Level}", username, level);
        _audit?.Record(CurrentUser ?? "?", "User.SetLevel", allowed: true, detail: $"{username} → {level}");
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task<bool> HasDefaultPasswordsAsync(CancellationToken ct = default)
    {
        List<UserRecord> snapshot;
        lock (_lock)
        {
            if (_hasDefaultPwdCache is bool cached) return cached;
            snapshot = [.. _users];
        }

        // BCrypt.Verify nặng CPU → chạy nền; hash hỏng coi như KHÔNG phải mặc định
        bool any = await Task.Run(() =>
        {
            foreach (var (seedUser, seedPwd, _) in SeedAccounts)
            {
                var rec = snapshot.Find(u => string.Equals(u.Username, seedUser, StringComparison.OrdinalIgnoreCase));
                if (rec is null) continue;
                try
                {
                    if (BCrypt.Net.BCrypt.Verify(seedPwd, rec.PasswordHash)) return true;
                }
#pragma warning disable CA1031 // hash sai định dạng → bỏ qua record đó, không phá vòng kiểm tra
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    _logger.LogWarning(ex, "[User] Hash của {User} không đọc được khi kiểm mật khẩu mặc định", seedUser);
                }
            }
            return false;
        }, ct).ConfigureAwait(false);

        lock (_lock) { _hasDefaultPwdCache = any; }
        return any;
    }

    // True nếu username thuộc nhóm dành riêng break-glass.
    private static bool IsReservedUsername(string username)
        => string.Equals(username.Trim(), ServiceUsername, StringComparison.OrdinalIgnoreCase)
        || string.Equals(username.Trim(), RecoveryUsername, StringComparison.OrdinalIgnoreCase);

    // Đếm số Administrator (gọi trong _lock).
    private int AdminCount() => _users.Count(u => u.Level == UserLevel.Administrator);

    // True nếu đã có username (gọi trong _lock).
    private bool Exists(string username)
        => _users.Exists(u => string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));

    // Ghi store ra file (gọi trong _lock).
    private void Save()
    {
        _hasDefaultPwdCache = null; // mật khẩu/danh sách đổi → banner cảnh báo tính lại
        try
        {
            var store = new UserStore(CurrentSchemaVersion, _users);
            File.WriteAllText(_storePath, JsonSerializer.Serialize(store, JsonOptions));
        }
#pragma warning disable CA1031 // ghi store lỗi (vd read-only) — giữ thay đổi in-memory, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "[User] Không ghi được {Path}", _storePath);
        }
    }

    // ─── User store (JSON) ─────────────────────────────────────────────────────

    private sealed record UserRecord(string Username, string PasswordHash, UserLevel Level);

    /// <summary>Envelope store có version — để migrate khi đổi nghĩa dữ liệu (vd reorder UserLevel).</summary>
    private sealed record UserStore(int SchemaVersion, List<UserRecord> Users);

    private void Load()
    {
        if (File.Exists(_storePath))
        {
            try
            {
                var store = JsonSerializer.Deserialize<UserStore>(File.ReadAllText(_storePath), JsonOptions);
                // File cũ (mảng trần `[...]`, không envelope) → store null hoặc Users null → re-seed.
                // Schema cũ hơn (vd lưu Level int trước khi reorder enum ở S47) → re-seed cho đúng cấp.
                if (store is { SchemaVersion: CurrentSchemaVersion, Users.Count: > 0 })
                {
                    _users.AddRange(store.Users);
                    _logger.LogInformation("[User] Nạp {Count} user từ {Path} (schema v{Ver})",
                        _users.Count, _storePath, store.SchemaVersion);
                    return;
                }
                _logger.LogWarning("[User] Store {Path} sai/cũ schema → seed lại mặc định", _storePath);
                BackupCorruptStore();
            }
#pragma warning disable CA1031 // file user lỗi/định dạng cũ → seed lại mặc định, không sập app
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "[User] Lỗi nạp {Path} — seed mặc định", _storePath);
                BackupCorruptStore();
            }
        }

        SeedDefaults();
    }

    // P0.3: re-seed sẽ GHI ĐÈ store — backup file cũ trước để user đã tạo không mất im lặng.
    private void BackupCorruptStore()
    {
        try
        {
            string backupPath = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{_storePath}.bak-{DateTime.Now:yyyyMMdd-HHmmss}");
            File.Copy(_storePath, backupPath, overwrite: true);
            _logger.LogError("[User] Store cũ đã BACKUP vào {Backup} trước khi seed lại — cần user cũ thì khôi phục từ đây",
                backupPath);
        }
#pragma warning disable CA1031 // backup lỗi (read-only...) không được chặn seed — app vẫn phải chạy
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[User] Không backup được {Path} — dữ liệu cũ sẽ bị ghi đè", _storePath);
        }
    }

    // Tài khoản seed + mật khẩu mặc định — nguồn duy nhất cho SeedDefaults và kiểm banner cảnh báo.
    private static readonly (string User, string Pwd, UserLevel Level)[] SeedAccounts =
    [
        ("operator", "operator123", UserLevel.Operator),
        ("linelead", "linelead123", UserLevel.LineLead),
        ("engineer", "engineer123", UserLevel.Engineer),
        ("admin",    "admin123",    UserLevel.Administrator),
    ];

    private void SeedDefaults()
    {
        _users.Clear();
        foreach (var (user, pwd, level) in SeedAccounts)
            _users.Add(new UserRecord(user, Hash(pwd), level));

        Save();
        _logger.LogWarning("[User] Đã seed user mặc định (operator/engineer/admin) — ĐỔI MẬT KHẨU trước khi sản xuất!");
    }

    private static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
}
