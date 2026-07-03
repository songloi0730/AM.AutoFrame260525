// -------------------------------------------------------
// File:    SequenceLoader.cs
// Project: AM.Core.Sequencing
// Purpose: Nạp + validate sequence JSON — 2 pha (schema → ngữ nghĩa), gom toàn bộ lỗi (ADR 0011 §1)
// -------------------------------------------------------

using System.Text.Json;

namespace AM.Core.Sequencing;

/// <summary>
/// Nạp sequence từ JSON. Pha 1: parse + schema (thiếu trường, sai kiểu; key lạ = warning).
/// Pha 2: ngữ nghĩa (id trùng, order âm, timeout ≤ 0, retry, tên station qua
/// <see cref="IStationResolver"/>). Mọi lỗi gom vào <see cref="SequenceLoadResult"/> —
/// tên station sai chết NGAY LÚC NẠP, không lúc chạy (spec §4 test case 6).
/// </summary>
public static class SequenceLoader
{
    private static readonly JsonDocumentOptions DocOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly string[] RootKeys = ["name", "version", "settings", "steps"];
    private static readonly string[] SettingsKeys = ["continueMode", "maxProductsInFlight"];
    private static readonly string[] StepKeys =
        ["id", "station", "order", "timeoutMs", "onError", "retry", "onRetryExhausted", "runOnNg", "skipCountsAsNg"];

    /// <summary>Nạp + validate sequence. Không ném với lỗi nội dung — xem <see cref="SequenceLoadResult"/>.</summary>
    /// <param name="json">Nội dung file sequence JSON.</param>
    /// <param name="stationResolver">Resolver kiểm tra tên station tồn tại.</param>
    public static SequenceLoadResult Load(string json, IStationResolver stationResolver)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(stationResolver);

