// Thực thể giao dịch tài chính
// Là trung tâm của toàn bộ hệ thống Personal Finance
namespace FinanceSystem.Domain.Entities;

/// <summary>
/// Đại diện cho một giao dịch thu/chi trong hệ thống
/// </summary>
public class Transaction
{
    // Khóa chính
    public int Id { get; set; }

    // Số tiền giao dịch (luôn dương, chiều được xác định bởi Type)
    public decimal Amount { get; set; }

    // Loại giao dịch: "Income" (thu) hoặc "Expense" (chi)
    public string Type { get; set; } = "Expense";

    // Khóa ngoại đến bảng Categories
    public int CategoryId { get; set; }

    // Khóa ngoại đến bảng Users
    public int UserId { get; set; }

    // Ngày thực hiện giao dịch
    public DateTime Date { get; set; }

    // Mô tả giao dịch (tùy chọn)
    public string? Description { get; set; }

    // Thời điểm tạo bản ghi
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Thời điểm cập nhật lần cuối
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Category Category { get; set; } = null!;
    public User User { get; set; } = null!;
}
