// Middleware xử lý ngoại lệ toàn cục
// Chuyển đổi tất cả exceptions thành response JSON chuẩn RFC 7807 (Problem Details)
// Tất cả thông báo lỗi trả về client bằng tiếng Việt
using System.Net;
using System.Text.Json;
using FinanceSystem.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace FinanceSystem.API.Middleware;

/// <summary>
/// Middleware bắt tất cả exceptions chưa được xử lý và trả về response chuẩn RFC 7807
/// Đảm bảo API không bao giờ trả về stack trace hay thông tin nhạy cảm
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không được xử lý: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var problemDetails = exception switch
        {
            // Lỗi validation dữ liệu → 422 Unprocessable Entity
            FinanceSystem.Domain.Exceptions.ValidationException validationEx => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                Title = "Dữ liệu không hợp lệ",
                Status = (int)HttpStatusCode.UnprocessableEntity,
                Detail = validationEx.Message,
                Extensions = { ["errors"] = validationEx.Errors }
            },

            // Không tìm thấy tài nguyên → 404 Not Found
            NotFoundException notFoundEx => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                Title = "Không tìm thấy",
                Status = (int)HttpStatusCode.NotFound,
                Detail = notFoundEx.Message
            },

            // Không có quyền → 403 Forbidden
            ForbiddenException => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                Title = "Không có quyền truy cập",
                Status = (int)HttpStatusCode.Forbidden,
                Detail = "Bạn không có quyền thực hiện hành động này."
            },

            // Lỗi xác thực → 401 Unauthorized
            UnauthorizedException unauthorizedEx => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                Title = "Chưa xác thực",
                Status = (int)HttpStatusCode.Unauthorized,
                Detail = unauthorizedEx.Message
            },

            // Lỗi nghiệp vụ → 400 Bad Request
            BusinessException businessEx => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Lỗi nghiệp vụ",
                Status = (int)HttpStatusCode.BadRequest,
                Detail = businessEx.Message
            },

            // Mọi lỗi khác → 500 Internal Server Error (không lộ chi tiết)
            _ => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Lỗi máy chủ",
                Status = (int)HttpStatusCode.InternalServerError,
                Detail = "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại sau hoặc liên hệ hỗ trợ."
            }
        };

        // Thêm request path để dễ debug
        problemDetails.Instance = context.Request.Path;

        context.Response.StatusCode = problemDetails.Status!.Value;

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await context.Response.WriteAsync(json);
    }
}
