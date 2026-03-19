// Code-behind cho MainWindow
using FinanceSystem.WPF.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace FinanceSystem.WPF.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Inject MainViewModel qua DI
        DataContext = App.Services.GetRequiredService<MainViewModel>();
    }

    /// <summary>
    /// Khi chọn tab Lịch sử → tự động tải dữ liệu
    /// </summary>
    private async void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is TabControl tc && tc.SelectedIndex == 1)
            await vm.HistoryTab.LoadAsync();
    }
}
