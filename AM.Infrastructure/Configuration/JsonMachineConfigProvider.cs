// -------------------------------------------------------
// File:    JsonMachineConfigProvider.cs
// Project: AM.Infrastructure
// Purpose: IMachineConfigProvider nạp machine.json (layout máy).
// -------------------------------------------------------

using System.Text.Json;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Infrastructure.Configuration;

/// <summary>Nạp <see cref="MachineConfig"/> từ <c>machine.json</c>; rỗng nếu thiếu file/lỗi.</summary>
public sealed class JsonMachineConfigProvider : IMachineConfigProvider
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly ILogger<JsonMachineConfigProvider> _logger;

    /// <summary>Tạo provider, nạp <paramref name="path"/> (nếu có).</summary>
    public JsonMachineConfigProvider(ILogger<JsonMachineConfigProvider> logger, string path = "machine.json")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _logger = logger;
        Machine = Load(path);
    }

    /// <inheritdoc/>
    public MachineConfig Machine { get; }

    /// <inheritdoc/>
    public StationConfig? GetStation(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Machine.Stations.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private MachineConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            _logger.LogWarning("[MachineConfig] Không thấy '{Path}' — dùng config rỗng", path);
            return new MachineConfig();
        }
        try
        {
            var cfg = JsonSerializer.Deserialize<MachineConfig>(File.ReadAllText(path), JsonOptions);
            if (cfg is not null)
            {
                _logger.LogInformation("[MachineConfig] Nạp '{Machine}' ({Count} station) từ '{Path}'",
                    cfg.MachineName, cfg.Stations.Count, path);
                return cfg;
            }
        }
#pragma warning disable CA1031 // lỗi cấu hình không được sập app
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[MachineConfig] Lỗi nạp '{Path}'", path);
        }
        return new MachineConfig();
    }
}
