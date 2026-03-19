// Thực thể lịch sử import và lỗi import
// Ghi lại kết quả từng lần nhập dữ liệu từ Excel
namespace FinanceSystem.Domain.Entities;

/// <summary>
/// Ghi lại thông tin mỗi lần import file Excel
/// </summary>
public class ImportHistory
{
    // Khóa chính
    public int Id { get; set; }

    // Khóa ngoại đến bảng Users (người thực hiện import)
    public int UserId { get; set; }

    // Tên file gốc được upload
    public string FileName { get; set; } = string.Empty;

    // Tổng số dòng trong file
    public int TotalRows { get; set; }

    // Số dòng import thành công
    public int SuccessCount { get; set; }

    // Số dòng có lỗi
    public int ErrorCount { get; set; }

    // Trạng thái: "Processing" | "Completed" | "Failed"
    public string Status { get; set; } = "Processing";

    // Thời điểm bắt đầu import
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    // Thời điểm hoàn thành import (null nếu đang xử lý)
    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<ImportError> Errors { get; set; } = new List<ImportError>();
}

/// <summary>
/// Ghi lại thông tin lỗi cho từng dòng không hợp lệ trong quá trình import
/// </summary>
public class ImportError
{
    // Khóa chính
    public int Id { get; set; }

    // Khóa ngoại đến bảng ImportHistories
    public int ImportHistoryId { get; set; }

    // Số thứ tự dòng trong file Excel (bắt đầu từ 2, dòng 1 là header)
    public int RowNumber { get; set; }

    // Lý do lỗi bằng tiếng Việt (hiển thị cho người dùng)
    public string ErrorMessage { get; set; } = string.Empty;

    // Dữ liệu gốc của dòng lỗi (JSON string để debug)
    public string? RawData { get; set; }

    // Navigation property
    public ImportHistory ImportHistory { get; set; } = null!;
}
