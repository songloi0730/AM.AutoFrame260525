// -------------------------------------------------------
// File:    UserService.cs
// Project: AM.Services
// Purpose: Phiên đăng nhập + RBAC — user store JSON, mật khẩu băm BCrypt.
// -------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Quản lý đăng nhập/đăng xuất + phân quyền. User store là file JSON (username, hash BCrypt, level).
/// Lần đầu chạy (chưa có file) sẽ seed user mặc định — <b>đổi mật khẩu trước khi đưa vào sản xuất</b>.
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

    private readonly ILogger<UserService> _logger;
    private readonly string _storePath;
    private readonly List<UserRecord> _users = [];
    private readonly Lock _lock = new();
    private string? _currentUser;
    private UserLevel _currentLevel = UserLevel.Null;

    /// <summary>Tạo service, nạp user store (seed mặc định nếu chưa có).</summary>
    public UserService(ILogger<UserService> logger, string storePath = "users.json")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(storePath);
        _logger = logger;
        _storePath = storePath;
        Load();
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
            _logger.LogWarning("[User] Đăng nhập thất bại: {User}", username);
            return false;
        }

        lock (_lock)
        {
            _currentUser = user!.Username;
            _currentLevel = user.Level;
        }
        _logger.LogInformation("[User] Đăng nhập: {User} ({Level})", user!.Username, user.Level);
        UserChanged?.Invoke(this, new UserChangedEventArgs(user.Username, user.Level));
        return true;
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
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task<bool> ResetPasswordAsync(string username, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(newPassword)) return false;
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
        return Task.FromResult(true);
    }

    // Đếm số Administrator (gọi trong _lock).
    private int AdminCount() => _users.Count(u => u.Level == UserLevel.Administrator);

    // True nếu đã có username (gọi trong _lock).
    private bool Exists(string username)
        => _users.Exists(u => string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));

    // Ghi store ra file (gọi trong _lock).
    private void Save()
    {
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

    private void SeedDefaults()
    {
        _users.Clear();
        _users.Add(new UserRecord("operator",  Hash("operator123"),  UserLevel.Operator));
        _users.Add(new UserRecord("linelead",  Hash("linelead123"),  UserLevel.LineLead));
        _users.Add(new UserRecord("engineer",  Hash("engineer123"),  UserLevel.Engineer));
        _users.Add(new UserRecord("admin",     Hash("admin123"),     UserLevel.Administrator));

        Save();
        _logger.LogWarning("[User] Đã seed user mặc định (operator/engineer/admin) — ĐỔI MẬT KHẨU trước khi sản xuất!");
    }

    private static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
}
