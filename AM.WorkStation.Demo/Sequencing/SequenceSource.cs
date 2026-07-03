// -------------------------------------------------------
// File:    SequenceSource.cs
// Project: AM.WorkStation.Demo
// Purpose: Nạp + cache sequence JSON của máy — validate qua IStationResolver NGAY LÚC NẠP
// -------------------------------------------------------

using System.IO;
using AM.Core.Sequencing;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Sequencing;

/// <summary>
/// Nguồn sequence của máy (gắn theo recipe — v1 một file cấu hình qua
/// <c>AutoMachine:Sequence:File</c>). Nạp lười + cache; lỗi validate ném
/// <see cref="SequenceValidationException"/> chứa TOÀN BỘ lỗi — master controller
/// chuyển thành alarm 60005 (máy vào InitAlarm, không chạy với sequence hỏng).
/// </summary>
public sealed class SequenceSource
{
    private readonly string _filePath;
    private readonly IStationResolver _resolver;
    private readonly ILogger<SequenceSource> _logger;
    private readonly Lock _sync = new();
    private SequenceDefinition? _cached;

    /// <summary>Tạo nguồn sequence.</summary>
    /// <param name="filePath">Đường dẫn file sequence JSON.</param>
    /// <param name="resolver">Resolver kiểm tra tên station lúc nạp.</param>
    /// <param name="logger">Logger.</param>
    public SequenceSource(string filePath, IStationResolver resolver, ILogger<SequenceSource> logger)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(logger);
        _filePath = filePath;
        _resolver = resolver;
        _logger = logger;
    }

    /// <summary>
    /// Lấy sequence đã validate (nạp lần đầu, cache các lần sau).
    /// Ném <see cref="SequenceValidationException"/> khi nội dung không hợp lệ,
    /// <see cref="FileNotFoundException"/>/<see cref="IOException"/> khi thiếu file.
    /// </summary>
    public SequenceDefinition Get()
    {
        lock (_sync)
        {
            if (_cached is not null) return _cached;

            string json = File.ReadAllText(_filePath);
            var result = SequenceLoader.Load(json, _resolver);
            foreach (string warning in result.Warnings)
                _logger.LogWarning("[Sequence] {Warning}", warning);
            if (!result.Success)
                throw new SequenceValidationException(result.Errors);

            _cached = result.Definition!;
            _logger.LogInformation("[Sequence] Nạp '{Name}' v{Version} — {Steps} bước từ {File}",
                _cached.Name, _cached.Version, _cached.Steps.Count, _filePath);
            return _cached;
        }
    }

    /// <summary>Xoá cache — gọi khi đổi file/recipe để nạp lại.</summary>
    public void Invalidate()
    {
        lock (_sync) { _cached = null; }
    }
}
