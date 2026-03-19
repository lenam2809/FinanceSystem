// Thực thể danh mục giao dịch (Ăn uống, Di chuyển, Lương, Mua sắm, v.v.)
namespace FinanceSystem.Domain.Entities;

/// <summary>
/// Danh mục để phân loại giao dịch tài chính
/// </summary>
public class Category
{
    // Khóa chính
    public int Id { get; set; }

    // Tên danh mục (ví dụ: "Ăn uống", "Di chuyển")
    public string Name { get; set; } = string.Empty;

    // Mô tả ngắn về danh mục
    public string? Description { get; set; }

    // Màu sắc hiển thị trên biểu đồ (hex color, ví dụ: "#FF5733")
    public string ColorHex { get; set; } = "#000000";

    // Biểu tượng (emoji hoặc icon name)
    public string? Icon { get; set; }

    // Trạng thái hoạt động
    public bool IsActive { get; set; } = true;

    // Danh sách giao dịch thuộc danh mục này
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
