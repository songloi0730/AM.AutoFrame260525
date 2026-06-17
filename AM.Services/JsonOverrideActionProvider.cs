// -------------------------------------------------------
// File:    JsonOverrideActionProvider.cs
// Project: AM.Services
// Purpose: Nạp metadata Supervised Override từ override-actions.json (fail-safe rỗng nếu thiếu/sai).
// -------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Hiện thực <see cref="IOverrideActionProvider"/> nạp từ <c>override-actions.json</c>:
/// <code>
/// { "Actions": [
///     { "id":"VacuumReleaseOverride", "labelKey":"Override.VacuumReleaseOverride", "icon":"E945",
///       "warningKey":"Override.VacuumReleaseOverride.Warn", "overridesGuard":"VacuumOff.guard", "countdownSec":3 } ] }
/// </code>
/// Thiếu file / sai định dạng → danh sách rỗng (fail-safe).
/// </summary>
public sealed class JsonOverrideActionProvider : IOverrideActionProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <inheritdoc/>
    public IReadOnlyList<OverrideActionDef> Actions { get; }

    /// <summary>Tạo provider từ danh sách dựng sẵn (dùng cho test).</summary>
    public JsonOverrideActionProvider(IReadOnlyList<OverrideActionDef> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        Actions = actions;
    }

    /// <summary>Nạp provider từ file; lỗi/thiếu → rỗng + log cảnh báo.</summary>
    public static JsonOverrideActionProvider LoadFromFile(string path, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(logger);
        if (!File.Exists(path))
        {
            logger.LogWarning("Overrides: không tìm thấy {Path} — dùng danh sách rỗng", path);
            return new JsonOverrideActionProvider([]);
        }
        try
        {
            var dto = JsonSerializer.Deserialize<RootDto>(File.ReadAllText(path), JsonOptions);
            var list = (dto?.Actions ?? []).Select(ToDef).ToList();
            logger.LogInformation("Overrides: nạp {Count} thao tác từ {Path}", list.Count, path);
            return new JsonOverrideActionProvider(list);
        }
#pragma warning disable CA1031 // nạp config: lỗi không được làm sập app, chỉ log + rỗng
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Overrides: lỗi nạp {Path} — dùng danh sách rỗng", path);
            return new JsonOverrideActionProvider([]);
        }
    }

    private static OverrideActionDef ToDef(ActionDto a) => new(
        a.Id ?? string.Empty,
        string.IsNullOrWhiteSpace(a.LabelKey) ? a.Id ?? string.Empty : a.LabelKey,
        string.IsNullOrWhiteSpace(a.Icon) ? "E7BA" : a.Icon,
        a.WarningKey ?? string.Empty,
        a.OverridesGuard,
        a.CountdownSec is > 0 ? a.CountdownSec.Value : 3);

    // ─── DTO deserialize (khởi tạo bởi System.Text.Json) ─────────────────────────
    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by System.Text.Json deserialization")]
    private sealed record RootDto(List<ActionDto>? Actions);

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by System.Text.Json deserialization")]
    private sealed record ActionDto(string? Id, string? LabelKey, string? Icon, string? WarningKey,
        string? OverridesGuard, int? CountdownSec);
}
