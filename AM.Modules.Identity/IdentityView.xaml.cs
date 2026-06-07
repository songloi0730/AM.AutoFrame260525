// -------------------------------------------------------
// File:    IdentityView.xaml.cs
// Project: AM.Modules.Identity
// Purpose: Code-behind IdentityView — đọc mật khẩu từ PasswordBox, gọi LoginCommand (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;
using System.Windows.Input;
using AM.Core.Attributes;

namespace AM.Modules.Identity;

/// <summary>
/// View đăng nhập/đăng xuất. Logic ở <see cref="IdentityViewModel"/>.
/// Mật khẩu KHÔNG bind (PasswordBox.Password không phải DP) — đọc tại đây rồi truyền vào LoginCommand.
/// </summary>
[ModuleNavigation("Nav.Identity", icon: "user", order: 90)]
public partial class IdentityView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public IdentityView()
    {
        InitializeComponent();
    }

    private void OnLoginClick(object sender, System.Windows.RoutedEventArgs e) => TryLogin();

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryLogin();
    }

    private void TryLogin()
    {
        if (DataContext is not IdentityViewModel vm) return;
        string password = PasswordField.Password;
        if (vm.LoginCommand.CanExecute(password))
            vm.LoginCommand.Execute(password);
        PasswordField.Clear();
    }
}
