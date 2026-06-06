// -------------------------------------------------------
// File:    NavigationBuilder.cs
// Project: AM.Application.Shell
// Purpose: Tự sinh menu sidebar từ [ModuleNavigation] trên các View — thêm module không sửa Shell.
// -------------------------------------------------------

using System.Reflection;
using System.Windows.Controls;
using AM.Core.Attributes;

namespace AM.Application.Shell.Navigation;

/// <summary>Một mục điều hướng: key i18n + icon + thứ tự + kiểu View.</summary>
/// <param name="DisplayKey">Key i18n hiển thị trên menu.</param>
/// <param name="Icon">Tên icon.</param>
/// <param name="Order">Thứ tự (nhỏ hơn đứng trước).</param>
/// <param name="ViewType">Kiểu UserControl của module.</param>
internal sealed record NavigationEntry(string DisplayKey, string Icon, int Order, Type ViewType);

/// <summary>
/// Quét các assembly <c>AM.Modules.*</c> đã nạp, tìm View gắn <see cref="ModuleNavigationAttribute"/>,
/// trả về danh sách mục điều hướng đã sắp theo Order.
/// </summary>
internal static class NavigationBuilder
{
    public static IReadOnlyList<NavigationEntry> Discover()
    {
        var entries = new List<NavigationEntry>();

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("AM.Modules", StringComparison.Ordinal) == true))
        {
            foreach (var type in SafeGetTypes(asm))
            {
                var attr = type.GetCustomAttribute<ModuleNavigationAttribute>();
                if (attr is not null && typeof(UserControl).IsAssignableFrom(type))
                    entries.Add(new NavigationEntry(attr.DisplayName, attr.Icon, attr.Order, type));
            }
        }

        return entries.OrderBy(e => e.Order).ToList();
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }
}
