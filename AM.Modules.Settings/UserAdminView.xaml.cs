// -------------------------------------------------------
// File:    UserAdminView.xaml.cs
// Project: AM.Modules.Settings
// Purpose: Code-behind màn quản trị user — đọc PasswordBox (không bind plaintext) đẩy vào VM khi bấm.
// -------------------------------------------------------

using System.Windows;
using System.Windows.Controls;

namespace AM.Modules.Settings;

/// <summary>
/// View quản trị người dùng. PasswordBox.Password không bind được (bảo mật) → code-behind đọc rồi gọi VM
/// (mẫu IdentityView login). Logic nghiệp vụ + audit ở <see cref="UserAdminViewModel"/>.
/// </summary>
public partial class UserAdminView : UserControl
{
    /// <summary>Khởi tạo view.</summary>
    public UserAdminView() => InitializeComponent();

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not UserAdminViewModel vm) return;
        string pwd = NewPasswordBox.Password;
        NewPasswordBox.Clear();
        await vm.CreateAsync(pwd).ConfigureAwait(true);
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not UserAdminViewModel vm) return;
        string pwd = ResetPasswordBox.Password;
        ResetPasswordBox.Clear();
        await vm.ResetSelectedAsync(pwd).ConfigureAwait(true);
    }
}
