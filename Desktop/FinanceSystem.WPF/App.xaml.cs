// Điểm khởi động WPF Application
// Cấu hình Dependency Injection và hiển thị màn hình đăng nhập
using FinanceSystem.WPF.Services;
using FinanceSystem.WPF.ViewModels;
using FinanceSystem.WPF.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace FinanceSystem.WPF;

public partial class App : Application
{
    // DI container dùng chung toàn ứng dụng
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Hiển thị màn hình đăng nhập
        var loginWindow = Services.GetRequiredService<LoginWindow>();
        loginWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // HttpClient factory cho API
        services.AddHttpClient("FinanceApi", client =>
        {
            client.BaseAddress = new Uri("http://localhost:5000/");
            client.Timeout     = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // ── Services ──
        // AuthService là Singleton: giữ token suốt vòng đời app
        services.AddSingleton<IAuthService, AuthService>();
        // Import/Transaction/Template dùng Transient (WPF không có Scoped)
        services.AddTransient<IImportService, ImportService>();
        services.AddTransient<ITransactionService, TransactionService>();
        services.AddTransient<ITemplateService, TemplateService>();

        // ── ViewModels ──
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ImportViewModel>();
        services.AddTransient<ImportHistoryViewModel>();
        // MainViewModel phụ thuộc ImportViewModel + ImportHistoryViewModel → Transient
        services.AddTransient<MainViewModel>();

        // ── Views ──
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
    }
}
