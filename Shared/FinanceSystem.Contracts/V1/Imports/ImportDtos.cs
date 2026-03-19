// DTOs cho hệ thống import Excel - phiên bản V1
namespace FinanceSystem.Contracts.V1.Imports;

/// <summary>
/// Một dòng dữ liệu trong file Excel import
/// </summary>
public class TransactionImportItemDto
{
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Payload gửi lên khi import (nếu dùng JSON thay vì multipart)
/// </summary>
public class ImportDto
{
    public List<TransactionImportItemDto> Items { get; set; } = new();
}

/// <summary>
/// Kết quả trả về ngay sau khi upload file (sync hoặc async)
/// </summary>
public class ImportResultDto
{
    // ID của lần import (dùng để truy vấn trạng thái nếu async)
    public int ImportId { get; set; }

    // Trạng thái: "Processing" | "Completed" | "Failed"
    public string Status { get; set; } = string.Empty;

    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }

    // Thông báo tổng kết bằng tiếng Việt
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Thông tin một lần import trong lịch sử
/// </summary>
public class ImportHistoryDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string UserEmail { get; set; } = string.Empty;
}

/// <summary>
/// Thông tin một lỗi trong quá trình import
/// </summary>
public class ImportErrorDto
{
    public int RowNumber { get; set; }

    // Lý do lỗi bằng tiếng Việt
    public string ErrorMessage { get; set; } = string.Empty;

    // Dữ liệu gốc của dòng lỗi
    public string? RawData { get; set; }
}

/// <summary>
/// Tham số phân trang cho lịch sử import
/// </summary>
public class ImportHistoryFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; } = "startedAt";
    public bool SortDesc { get; set; } = true;
}
