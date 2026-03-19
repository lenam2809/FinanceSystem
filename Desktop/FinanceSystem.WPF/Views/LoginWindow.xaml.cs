// Code-behind cho LoginWindow
// PasswordBox không hỗ trợ binding trực tiếp nên phải xử lý ở code-behind
using FinanceSystem.WPF.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace FinanceSystem.WPF.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        // Inject ViewModel qua DI
        DataContext = App.Services.GetRequiredService<LoginViewModel>();
    }

    /// <summary>
    /// Khi mật khẩu thay đổi, cập nhật vào ViewModel thủ công
    /// (PasswordBox.Password không hỗ trợ TwoWay binding vì lý do bảo mật)
    /// </summary>
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is PasswordBox pb)
            vm.Password = pb.Password;
    }
}
