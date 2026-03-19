// ViewModel cửa sổ chính - quản lý tab Import và tab Lịch sử
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceSystem.WPF.Services;
using FinanceSystem.WPF.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace FinanceSystem.WPF.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    // Thông tin user hiển thị trên thanh nav
    [ObservableProperty]
    private string _userInfo = string.Empty;

    // Sub-ViewModels cho từng tab
    // Blazor tab dùng DataContext="{Binding ImportTab}" và DataContext="{Binding HistoryTab}"
    public ImportViewModel        ImportTab  { get; }
    public ImportHistoryViewModel HistoryTab { get; }

    public MainViewModel(
        IAuthService              authService,
        ImportViewModel           importViewModel,
        ImportHistoryViewModel    historyViewModel)
    {
        _authService = authService;
        ImportTab    = importViewModel;
        HistoryTab   = historyViewModel;

        UserInfo = $"{_authService.UserName}  ({_authService.Role})";
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();

        Application.Current.Dispatcher.Invoke(() =>
        {
            // Mở lại màn hình đăng nhập
            var loginWindow = App.Services.GetRequiredService<LoginWindow>();
            loginWindow.Show();
            // Đóng cửa sổ chính
            Application.Current.Windows
                .OfType<MainWindow>()
                .FirstOrDefault()
                ?.Close();
        });
    }
}
