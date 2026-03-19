// DTOs dùng chung toàn hệ thống - phiên bản V1
namespace FinanceSystem.Contracts.V1.Common;

/// <summary>
/// Kết quả phân trang chung - dùng cho mọi endpoint trả về danh sách
/// </summary>
public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

/// <summary>
/// Wrapper chuẩn cho mọi response từ API
/// Tuân theo RFC 7807 cho error responses
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }

    // Factory methods
    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message, IDictionary<string, string[]>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}

/// <summary>
/// Phản hồi lỗi chuẩn RFC 7807 (Problem Details)
/// </summary>
public class ProblemDetailsDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string? Instance { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
}

/// <summary>
/// Thông tin danh mục giao dịch
/// </summary>
public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ColorHex { get; set; } = string.Empty;
    public string? Icon { get; set; }
}
