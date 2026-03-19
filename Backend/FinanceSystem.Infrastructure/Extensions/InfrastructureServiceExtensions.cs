// Đăng ký tất cả dịch vụ của Infrastructure layer vào DI container
using FinanceSystem.Application.Common.Interfaces;
using FinanceSystem.Infrastructure.Identity;
using FinanceSystem.Infrastructure.Persistence;
using FinanceSystem.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceSystem.Infrastructure.Extensions;

/// <summary>
/// Extension method để đăng ký Infrastructure layer services
/// Gọi từ Program.cs: builder.Services.AddInfrastructure(configuration)
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Đăng ký DbContext với PostgreSQL
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    // Tự động retry khi kết nối bị gián đoạn (hữu ích với Docker)
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                });

            // Ghi log query SQL khi ở môi trường Development
            options.EnableSensitiveDataLogging(
                configuration.GetValue<bool>("Logging:EnableSensitiveData"));
        });

        // Đăng ký IAppDbContext → AppDbContext
        services.AddScoped<IAppDbContext>(provider =>
            provider.GetRequiredService<AppDbContext>());

        // Đăng ký các dịch vụ identity và bảo mật
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordService, PasswordService>();

        // Đăng ký dịch vụ đọc Excel
        services.AddScoped<IExcelService, ExcelService>();

        // Đăng ký dịch vụ tạo Excel template
        services.AddScoped<IExcelTemplateService, ExcelTemplateService>();

        // Đăng ký dịch vụ thông báo SignalR
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
