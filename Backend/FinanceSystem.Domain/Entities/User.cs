// Thực thể người dùng trong hệ thống
// Chứa thông tin tài khoản và vai trò
namespace FinanceSystem.Domain.Entities;

/// <summary>
/// Đại diện cho một tài khoản người dùng trong hệ thống
/// </summary>
public class User
{
    // Khóa chính
    public int Id { get; set; }

    // Địa chỉ email (dùng làm tên đăng nhập)
    public string Email { get; set; } = string.Empty;

    // Mật khẩu đã được mã hóa bằng BCrypt
    public string PasswordHash { get; set; } = string.Empty;

    // Họ và tên hiển thị
    public string FullName { get; set; } = string.Empty;

    // Vai trò: "Admin" hoặc "User"
    public string Role { get; set; } = "User";

    // Trạng thái tài khoản
    public bool IsActive { get; set; } = true;

    // Thời điểm tạo tài khoản
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Danh sách refresh tokens liên kết
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    // Danh sách giao dịch của người dùng
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    // Lịch sử import của người dùng
    public ICollection<ImportHistory> ImportHistories { get; set; } = new List<ImportHistory>();
}