        var errors = new List<string>();
        var warnings = new List<string>();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, DocOptions);
        }
        catch (JsonException ex)
        {
            errors.Add($"JSON không hợp lệ: {ex.Message}");
            return new SequenceLoadResult(null, errors, warnings);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errors.Add("Gốc JSON phải là một object.");
                return new SequenceLoadResult(null, errors, warnings);
            }

            WarnUnknownKeys(root, RootKeys, "gốc", warnings);
            string? name = ReadString(root, "name", "gốc", required: true, errors);
            int version = ReadInt(root, "version", "gốc", required: false, defaultValue: 1, errors);
            var settings = ParseSettings(root, errors, warnings);
            var steps = ParseSteps(root, errors, warnings);

            ValidateSemantics(steps, stationResolver, errors);

            if (errors.Count > 0)
                return new SequenceLoadResult(null, errors, warnings);

            var definition = new SequenceDefinition(name!, version, settings, steps);
            return new SequenceLoadResult(definition, errors, warnings);
        }
    }

    /// <summary>Như <see cref="Load"/> nhưng ném <see cref="SequenceValidationException"/> khi có lỗi.</summary>
    /// <param name="json">Nội dung file sequence JSON.</param>
    /// <param name="stationResolver">Resolver kiểm tra tên station tồn tại.</param>
    public static SequenceDefinition LoadOrThrow(string json, IStationResolver stationResolver)
    {
        var result = Load(json, stationResolver);
        return result.Success
            ? result.Definition!
            : throw new SequenceValidationException(result.Errors);
    }

    // ─── Pha 1: parse schema ─────────────────────────────────────────────────

    private static SequenceSettings ParseSettings(JsonElement root, List<string> errors, List<string> warnings)
    {
        if (!root.TryGetProperty("settings", out var el))
            return SequenceSettings.Default;
        if (el.ValueKind != JsonValueKind.Object)
        {
            errors.Add("'settings' phải là object.");
            return SequenceSettings.Default;
        }

        WarnUnknownKeys(el, SettingsKeys, "settings", warnings);
        var mode = ReadEnum<ContinueMode>(el, "continueMode", "settings", errors)
                   ?? ContinueMode.UntilStopped;
        int inFlight = ReadInt(el, "maxProductsInFlight", "settings", required: false, defaultValue: 1, errors);
        return new SequenceSettings(mode, inFlight);
    }

    private static List<SequenceStep> ParseSteps(JsonElement root, List<string> errors, List<string> warnings)
    {
        var steps = new List<SequenceStep>();
        if (!root.TryGetProperty("steps", out var arr))
        {
            errors.Add("Thiếu trường bắt buộc 'steps'.");
            return steps;
        }
        if (arr.ValueKind != JsonValueKind.Array)
        {
            errors.Add("'steps' phải là mảng.");
            return steps;
        }

        int index = 0;
        foreach (var el in arr.EnumerateArray())
        {
            var step = ParseStep(el, index, errors, warnings);
            if (step is not null) steps.Add(step);
            index++;
        }

        if (index == 0) errors.Add("'steps' không được rỗng.");
        return steps;
    }

    private static SequenceStep? ParseStep(JsonElement el, int index, List<string> errors, List<string> warnings)
    {
        string ctx = $"steps[{index}]";
        if (el.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{ctx}: mỗi bước phải là object.");
            return null;
        }

        WarnUnknownKeys(el, StepKeys, ctx, warnings);

        int errorsBefore = errors.Count;
        string? id = ReadString(el, "id", ctx, required: true, errors);
        string? station = ReadString(el, "station", ctx, required: true, errors);
        int order = ReadInt(el, "order", ctx, required: true, defaultValue: 0, errors);
        int timeoutMs = ReadInt(el, "timeoutMs", ctx, required: true, defaultValue: 0, errors);
        var onError = ReadEnum<StepErrorAction>(el, "onError", ctx, errors) ?? StepErrorAction.Pause;
        int retry = ReadInt(el, "retry", ctx, required: false, defaultValue: 0, errors);
        var onExhausted = ReadEnum<StepErrorAction>(el, "onRetryExhausted", ctx, errors);
        bool runOnNg = ReadBool(el, "runOnNg", ctx, errors);
        bool skipCountsAsNg = ReadBool(el, "skipCountsAsNg", ctx, errors);

        if (errors.Count > errorsBefore) return null; // thiếu trường bắt buộc / sai kiểu

        // Chuẩn hoá nhánh retry (ADR 0011 §1)
        if (onError == StepErrorAction.Retry)
        {
            if (retry <= 0)
                errors.Add($"{ctx} ('{id}'): onError=Retry nhưng retry={retry} — retry phải > 0.");
            onExhausted ??= StepErrorAction.Pause; // mặc định an toàn nhất: operator quyết
        }
        else if (retry > 0 || onExhausted is not null)
        {
            warnings.Add($"{ctx} ('{id}'): 'retry'/'onRetryExhausted' bị bỏ qua vì onError={onError}.");
        }

        return new SequenceStep(id!, station!, order, timeoutMs, onError, retry,
            onExhausted, runOnNg, skipCountsAsNg);
    }

    // ─── Pha 2: ngữ nghĩa ────────────────────────────────────────────────────

    private static void ValidateSemantics(List<SequenceStep> steps, IStationResolver resolver, List<string> errors)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in steps)
        {
            if (!seenIds.Add(step.Id))
                errors.Add($"Bước '{step.Id}': id trùng lặp.");
            if (step.Order < 0)
                errors.Add($"Bước '{step.Id}': order={step.Order} — không được âm.");
            if (step.TimeoutMs <= 0)
                errors.Add($"Bước '{step.Id}': timeoutMs={step.TimeoutMs} — phải > 0 (không có timeout mặc định).");
            if (step.Retry < 0)
                errors.Add($"Bước '{step.Id}': retry={step.Retry} — không được âm.");
            if (!resolver.Contains(step.Station))
                errors.Add($"Bước '{step.Id}': station '{step.Station}' chưa được đăng ký. " +
                           $"Đã đăng ký: [{string.Join(", ", resolver.AllNames())}].");
        }
    }

    // ─── Đọc trường có kiểm kiểu ─────────────────────────────────────────────

    private static string? ReadString(JsonElement obj, string prop, string ctx, bool required, List<string> errors)
    {
        if (obj.TryGetProperty(prop, out var el))
        {
            if (el.ValueKind == JsonValueKind.String)
            {
                string? value = el.GetString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            errors.Add($"{ctx}: '{prop}' phải là chuỗi khác rỗng.");
            return null;
        }
        if (required) errors.Add($"{ctx}: thiếu trường bắt buộc '{prop}'.");
        return null;
    }

    private static int ReadInt(JsonElement obj, string prop, string ctx, bool required, int defaultValue, List<string> errors)
    {
        if (obj.TryGetProperty(prop, out var el))
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out int value)) return value;
            errors.Add($"{ctx}: '{prop}' phải là số nguyên.");
            return defaultValue;
        }
        if (required) errors.Add($"{ctx}: thiếu trường bắt buộc '{prop}'.");
        return defaultValue;
    }

    private static bool ReadBool(JsonElement obj, string prop, string ctx, List<string> errors)
    {
        if (!obj.TryGetProperty(prop, out var el)) return false;
        if (el.ValueKind is JsonValueKind.True or JsonValueKind.False) return el.GetBoolean();
        errors.Add($"{ctx}: '{prop}' phải là true/false.");
        return false;
    }

    private static TEnum? ReadEnum<TEnum>(JsonElement obj, string prop, string ctx, List<string> errors)
        where TEnum : struct, Enum
    {
        if (!obj.TryGetProperty(prop, out var el)) return null;
        if (el.ValueKind == JsonValueKind.String
            && Enum.TryParse<TEnum>(el.GetString(), ignoreCase: true, out var value))
        {
            return value;
        }
        errors.Add($"{ctx}: '{prop}' không hợp lệ — giá trị cho phép: [{string.Join(", ", Enum.GetNames<TEnum>())}].");
        return null;
    }

    private static void WarnUnknownKeys(JsonElement obj, string[] known, string ctx, List<string> warnings)
    {
        foreach (var name in obj.EnumerateObject()
                     .Select(p => p.Name)
                     .Where(n => !known.Contains(n, StringComparer.Ordinal)))
        {
            warnings.Add($"{ctx}: key lạ '{name}' bị bỏ qua.");
        }
    }
}
