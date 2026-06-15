// -------------------------------------------------------
// File:    JsonIoTagMap.cs
// Project: AM.Hardware.IO
// Purpose: IIoTagMap nạp từ io.map.json (tag↔kênh + metadata địa chỉ/tên/xi lanh) — IO List của máy.
// -------------------------------------------------------

using System.Text.Json;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Models;

namespace AM.Hardware.IO;

/// <summary>
/// Bảng tag IO nạp từ <c>io.map.json</c> (schema mảng — mỗi kênh kèm địa chỉ/tên đa ngữ/metadata):
/// <code>
/// { "Di": [ { "tag":"DI_Safety_DoorClosed", "channel":0, "address":"X000",
///            "name":{"vi":"Cửa an toàn đóng","en":"...","zh":"安全门关闭"},
///            "localize":true, "rawName":"安全门关闭", "kind":"sensor", "station":"Safety" } ],
///   "Do": [ { "tag":"DO_Lamp_Green", "channel":3, "address":"Y003", "name":{...}, "kind":"actuator", "confirmDi":5 } ],
///   "Cylinders": [ { "name":{...}, "extendedDi":17, "retractedDi":18 } ] }
/// </code>
/// </summary>
public sealed class JsonIoTagMap : IIoTagMap
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<string, int> _di;
    private readonly Dictionary<string, int> _do;
    private readonly Dictionary<int, IoChannelDescriptor> _diByCh;
    private readonly Dictionary<int, IoChannelDescriptor> _doByCh;

    /// <inheritdoc/>
    public IReadOnlyList<IoChannelDescriptor> DiChannels { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IoChannelDescriptor> DoChannels { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IoCylinderDescriptor> Cylinders { get; }

    /// <summary>Tạo map từ hai dictionary tag→kênh (không metadata — descriptor rỗng).</summary>
    public JsonIoTagMap(IReadOnlyDictionary<string, int> diMap, IReadOnlyDictionary<string, int> doMap)
        : this(diMap, doMap, [], [], [])
    {
    }

    private JsonIoTagMap(
        IReadOnlyDictionary<string, int> diMap, IReadOnlyDictionary<string, int> doMap,
        IReadOnlyList<IoChannelDescriptor> diChannels, IReadOnlyList<IoChannelDescriptor> doChannels,
        IReadOnlyList<IoCylinderDescriptor> cylinders)
    {
        ArgumentNullException.ThrowIfNull(diMap);
        ArgumentNullException.ThrowIfNull(doMap);
        _di = new Dictionary<string, int>(diMap, StringComparer.OrdinalIgnoreCase);
        _do = new Dictionary<string, int>(doMap, StringComparer.OrdinalIgnoreCase);
        DiChannels = diChannels;
        DoChannels = doChannels;
        Cylinders  = cylinders;
        _diByCh = diChannels.ToDictionary(d => d.Channel);
        _doByCh = doChannels.ToDictionary(d => d.Channel);
    }

    /// <summary>
    /// Nạp map từ file io.map.json. Nhận CẢ hai schema: mảng mới (kèm metadata địa chỉ/tên/xi lanh)
    /// và object cũ (<c>{ "Di": { tag: ch } }</c> — chỉ tag↔kênh, descriptor rỗng).
    /// </summary>
    /// <exception cref="FileNotFoundException">Ném khi file không tồn tại.</exception>
    /// <exception cref="InvalidOperationException">Ném khi JSON sai định dạng.</exception>
    public static JsonIoTagMap LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"io.map.json không tồn tại: {path}", path);

        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"io.map.json rỗng/sai định dạng: {path}");

        var diEl = Prop(root, "Di");
        var doEl = Prop(root, "Do");

        // Schema object cũ — { "Di": { tag: ch }, "Do": { tag: ch } } → chỉ tag↔kênh.
        if (diEl?.ValueKind == JsonValueKind.Object || doEl?.ValueKind == JsonValueKind.Object)
        {
            return new JsonIoTagMap(
                diEl?.ValueKind == JsonValueKind.Object ? ReadIntMap(diEl.Value) : [],
                doEl?.ValueKind == JsonValueKind.Object ? ReadIntMap(doEl.Value) : []);
        }

        // Schema mảng mới — kèm metadata.
        var dto = JsonSerializer.Deserialize<MapDto>(json, JsonOptions)
            ?? throw new InvalidOperationException($"io.map.json rỗng/sai định dạng: {path}");

        var diChannels = ToDescriptors(dto.Di, "sensor");
        var doChannels = ToDescriptors(dto.Do, "actuator");
        var cylinders = (dto.Cylinders ?? [])
            .Select(c => new IoCylinderDescriptor(ToNameDict(c.Name), c.ExtendedDi, c.RetractedDi))
            .ToList();

        var di = diChannels.Where(d => !string.IsNullOrWhiteSpace(d.Tag))
            .ToDictionary(d => d.Tag, d => d.Channel, StringComparer.OrdinalIgnoreCase);
        var @do = doChannels.Where(d => !string.IsNullOrWhiteSpace(d.Tag))
            .ToDictionary(d => d.Tag, d => d.Channel, StringComparer.OrdinalIgnoreCase);

        return new JsonIoTagMap(di, @do, diChannels, doChannels, cylinders);
    }

    // Tìm property không phân biệt hoa thường (JsonElement mặc định phân biệt hoa thường).
    private static JsonElement? Prop(JsonElement root, string name)
        => root.EnumerateObject()
            .Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(p => (JsonElement?)p.Value)
            .FirstOrDefault();

    // Đọc object { tag: số kênh } → Dictionary.
    private static Dictionary<string, int> ReadIntMap(JsonElement obj)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in obj.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Number))
            map[p.Name] = p.Value.GetInt32();
        return map;
    }

    /// <inheritdoc/>
    public int ResolveDi(string tag) => Resolve(_di, tag, "DI");

    /// <inheritdoc/>
    public int ResolveDo(string tag) => Resolve(_do, tag, "DO");

    /// <inheritdoc/>
    public bool ContainsDi(string tag) => _di.ContainsKey(tag);

    /// <inheritdoc/>
    public bool ContainsDo(string tag) => _do.ContainsKey(tag);

    /// <inheritdoc/>
    public IoChannelDescriptor? DescribeDi(int channel) => _diByCh.GetValueOrDefault(channel);

    /// <inheritdoc/>
    public IoChannelDescriptor? DescribeDo(int channel) => _doByCh.GetValueOrDefault(channel);

    private static List<IoChannelDescriptor> ToDescriptors(List<ChannelDto>? dtos, string defaultKind)
        => (dtos ?? [])
            .Select(d => new IoChannelDescriptor(
                d.Channel,
                d.Tag ?? string.Empty,
                string.IsNullOrWhiteSpace(d.Address) ? d.Tag ?? d.Channel.ToString(System.Globalization.CultureInfo.InvariantCulture) : d.Address,
                ToNameDict(d.Name),
                d.Localize ?? true,
                d.RawName,
                string.IsNullOrWhiteSpace(d.Kind) ? defaultKind : d.Kind,
                d.Station,
                d.ConfirmDi))
            .ToList();

    private static Dictionary<string, string> ToNameDict(Dictionary<string, string>? name)
        => new(name ?? [], StringComparer.OrdinalIgnoreCase);

    private static int Resolve(Dictionary<string, int> map, string tag, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return map.TryGetValue(tag, out int ch)
            ? ch
            : throw new KeyNotFoundException($"Không tìm thấy tag {kind} '{tag}' trong io.map.json");
    }

    // ─── DTO nội bộ cho deserialize schema mảng (khởi tạo bởi System.Text.Json qua reflection) ───
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
        Justification = "Instantiated by System.Text.Json deserialization")]
    private sealed record ChannelDto(
        int Channel, string? Tag, string? Address, Dictionary<string, string>? Name,
        bool? Localize, string? RawName, string? Kind, string? Station, int? ConfirmDi);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
        Justification = "Instantiated by System.Text.Json deserialization")]
    private sealed record CylinderDto(Dictionary<string, string>? Name, int ExtendedDi, int RetractedDi);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
        Justification = "Instantiated by System.Text.Json deserialization")]
    private sealed record MapDto(List<ChannelDto>? Di, List<ChannelDto>? Do, List<CylinderDto>? Cylinders);
}
