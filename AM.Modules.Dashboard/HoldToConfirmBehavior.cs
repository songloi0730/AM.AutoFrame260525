// -------------------------------------------------------
// File:    HoldToConfirmBehavior.cs
// Project: AM.Modules.Dashboard
// Purpose: Attached behavior "giữ-để-xác-nhận" — nút R1 phải GIỮ đủ DurationMs mới chạy Command (nhả sớm = huỷ).
// -------------------------------------------------------

using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace AM.Modules.Dashboard;

/// <summary>
/// Behavior gắn vào <see cref="ButtonBase"/>: nếu <c>DurationMs &gt; 0</c> thì chặn click thường và yêu cầu
/// GIỮ chuột đủ <c>DurationMs</c> mới thực thi <see cref="ButtonBase.Command"/> (nhả/rời sớm → huỷ). Dùng cho
/// thao tác có hậu quả (cửa R1) — xem docs/design-notes/0004. <c>DurationMs = 0</c> → click bình thường.
/// </summary>
public static class HoldToConfirm
{
    /// <summary>Thời gian phải giữ (ms); 0 = click thường.</summary>
    public static readonly DependencyProperty DurationMsProperty = DependencyProperty.RegisterAttached(
        "DurationMs", typeof(int), typeof(HoldToConfirm), new PropertyMetadata(0, OnDurationChanged));

    /// <summary>Getter attached prop DurationMs.</summary>
    public static int GetDurationMs(DependencyObject obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return (int)obj.GetValue(DurationMsProperty);
    }

    /// <summary>Setter attached prop DurationMs.</summary>
    public static void SetDurationMs(DependencyObject obj, int value)
    {
        ArgumentNullException.ThrowIfNull(obj);
        obj.SetValue(DurationMsProperty, value);
    }

    // Timer đang chạy của từng nút (lưu kèm nút).
    private static readonly DependencyProperty TimerProperty = DependencyProperty.RegisterAttached(
        "Timer", typeof(DispatcherTimer), typeof(HoldToConfirm), new PropertyMetadata(null));

    private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ButtonBase btn) return;
        btn.PreviewMouseLeftButtonDown -= OnDown;
        btn.PreviewMouseLeftButtonUp -= OnUp;
        btn.MouseLeave -= OnLeave;
        if (e.NewValue is int ms && ms > 0)
        {
            btn.PreviewMouseLeftButtonDown += OnDown;
            btn.PreviewMouseLeftButtonUp += OnUp;
            btn.MouseLeave += OnLeave;
        }
    }

    private static void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ButtonBase btn) return;
        int ms = GetDurationMs(btn);
        if (ms <= 0 || !btn.IsEnabled) return;
        e.Handled = true; // chặn click thường — chỉ chạy khi giữ đủ
        Stop(btn);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) =>
        {
            Stop(btn);
            if (btn.Command is { } cmd && cmd.CanExecute(btn.CommandParameter))
                cmd.Execute(btn.CommandParameter);
        };
        btn.SetValue(TimerProperty, timer);
        timer.Start();
    }

    private static void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ButtonBase btn) Stop(btn);
    }

    private static void OnLeave(object sender, MouseEventArgs e)
    {
        if (sender is ButtonBase btn) Stop(btn);
    }

    private static void Stop(ButtonBase btn)
    {
        if (btn.GetValue(TimerProperty) is DispatcherTimer t)
        {
            t.Stop();
            btn.SetValue(TimerProperty, null);
        }
    }
}
