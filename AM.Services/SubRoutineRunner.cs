// -------------------------------------------------------
// File:    SubRoutineRunner.cs
// Project: AM.Services
// Purpose: Chạy subroutine với gate quyền + trạng thái máy + bọc lỗi → alarm.
// -------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using AM.Core.Abstractions.Interfaces;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Điều phối chạy subroutine: kiểm tra quyền (IUserService), trạng thái máy (không chạy khi Running/Paused),
/// và bọc <see cref="AlarmException"/> → raise alarm rồi ném lại để UI biết.
/// </summary>
public sealed class SubRoutineRunner : ISubRoutineRunner
{
    private readonly List<ISubRoutine> _subs;
    private readonly IUserService _user;
    private readonly IMasterController _master;
    private readonly IAlarmService _alarm;
    private readonly ILogger<SubRoutineRunner> _logger;

    /// <summary>Tạo runner với các subroutine đã đăng ký.</summary>
    public SubRoutineRunner(IEnumerable<ISubRoutine> subRoutines, IUserService user,
        IMasterController master, IAlarmService alarm, ILogger<SubRoutineRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(subRoutines);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(alarm);
        ArgumentNullException.ThrowIfNull(logger);
        _subs = [.. subRoutines];
        _user = user;
        _master = master;
        _alarm = alarm;
        _logger = logger;
    }

    /// <inheritdoc/>
    [SuppressMessage("Major Code Smell", "S2365:Properties should not copy collections",
        Justification = "Snapshot nhỏ, đọc ít lần; danh sách subroutine bất biến sau khi đăng ký")]
    public IReadOnlyList<ISubRoutine> Available => _subs.ToList();

    /// <inheritdoc/>
    public async Task RunAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var sub = _subs.Find(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Không có subroutine '{name}'");

        if (!_user.HasPermission(sub.RequiredLevel))
            throw new UnauthorizedAccessException(
                $"Cần quyền {sub.RequiredLevel} để chạy '{sub.Name}'");

        if (_master.State is MachineState.Running or MachineState.Paused)
            throw new InvalidOperationException(
                "Không thể chạy subroutine khi máy đang chạy/tạm dừng — dừng máy trước");

        _logger.LogInformation("[SubRoutineRunner] Chạy '{Name}' bởi {User}", sub.Name, _user.CurrentUser);
        try
        {
            await sub.ExecuteAsync(ct).ConfigureAwait(false);
        }
        // S2139: cố ý — raise alarm (UI thấy) + log audit rồi ném lại cho caller xử lý
#pragma warning disable S2139
        catch (AlarmException ex)
#pragma warning restore S2139
        {
            _logger.LogError(ex, "[SubRoutineRunner] '{Name}' alarm code={Code}", sub.Name, ex.AlarmCode);
            await _alarm.RaiseAsync(ex.AlarmCode, ex.Station, ex.Message, ct).ConfigureAwait(false);
            throw;
        }
    }
}
