// -------------------------------------------------------
// File:    JsonRecoveryActionProvider.cs
// Project: AM.Services
// Purpose: Nạp metadata thao tác trạm từ recovery-actions.json (schema khai báo guard tầng 3 theo signal keys).
// -------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Hiện thực <see cref="IRecoveryActionProvider"/> nạp từ <c>recovery-actions.json</c>:
/// <code>
/// { "Actions": [
///     { "id":"ConveyorToggle", "labelKey":"Recovery.ConveyorToggle", "icon":"E896", "risk":"R1",
///       "requiresAdmin":false,
///       "guard": { "anyOf": [ [ {"key":"Safety.AllSafe","expected":true} ] ], "blockKey":"Recovery.ConveyorToggle.Block" } } ] }
/// </code>
/// Thiếu file / sai định dạng → danh sách rỗng (fail-safe, app vẫn chạy).
/// </summary>
public sealed class JsonRecoveryActionProvider : IRecoveryActionProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <inheritdoc/>
    public IReadOnlyList<RecoveryActionDef> Actions { get; }

    /// <summary>Tạo provider từ danh sách dựng sẵn (dùng cho test).</summary>
    public JsonRecoveryActionProvider(IReadOnlyList<RecoveryActionDef> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        Actions = actions;
    }

    /// <summary>Nạp provider từ file; lỗi/thiếu → rỗng + log cảnh báo.</summary>
    public static JsonRecoveryActionProvider LoadFromFile(string path, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(logger);
        if (!File.Exists(path))
        {
            logger.LogWarning("RecoveryActions: không tìm thấy {Path} — dùng danh sách rỗng", path);
            return new JsonRecoveryActionProvider([]);
        }
        try
        {
            var dto = JsonSerializer.Deserialize<RootDto>(File.ReadAllText(path), JsonOptions);
            var list = (dto?.Actions ?? []).Select(ToDef).ToList();
            logger.LogInformation("RecoveryActions: nạp {Count} thao tác từ {Path}", list.Count, path);
            return new JsonRecoveryActionProvider(list);
        }
#pragma warning disable CA1031 // nạp config: lỗi không được làm sập app, chỉ log + rỗng
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "RecoveryActions: lỗi nạp {Path} — dùng danh sách rỗng", path);
            return new JsonRecoveryActionProvider([]);
        }
    }

    private static RecoveryActionDef ToDef(ActionDto a)
    {
        var risk = Enum.TryParse<RiskTier>(a.Risk, ignoreCase: true, out var r) ? r : RiskTier.R1;
        GuardCondition? guard = null;
        if (a.Guard?.AnyOf is { Count: > 0 } anyOf)
        {
            var groups = anyOf
                .Select(g => (IReadOnlyList<SignalRequirement>)
                    (g ?? []).Select(s => new SignalRequirement(s.Key ?? string.Empty, s.Expected)).ToList())
                .ToList();
            guard = new GuardCondition(groups, a.Guard.BlockKey);
        }
        return new RecoveryActionDef(
            a.Id ?? string.Empty,
            string.IsNullOrWhiteSpace(a.LabelKey) ? a.Id ?? string.Empty : a.LabelKey,
            string.IsNullOrWhiteSpace(a.Icon) ? "E90F" : a.Icon,
            risk, guard, a.RequiresAdmin ?? false);
    }

    // ─── DTO deserialize (khởi tạo bởi System.Text.Json) ─────────────────────────
    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by System.Text.Json deserialization")]
    private sealed record RootDto(List<ActionDto>? Actions);

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by System.Text.Json deserialization")]
    private sealed record ActionDto(string? Id, string? LabelKey, string? Icon, string? Risk,
        bool? RequiresAdmin, GuardDto? Guard);

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by System.Text.Json deserialization")]
    private sealed record GuardDto(List<List<ReqDto>>? AnyOf, string? BlockKey);

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by System.Text.Json deserialization")]
    private sealed record ReqDto(string? Key, bool Expected);
}
