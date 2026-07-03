// -------------------------------------------------------
// File:    KeyedStationResolver.cs
// Project: AM.Application.Shell
// Purpose: IStationResolver trên keyed DI của composition root (ADR 0011 §2) —
//          engine/station không thấy container
// -------------------------------------------------------

using AM.Core.Sequencing;
using Microsoft.Extensions.DependencyInjection;

namespace AM.Application.Shell.Sequencing;

/// <summary>
/// Resolve <see cref="AM.Core.Sequencing.IStation"/> theo tên logic từ keyed service
/// của container (đăng ký ở ServiceCollectionExtensions). Danh sách tên khai tường minh
/// lúc đăng ký — validator dùng để bắt tên sai NGAY LÚC NẠP sequence.
/// </summary>
internal sealed class KeyedStationResolver : IStationResolver
{
    private readonly IServiceProvider _services;
    private readonly IReadOnlyList<string> _names;

    public KeyedStationResolver(IServiceProvider services, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(names);
        _services = services;
        _names = names;
    }

    public bool Contains(string name) => _names.Contains(name, StringComparer.Ordinal);

    public AM.Core.Sequencing.IStation Resolve(string name)
        => _services.GetRequiredKeyedService<AM.Core.Sequencing.IStation>(name);

    public IReadOnlyList<string> AllNames() => _names;
}
