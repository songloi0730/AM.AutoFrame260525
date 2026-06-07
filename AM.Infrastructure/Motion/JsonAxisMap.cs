// -------------------------------------------------------
// File:    JsonAxisMap.cs
// Project: AM.Infrastructure
// Purpose: IAxisMap nạp axismap.json — trả IAxis đã bind qua HardwareManager.
// -------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Infrastructure.Motion;

/// <summary>
/// <see cref="IAxisMap"/> nạp danh sách <see cref="AxisConfig"/> từ <c>axismap.json</c>.
/// <see cref="ResolveAxis"/> resolve controller theo tên qua <see cref="IHardwareManagerService"/>
/// rồi trả về <see cref="MotionAxisAdapter"/> (cache theo tên trục).
/// </summary>
public sealed class JsonAxisMap : IAxisMap
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly IHardwareManagerService _hardware;
    private readonly ILogger<JsonAxisMap> _logger;
    private readonly Dictionary<string, AxisConfig> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IAxis> _adapters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tạo map, nạp <paramref name="path"/> (nếu có).</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="hardware">HardwareManager để resolve controller theo tên.</param>
    /// <param name="path">Đường dẫn axismap.json.</param>
    public JsonAxisMap(ILogger<JsonAxisMap> logger, IHardwareManagerService hardware,
        string path = "axismap.json")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _logger = logger;
        _hardware = hardware;
        Load(path);
    }

    /// <inheritdoc/>
    [SuppressMessage("Major Code Smell", "S2365:Properties should not copy collections",
        Justification = "Snapshot nhỏ, đọc ít lần; danh sách trục bất biến sau khi nạp")]
    public IReadOnlyList<AxisConfig> All => _byName.Values.ToList();

    /// <inheritdoc/>
    public AxisConfig GetConfig(string logicalName)
    {
        ArgumentNullException.ThrowIfNull(logicalName);
        return _byName.TryGetValue(logicalName, out var cfg)
            ? cfg
            : throw new KeyNotFoundException($"Axis '{logicalName}' không có trong axismap");
    }

    /// <inheritdoc/>
    public bool TryGet(string logicalName, out AxisConfig? config)
    {
        ArgumentNullException.ThrowIfNull(logicalName);
        return _byName.TryGetValue(logicalName, out config);
    }

    /// <inheritdoc/>
    public IAxis ResolveAxis(string logicalName)
    {
        if (_adapters.TryGetValue(logicalName, out var cached)) return cached;

        var cfg = GetConfig(logicalName);
        var controller = _hardware.Resolve<IMotionController>(cfg.Controller);
        var axis = new MotionAxisAdapter(controller, cfg);
        _adapters[logicalName] = axis;
        _logger.LogDebug("[AxisMap] Bind '{Axis}' → {Controller}[{Index}]", cfg.Name, cfg.Controller, cfg.Index);
        return axis;
    }

    private void Load(string path)
    {
        if (!File.Exists(path))
        {
            _logger.LogWarning("[AxisMap] Không thấy '{Path}' — axismap rỗng", path);
            return;
        }
        try
        {
            var list = JsonSerializer.Deserialize<List<AxisConfig>>(File.ReadAllText(path), JsonOptions);
            if (list is not null)
                foreach (var cfg in list)
                    if (!string.IsNullOrWhiteSpace(cfg.Name))
                        _byName[cfg.Name] = cfg;
            _logger.LogInformation("[AxisMap] Nạp {Count} trục từ '{Path}'", _byName.Count, path);
        }
#pragma warning disable CA1031 // file cấu hình lỗi không được làm sập app — log để sửa
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[AxisMap] Lỗi nạp '{Path}'", path);
        }
    }
}
