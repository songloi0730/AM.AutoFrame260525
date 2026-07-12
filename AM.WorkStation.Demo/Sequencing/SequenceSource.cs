// -------------------------------------------------------
// File:    SequenceSource.cs
// Project: AM.WorkStation.Demo
// Purpose: Nạp + cache sequence JSON theo RECIPE đang active (P4.2) —
//          validate qua IStationResolver ngay lúc nạp; đổi recipe → invalidate + validate sớm
// -------------------------------------------------------

using System.IO;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Models.EventArgs;
using AM.Core.Sequencing;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Sequencing;

/// <summary>
/// Nguồn sequence của máy, chọn file theo recipe đang active (P4.2):
/// (1) <see cref="AM.Core.Models.RecipeBase.SequenceFile"/> nếu khai →
/// (2) convention <c>recipes/{RecipeName}.sequence.json</c> nếu file tồn tại →
/// (3) file mặc định của máy (config). Đổi recipe → cache tự invalidate + VALIDATE SỚM:
/// sequence của recipe mới hỏng thì alarm 60005 ngay lúc đổi (không đợi bấm Chạy).
/// Lỗi validate lúc Get() ném <see cref="SequenceValidationException"/> chứa TOÀN BỘ lỗi —
/// master controller chuyển thành alarm 60005 (máy vào InitAlarm, không chạy sequence hỏng).
/// </summary>
public sealed class SequenceSource
{
    private readonly string _defaultFilePath;
    private readonly IStationResolver _resolver;
    private readonly IRecipeService? _recipes;
    private readonly IAlarmService? _alarms;
    private readonly ILogger<SequenceSource> _logger;
    private readonly Lock _sync = new();
    private SequenceDefinition? _cached;
    private string? _cachedPath;

    /// <summary>Tạo nguồn sequence.</summary>
    /// <param name="defaultFilePath">File sequence mặc định của máy (fallback cuối).</param>
    /// <param name="resolver">Resolver kiểm tra tên station lúc nạp.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="recipes">Recipe service — chọn file theo recipe active + nghe RecipeChanged (null = luôn dùng file mặc định).</param>
    /// <param name="alarms">Alarm service — báo 60005 khi sequence của recipe mới hỏng (null = chỉ log).</param>
    public SequenceSource(string defaultFilePath, IStationResolver resolver, ILogger<SequenceSource> logger,
        IRecipeService? recipes = null, IAlarmService? alarms = null)
    {
        ArgumentNullException.ThrowIfNull(defaultFilePath);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(logger);
        _defaultFilePath = defaultFilePath;
        _resolver = resolver;
        _logger = logger;
        _recipes = recipes;
        _alarms = alarms;
        if (_recipes is not null)
            _recipes.RecipeChanged += OnRecipeChanged;
    }

    /// <summary>
    /// Lấy sequence đã validate cho recipe đang active (nạp lần đầu/khi đổi file, cache các lần sau).
    /// Ném <see cref="SequenceValidationException"/> khi nội dung không hợp lệ,
    /// <see cref="FileNotFoundException"/>/<see cref="IOException"/> khi thiếu file.
    /// </summary>
    public SequenceDefinition Get()
    {
        lock (_sync)
        {
            string path = ResolvePath();
            if (_cached is not null && string.Equals(_cachedPath, path, StringComparison.OrdinalIgnoreCase))
                return _cached;

            string json = File.ReadAllText(path);
            var result = SequenceLoader.Load(json, _resolver);
            foreach (string warning in result.Warnings)
                _logger.LogWarning("[Sequence] {Warning}", warning);
            if (!result.Success)
                throw new SequenceValidationException(result.Errors);

            _cached = result.Definition!;
            _cachedPath = path;
            _logger.LogInformation("[Sequence] Nạp '{Name}' v{Version} — {Steps} bước từ {File}",
                _cached.Name, _cached.Version, _cached.Steps.Count, path);
            return _cached;
        }
    }

    /// <summary>Xoá cache — lần Get() kế tiếp nạp lại theo recipe hiện tại.</summary>
    public void Invalidate()
    {
        lock (_sync)
        {
            _cached = null;
            _cachedPath = null;
        }
    }

    // Chọn file theo recipe active: khai tường minh → convention theo tên → mặc định của máy.
    // Gọi trong _sync.
    private string ResolvePath()
    {
        var recipe = _recipes?.ActiveRecipe;
        if (recipe is null) return _defaultFilePath;

        if (!string.IsNullOrWhiteSpace(recipe.SequenceFile))
        {
            // Đường dẫn tương đối neo theo thư mục app (cùng gốc với file mặc định)
            return Path.IsPathRooted(recipe.SequenceFile)
                ? recipe.SequenceFile
                : Path.Combine(AppContext.BaseDirectory, recipe.SequenceFile);
        }

        // Convention: cùng thư mục với file sequence mặc định (recipes/)
        string dir = Path.GetDirectoryName(_defaultFilePath) ?? ".";
        string conventionPath = Path.Combine(dir, $"{recipe.Name}.sequence.json");
        return File.Exists(conventionPath) ? conventionPath : _defaultFilePath;
    }

    // Đổi recipe: invalidate + VALIDATE SỚM — sequence hỏng báo 60005 ngay, không đợi bấm Chạy.
    private void OnRecipeChanged(object? sender, RecipeEventArgs e)
    {
        Invalidate();
        try
        {
            var seq = Get();
            _logger.LogInformation("[Sequence] Recipe đổi → sequence '{Name}' sẵn sàng", seq.Name);
        }
#pragma warning disable CA1031 // validate sớm: mọi lỗi (validate/thiếu file/IO) → alarm, không phá luồng đổi recipe
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Sequence] Sequence của recipe mới KHÔNG hợp lệ — máy sẽ không chạy được");
            RaiseSequenceAlarm(ex.Message);
        }
    }

    private void RaiseSequenceAlarm(string message)
    {
        if (_alarms is null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await _alarms.RaiseAsync(AlarmCodes.ProdSequenceInvalid, "SEQUENCE",
                    $"Sequence của recipe không hợp lệ: {message}").ConfigureAwait(false);
            }
#pragma warning disable CA1031 // alarm lỗi không được phá luồng đổi recipe — đã log lỗi gốc
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "[Sequence] Không raise được alarm 60005");
            }
        });
    }
}
