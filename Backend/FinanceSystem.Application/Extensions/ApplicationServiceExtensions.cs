// Đăng ký tất cả dịch vụ của Application layer vào DI container
using FinanceSystem.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceSystem.Application.Extensions;

/// <summary>
/// Extension method để đăng ký Application layer services
/// Gọi từ Program.cs: builder.Services.AddApplication()
/// </summary>
public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationServiceExtensions).Assembly;

        // Đăng ký MediatR - tự động tìm tất cả handlers trong assembly
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
        });

        // Đăng ký tất cả FluentValidation validators trong assembly
        services.AddValidatorsFromAssembly(assembly);

        // Đăng ký pipeline behaviors theo thứ tự: Performance → Validation → Handler
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
