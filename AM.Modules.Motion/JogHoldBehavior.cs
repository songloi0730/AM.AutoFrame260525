// -------------------------------------------------------
// File:    JogHoldBehavior.cs
// Project: AM.Modules.Motion
// Purpose: Attached behavior GIỮ-ĐỂ-CHẠY cho nút jog pad (P1.5) — nhấn = DownCommand,
//          nhả/chuột rời nút/mất capture = UpCommand (deadman phía UI)
// -------------------------------------------------------

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AM.Modules.Motion;

/// <summary>
/// Attached behavior cho nút jog giữ-để-chạy: <c>PreviewMouseLeftButtonDown</c> →
/// <see cref="DownCommandProperty"/>; nhả chuột / con trỏ RỜI nút / mất capture →
/// <see cref="UpCommandProperty"/> (mọi đường thoát đều dừng — deadman phía UI;
/// deadman thật nằm ở HAL watchdog 200ms). Touch dùng qua mouse-promotion của WPF.
/// </summary>
public static class JogHold
{
    /// <summary>Command khi nhấn giữ (tham số = <see cref="ParameterProperty"/>).</summary>
    public static readonly DependencyProperty DownCommandProperty =
        DependencyProperty.RegisterAttached("DownCommand", typeof(ICommand), typeof(JogHold),
            new PropertyMetadata(null, OnDownCommandChanged));

    /// <summary>Command khi nhả/thoát (tham số = <see cref="ParameterProperty"/>).</summary>
    public static readonly DependencyProperty UpCommandProperty =
        DependencyProperty.RegisterAttached("UpCommand", typeof(ICommand), typeof(JogHold),
            new PropertyMetadata(null));

    /// <summary>Tham số chung cho cả Down/Up (thường là AxisVm của slot).</summary>
    public static readonly DependencyProperty ParameterProperty =
        DependencyProperty.RegisterAttached("Parameter", typeof(object), typeof(JogHold),
            new PropertyMetadata(null));

    // Cờ đang-giữ per-element: Up chỉ bắn nếu Down đã bắn (tránh Up mồ côi khi rê chuột ngang qua)
    private static readonly DependencyProperty IsHeldProperty =
        DependencyProperty.RegisterAttached("IsHeld", typeof(bool), typeof(JogHold),
            new PropertyMetadata(false));

    /// <summary>Setter XAML.</summary>
    public static void SetDownCommand(DependencyObject element, ICommand? value)
        => element.SetValue(DownCommandProperty, value);

    /// <summary>Getter XAML.</summary>
    public static ICommand? GetDownCommand(DependencyObject element)
        => (ICommand?)element.GetValue(DownCommandProperty);

    /// <summary>Setter XAML.</summary>
    public static void SetUpCommand(DependencyObject element, ICommand? value)
        => element.SetValue(UpCommandProperty, value);

    /// <summary>Getter XAML.</summary>
    public static ICommand? GetUpCommand(DependencyObject element)
        => (ICommand?)element.GetValue(UpCommandProperty);

    /// <summary>Setter XAML.</summary>
    public static void SetParameter(DependencyObject element, object? value)
        => element.SetValue(ParameterProperty, value);

    /// <summary>Getter XAML.</summary>
    public static object? GetParameter(DependencyObject element)
        => element.GetValue(ParameterProperty);

    private static void OnDownCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Button button) return;
        if (e.OldValue is not null)
        {
            button.PreviewMouseLeftButtonDown -= OnDown;
            button.PreviewMouseLeftButtonUp -= OnUp;
            button.MouseLeave -= OnUp;
            button.LostMouseCapture -= OnUp;
        }
        if (e.NewValue is not null)
        {
            button.PreviewMouseLeftButtonDown += OnDown;
            button.PreviewMouseLeftButtonUp += OnUp;
            button.MouseLeave += OnUp;      // con trỏ rời nút khi đang giữ → dừng
            button.LostMouseCapture += OnUp; // mất capture (Alt-Tab, popup...) → dừng
        }
    }

    private static void OnDown(object sender, MouseButtonEventArgs e)
    {
        var button = (Button)sender;
        object? param = GetParameter(button);
        var cmd = GetDownCommand(button);
        if (cmd is null || !cmd.CanExecute(param)) return;

        button.SetValue(IsHeldProperty, true);
        button.CaptureMouse();
        cmd.Execute(param);
        e.Handled = true; // không cho Click cũ bắn kèm
    }

    private static void OnUp(object sender, EventArgs e)
    {
        var button = (Button)sender;
        if (!(bool)button.GetValue(IsHeldProperty)) return; // chưa từng Down → bỏ qua

        button.SetValue(IsHeldProperty, false);
        if (button.IsMouseCaptured) button.ReleaseMouseCapture();
        var cmd = GetUpCommand(button);
        object? param = GetParameter(button);
        if (cmd is not null && cmd.CanExecute(param)) cmd.Execute(param);
    }
}
