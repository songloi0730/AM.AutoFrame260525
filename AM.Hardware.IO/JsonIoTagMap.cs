// -------------------------------------------------------
// File:    JsonIoTagMap.cs
// Project: AM.Hardware.IO
// Purpose: IIoTagMap nạp từ io.map.json (tag → kênh) — IO List của máy.
// -------------------------------------------------------

using System.Text.Json;
using AM.Core.Abstractions.Interfaces.Hardware;

namespace AM.Hardware.IO;

/// <summary>
/// Bảng tag IO nạp từ file JSON dạng:
/// <code>
/// { "Di": { "PartPresent_A": 0, "VacOk_A": 1 },
///   "Do": { "Vac_A": 0, "Clamp_A": 1 } }
/// </code>
/// </summary>
public sealed class JsonIoTagMap : IIoTagMap
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<string, int> _di;
    private readonly Dictionary<string, int> _do;

    /// <summary>Tạo map từ hai dictionary tag→kênh.</summary>
    public JsonIoTagMap(IReadOnlyDictionary<string, int> diMap, IReadOnlyDictionary<string, int> doMap)
    {
        ArgumentNullException.ThrowIfNull(diMap);
        ArgumentNullException.ThrowIfNull(doMap);
        _di = new Dictionary<string, int>(diMap, StringComparer.OrdinalIgnoreCase);
        _do = new Dictionary<string, int>(doMap, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Nạp map từ file io.map.json.</summary>
    /// <exception cref="FileNotFoundException">Ném khi file không tồn tại.</exception>
    /// <exception cref="InvalidOperationException">Ném khi JSON sai định dạng.</exception>
    public static JsonIoTagMap LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"io.map.json không tồn tại: {path}", path);

        string json = File.ReadAllText(path);
        var sections = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(json, JsonOptions)
            ?? throw new InvalidOperationException($"io.map.json rỗng/sai định dạng: {path}");
        var ci = new Dictionary<string, Dictionary<string, int>>(sections, StringComparer.OrdinalIgnoreCase);
        return new JsonIoTagMap(
            ci.GetValueOrDefault("Di") ?? new(),
            ci.GetValueOrDefault("Do") ?? new());
    }

    /// <inheritdoc/>
    public int ResolveDi(string tag) => Resolve(_di, tag, "DI");

    /// <inheritdoc/>
    public int ResolveDo(string tag) => Resolve(_do, tag, "DO");

    /// <inheritdoc/>
    public bool ContainsDi(string tag) => _di.ContainsKey(tag);

    /// <inheritdoc/>
    public bool ContainsDo(string tag) => _do.ContainsKey(tag);

    private static int Resolve(Dictionary<string, int> map, string tag, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return map.TryGetValue(tag, out int ch)
            ? ch
            : throw new KeyNotFoundException($"Không tìm thấy tag {kind} '{tag}' trong io.map.json");
    }
}
