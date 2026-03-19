// Thực thể lưu trữ refresh token cho xác thực JWT
// Hỗ trợ token rotation và thu hồi token
namespace FinanceSystem.Domain.Entities;

/// <summary>
/// Lưu trữ refresh token phục vụ chu kỳ xác thực JWT
/// Mỗi token chỉ được dùng một lần (token rotation)
/// </summary>
public class RefreshToken
{
    // Khóa chính
    public int Id { get; set; }

    // Khóa ngoại đến bảng Users
    public int UserId { get; set; }

    // Giá trị token ngẫu nhiên, unique
    public string Token { get; set; } = string.Empty;

    // Thời điểm hết hạn của token
    public DateTime ExpiryDate { get; set; }

    // Đánh dấu token đã bị thu hồi hay chưa
    public bool IsRevoked { get; set; } = false;

    // Thời điểm tạo token
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Thời điểm token bị thu hồi (null nếu chưa bị thu hồi)
    public DateTime? RevokedAt { get; set; }

    // Token mới thay thế token này (dùng cho token rotation tracking)
    public string? ReplacedByToken { get; set; }

    // Navigation property
    public User User { get; set; } = null!;

    // Kiểm tra token có còn hiệu lực hay không
    public bool IsActive => !IsRevoked && DateTime.UtcNow < ExpiryDate;
}
