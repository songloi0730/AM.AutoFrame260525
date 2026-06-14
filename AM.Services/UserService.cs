// -------------------------------------------------------
// File:    UserService.cs
// Project: AM.Services
// Purpose: Phiên đăng nhập + RBAC — user store JSON, mật khẩu băm BCrypt.
// -------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
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
            }
#pragma warning disable CA1031 // file user lỗi/định dạng cũ → seed lại mặc định, không sập app
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "[User] Lỗi nạp {Path} — seed mặc định", _storePath);
            }
        }

        SeedDefaults();
    }

    private void SeedDefaults()
    {
        _users.Clear();
        _users.Add(new UserRecord("operator",  Hash("operator123"),  UserLevel.Operator));
        _users.Add(new UserRecord("linelead",  Hash("linelead123"),  UserLevel.LineLead));
        _users.Add(new UserRecord("engineer",  Hash("engineer123"),  UserLevel.Engineer));
        _users.Add(new UserRecord("admin",     Hash("admin123"),     UserLevel.Administrator));

        try
        {
            var store = new UserStore(CurrentSchemaVersion, _users);
            File.WriteAllText(_storePath, JsonSerializer.Serialize(store, JsonOptions));
        }
#pragma warning disable CA1031 // ghi seed lỗi (vd read-only) — vẫn chạy được với user in-memory
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "[User] Không ghi được {Path} — dùng user mặc định in-memory", _storePath);
        }
        _logger.LogWarning("[User] Đã seed user mặc định (operator/engineer/admin) — ĐỔI MẬT KHẨU trước khi sản xuất!");
    }

    private static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
}
