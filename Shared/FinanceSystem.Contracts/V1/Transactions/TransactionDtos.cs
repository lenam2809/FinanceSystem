// DTOs giao dịch tài chính - phiên bản V1
namespace FinanceSystem.Contracts.V1.Transactions;

/// <summary>
/// Thông tin giao dịch trả về cho client
/// </summary>
public class TransactionDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }

    // "Income" (thu) hoặc "Expense" (chi)
    public string Type { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryColorHex { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Yêu cầu tạo giao dịch mới
/// </summary>
public class CreateTransactionDto
{
    public decimal Amount { get; set; }
    public string Type { get; set; } = "Expense";
    public int CategoryId { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Yêu cầu cập nhật giao dịch
/// </summary>
public class UpdateTransactionDto
{
    public decimal Amount { get; set; }
    public string Type { get; set; } = "Expense";
    public int CategoryId { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Tham số lọc và phân trang cho danh sách giao dịch
/// </summary>
public class TransactionFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; } = "date";
    public bool SortDesc { get; set; } = true;

    // Lọc theo khoảng ngày
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    // Lọc theo danh mục
    public int? CategoryId { get; set; }

    // Lọc theo loại giao dịch
    public string? Type { get; set; }

    // Lọc theo khoảng số tiền
    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }

    // Tìm kiếm theo mô tả
    public string? SearchText { get; set; }
}

/// <summary>
/// Thống kê tổng hợp thu/chi
/// </summary>
public class TransactionSummaryDto
{
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal Balance { get; set; }
    public int TransactionCount { get; set; }
}
