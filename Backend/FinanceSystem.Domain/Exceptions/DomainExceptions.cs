// Các ngoại lệ tùy chỉnh của domain
// Tất cả message đều bằng tiếng Việt để hiển thị đúng cho người dùng
namespace FinanceSystem.Domain.Exceptions;

/// <summary>
/// Ngoại lệ cơ sở cho tất cả lỗi nghiệp vụ trong hệ thống
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

/// <summary>
/// Lỗi khi không tìm thấy tài nguyên yêu cầu
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string resourceName, object key)
        : base($"Không tìm thấy {resourceName} với mã '{key}'.") { }
}

/// <summary>
/// Lỗi xác thực dữ liệu nghiệp vụ
/// </summary>
public class ValidationException : DomainException
{
    // Danh sách lỗi chi tiết theo từng trường
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("Dữ liệu không hợp lệ. Vui lòng kiểm tra lại các trường được đánh dấu.")
    {
        Errors = errors;
    }
}

/// <summary>
/// Lỗi khi người dùng không có quyền thực hiện hành động
/// </summary>
public class ForbiddenException : DomainException
{
    public ForbiddenException()
        : base("Bạn không có quyền thực hiện hành động này.") { }
}

/// <summary>
/// Lỗi xác thực đăng nhập
/// </summary>
public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message = "Phiên đăng nhập không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại.")
        : base(message) { }
}

/// <summary>
/// Lỗi nghiệp vụ chung (không phải lỗi validate hay not found)
/// </summary>
public class BusinessException : DomainException
{
    public BusinessException(string message) : base(message) { }
}
