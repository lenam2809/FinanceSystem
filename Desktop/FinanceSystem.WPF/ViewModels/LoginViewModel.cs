// ViewModel màn hình đăng nhập
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceSystem.WPF.Services;
using FinanceSystem.WPF.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace FinanceSystem.WPF.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _email = string.Empty;

    // Password được set từ code-behind (PasswordBox_PasswordChanged)
    // KHÔNG dùng [ObservableProperty] để tránh lưu plain text trong binding
    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set
        {
            _password = value;
            LoginCommand.NotifyCanExecuteChanged();
        }
    }

    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool   _isLoading    = false;
    [ObservableProperty] private bool   _hasError      = false;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
#if DEBUG
        Email    = "admin@finance.com";
        Password = "Admin@123";
#endif
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        IsLoading = true;
        HasError  = false;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _authService.LoginAsync(Email, Password);
            if (result != null)
            {
                // Phải dispatch về UI thread khi thao tác với Window
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = App.Services.GetRequiredService<MainWindow>();
                    mainWindow.Show();
                    // Đóng cửa sổ đăng nhập hiện tại
                    Application.Current.Windows
                        .OfType<LoginWindow>()
                        .FirstOrDefault()
                        ?.Close();
                });
            }
        }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanLogin()
        => !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(Password)
        && !IsLoading;
}
