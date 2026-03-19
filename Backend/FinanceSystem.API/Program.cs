// Điểm khởi động chính của ASP.NET Core Web API
// Cấu hình đầy đủ: DI, JWT, Swagger, Serilog, Rate Limiting, SignalR, Hangfire
using System.Text;
using System.Threading.RateLimiting;
using FinanceSystem.API.Middleware;
using FinanceSystem.API.Services;
using FinanceSystem.Application.Common.Interfaces;
using FinanceSystem.Application.Extensions;
using FinanceSystem.Infrastructure.Extensions;
using FinanceSystem.Infrastructure.Persistence;
using FinanceSystem.Infrastructure.Services;
using Hangfire;
using Hangfire.AspNetCore;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// ─────────────────────────────────────────────
// 1. CẤU HÌNH SERILOG (phải làm trước WebApplication.CreateBuilder)
// ─────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger(); // Logger tạm thời trong khi khởi động

try
{
    Log.Information("Đang khởi động FinanceSystem API...");

    var builder = WebApplication.CreateBuilder(args);

    // Cấu hình Serilog đầy đủ (đọc từ appsettings)
    builder.Host.UseSerilog((ctx, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            // WithMachineName() cần Serilog.Enrichers.Environment - thay bằng property tĩnh
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/finance-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
    });

    // ─────────────────────────────────────────────
    // 2. ĐĂNG KÝ SERVICES
    // ─────────────────────────────────────────────

    // Application layer (MediatR, FluentValidation, Pipeline Behaviors)
    builder.Services.AddApplication();

    // Infrastructure layer (DbContext, JWT, Password, Excel, Notification)
    builder.Services.AddInfrastructure(builder.Configuration);

    // HttpContextAccessor (dùng bởi CurrentUserService)
    builder.Services.AddHttpContextAccessor();

    // Dịch vụ lấy thông tin user hiện tại từ JWT claims
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    // Controllers
    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            // Sử dụng camelCase cho JSON output
            opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            // Bỏ qua các giá trị null trong response
            opts.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    builder.Services.AddEndpointsApiExplorer();

    // ─────────────────────────────────────────────
    // 3. CẤU HÌNH JWT AUTHENTICATION
    // ─────────────────────────────────────────────
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Key");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                // Không cho phép sai lệch thời gian (ClockSkew = 0)
                ClockSkew = TimeSpan.Zero
            };

            // Xử lý lỗi xác thực - trả về tiếng Việt
            options.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/problem+json";
                    var response = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                        title = "Chưa xác thực",
                        status = 401,
                        detail = "Bạn cần đăng nhập để truy cập tài nguyên này.",
                        instance = context.Request.Path.ToString()
                    });
                    await context.Response.WriteAsync(response);
                },
                OnForbidden = async context =>
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/problem+json";
                    var response = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                        title = "Không có quyền truy cập",
                        status = 403,
                        detail = "Bạn không có quyền thực hiện hành động này.",
                        instance = context.Request.Path.ToString()
                    });
                    await context.Response.WriteAsync(response);
                }
            };
        });

    builder.Services.AddAuthorization();

    // ─────────────────────────────────────────────
    // 4. CẤU HÌNH SWAGGER / OPENAPI
    // ─────────────────────────────────────────────
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Finance System API",
            Version = "v1",
            Description = "API quản lý tài chính cá nhân - hỗ trợ JWT authentication, import Excel và báo cáo.",
            Contact = new OpenApiContact
            {
                Name = "Finance System Team",
                Email = "support@finance.com"
            }
        });

        // Thêm nút "Authorize" trong Swagger UI để test JWT
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Nhập JWT token. Ví dụ: Bearer {token}"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // ─────────────────────────────────────────────
    // 5. CẤU HÌNH RATE LIMITING (.NET 8 built-in)
    // ─────────────────────────────────────────────
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Policy cho auth endpoints: 5 request/phút per IP
        options.AddPolicy("auth-policy", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));

        // Policy chung cho API: 100 request/phút per user
        options.AddPolicy("api-policy", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 2
                }));

        // Callback khi bị từ chối vì rate limit
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = 429;
            context.HttpContext.Response.ContentType = "application/problem+json";
            await context.HttpContext.Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Quá nhiều yêu cầu",
                    status = 429,
                    detail = "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau ít phút."
                }), cancellationToken);
        };
    });

    // ─────────────────────────────────────────────
    // 6. SIGNALR (thông báo real-time)
    // ─────────────────────────────────────────────
    builder.Services.AddSignalR();

    // ─────────────────────────────────────────────
    // 7. HANGFIRE (background jobs)
    // ─────────────────────────────────────────────
    builder.Services.AddHangfire(config =>
    {
        config.UsePostgreSqlStorage(c =>
            c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));
    });
    builder.Services.AddHangfireServer();

    // ─────────────────────────────────────────────
    // 8. CORS (cho phép Blazor và WPF gọi API)
    // ─────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });

        // Policy riêng cho production
        options.AddPolicy("Production", policy =>
        {
            policy.WithOrigins(
                    builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                    ?? Array.Empty<string>())
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials(); // Cần cho SignalR
        });
    });

    // ─────────────────────────────────────────────
    // 9. XÂY DỰNG APP VÀ CẤU HÌNH MIDDLEWARE PIPELINE
    // ─────────────────────────────────────────────
    var app = builder.Build();

    // Tự động chạy migration khi khởi động (cho môi trường dev/docker)
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Log.Information("Đang áp dụng database migrations...");
            await dbContext.Database.MigrateAsync();
            Log.Information("Database migrations đã được áp dụng thành công.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Lỗi khi áp dụng database migrations.");
            throw;
        }
    }

    // Middleware xử lý lỗi toàn cục (phải đặt đầu tiên)
    app.UseMiddleware<GlobalExceptionMiddleware>();

    // Ghi log mọi HTTP request
    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0.0000}ms)";
    });

    // Swagger chỉ bật trong Development
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Finance System API v1");
            c.RoutePrefix = "swagger";
            c.DisplayRequestDuration(); // Hiển thị thời gian response
        });
    }

    app.UseHttpsRedirection();
    app.UseCors(app.Environment.IsDevelopment() ? "AllowAll" : "Production");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // Hangfire Dashboard (chỉ Admin mới truy cập)
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthFilter() }
    });

    app.MapControllers();

    // Map SignalR hub
    app.MapHub<ImportNotificationHub>("/hubs/import");

    Log.Information("FinanceSystem API đã khởi động thành công. Swagger: /swagger");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ứng dụng khởi động thất bại.");
    throw;
}
finally
{
    // Đảm bảo flush log trước khi tắt
    await Log.CloseAndFlushAsync();
}

// ─────────────────────────────────────────────
// HANGFIRE AUTH FILTER (chỉ Admin mới xem dashboard)
// ─────────────────────────────────────────────
public class HangfireAuthFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        // Try to get HttpContext from the dashboard context without depending on AspNetCoreDashboardContext type
        var httpContext = (context as dynamic)?.HttpContext as Microsoft.AspNetCore.Http.HttpContext;

        // Chỉ cho phép authenticated Admin users
        return httpContext?.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole("Admin");
    }
}
