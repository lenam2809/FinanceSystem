// Xử lý Import Excel - logic nghiệp vụ cốt lõi
// Validate từng dòng, lưu giao dịch hợp lệ, ghi lỗi chi tiết
using FinanceSystem.Application.Common.Interfaces;
using FinanceSystem.Contracts.V1.Imports;
using FinanceSystem.Domain.Entities;
using FinanceSystem.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceSystem.Application.Imports.Commands;

/// <summary>
/// Command bắt đầu import file Excel
/// </summary>
public record ImportTransactionsCommand(
    Stream FileStream,
    string FileName,
    long FileSize,
    int UserId) : IRequest<ImportResultDto>;

/// <summary>
/// Kết quả xử lý từng dòng trong file Excel
/// </summary>
public class RowProcessResult
{
    public bool IsSuccess { get; set; }
    public int RowNumber { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RawData { get; set; }
    public Transaction? Transaction { get; set; }
}

/// <summary>
/// Handler chính xử lý import - validate và lưu từng dòng
/// </summary>
public class ImportTransactionsCommandHandler : IRequestHandler<ImportTransactionsCommand, ImportResultDto>
{
    private readonly IAppDbContext _db;
    private readonly IExcelService _excelService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ImportTransactionsCommandHandler> _logger;

    // Kích thước file tối đa: 10 MB
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public ImportTransactionsCommandHandler(
        IAppDbContext db,
        IExcelService excelService,
        INotificationService notificationService,
        ILogger<ImportTransactionsCommandHandler> logger)
    {
        _db = db;
        _excelService = excelService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ImportResultDto> Handle(ImportTransactionsCommand request, CancellationToken ct)
    {
        // Kiểm tra kích thước file
        if (request.FileSize > MaxFileSizeBytes)
            throw new BusinessException("Kích thước file vượt quá giới hạn 10 MB cho phép.");

        // Kiểm tra định dạng file
        if (!request.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("Chỉ chấp nhận file định dạng .xlsx.");

        // Tạo bản ghi lịch sử import
        var importHistory = new ImportHistory
        {
            UserId = request.UserId,
            FileName = request.FileName,
            Status = "Processing",
            StartedAt = DateTime.UtcNow
        };
        _db.ImportHistories.Add(importHistory);
        await _db.SaveChangesAsync(ct);

        try
        {
            // Đọc toàn bộ dữ liệu từ file Excel
            var rows = await _excelService.ReadExcelAsync(request.FileStream);
            importHistory.TotalRows = rows.Count;

            // Nạp danh mục từ DB để kiểm tra (không gọi DB trong vòng lặp)
            var categories = await _db.Categories
                .Where(c => c.IsActive)
                .ToDictionaryAsync(c => c.Name.ToLower().Trim(), c => c, ct);

            // Xử lý từng dòng
            var results = new List<RowProcessResult>();
            for (int i = 0; i < rows.Count; i++)
            {
                var result = ProcessRow(rows[i], i + 2, categories, request.UserId);
                results.Add(result);
            }

            // Lưu tất cả giao dịch hợp lệ trong một lần (bulk insert hiệu quả hơn)
            var validTransactions = results
                .Where(r => r.IsSuccess)
                .Select(r => r.Transaction!)
                .ToList();

            if (validTransactions.Any())
                await _db.Transactions.AddRangeAsync(validTransactions, ct);

            // Lưu tất cả lỗi
            var errors = results
                .Where(r => !r.IsSuccess)
                .Select(r => new ImportError
                {
                    ImportHistoryId = importHistory.Id,
                    RowNumber = r.RowNumber,
                    ErrorMessage = r.ErrorMessage!,
                    RawData = r.RawData
                })
                .ToList();

            if (errors.Any())
                await _db.ImportErrors.AddRangeAsync(errors, ct);

            // Cập nhật thống kê lịch sử import
            importHistory.SuccessCount = validTransactions.Count;
            importHistory.ErrorCount = errors.Count;
            importHistory.Status = "Completed";
            importHistory.CompletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Import hoàn thành: ImportId={Id}, Thành công={Success}, Lỗi={Error}",
                importHistory.Id, importHistory.SuccessCount, importHistory.ErrorCount);

            // Thông báo real-time qua SignalR
            await _notificationService.NotifyImportCompletedAsync(
                request.UserId, importHistory.Id,
                importHistory.SuccessCount, importHistory.ErrorCount);

            return new ImportResultDto
            {
                ImportId = importHistory.Id,
                Status = "Completed",
                TotalRows = importHistory.TotalRows,
                SuccessCount = importHistory.SuccessCount,
                ErrorCount = importHistory.ErrorCount,
                Message = $"Import hoàn tất: {importHistory.SuccessCount} dòng thành công, {importHistory.ErrorCount} dòng lỗi."
            };
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            // Ghi nhận lỗi hệ thống, cập nhật trạng thái import
            _logger.LogError(ex, "Lỗi khi xử lý import ImportId={Id}", importHistory.Id);
            importHistory.Status = "Failed";
            importHistory.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new BusinessException("Đã xảy ra lỗi trong quá trình xử lý file. Vui lòng thử lại.");
        }
    }

    /// <summary>
    /// Xử lý và validate một dòng dữ liệu từ Excel
    /// </summary>
    private static RowProcessResult ProcessRow(
        Dictionary<string, string> row,
        int rowNumber,
        Dictionary<string, Category> categories,
        int userId)
    {
        var rawData = System.Text.Json.JsonSerializer.Serialize(row);

        // Đọc giá trị các cột (linh hoạt với tên cột)
        var amountStr = GetValue(row, "Số tiền", "SoTien", "Amount");
        var dateStr = GetValue(row, "Ngày", "Ngay", "Date");
        var categoryStr = GetValue(row, "Danh mục", "DanhMuc", "Category");
        var description = GetValue(row, "Mô tả", "MoTa", "Description");

        var errors = new List<string>();

        // Validate Số tiền
        if (!decimal.TryParse(amountStr?.Replace(",", ""), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount))
            errors.Add("Số tiền không hợp lệ (phải là số).");
        else if (amount <= 0)
            errors.Add("Số tiền phải lớn hơn 0.");
        else if (decimal.Round(amount, 2) != amount)
            errors.Add("Số tiền tối đa 2 chữ số thập phân.");

        // Validate Ngày
        DateTime date = default;
        if (!DateTime.TryParse(dateStr, out date))
            errors.Add("Ngày không hợp lệ (định dạng: dd/MM/yyyy hoặc yyyy-MM-dd).");
        else if (date.Date > DateTime.Today)
            errors.Add("Ngày không được là ngày trong tương lai.");

        // Validate Danh mục
        Category? category = null;
        if (string.IsNullOrWhiteSpace(categoryStr))
            errors.Add("Danh mục không được để trống.");
        else if (!categories.TryGetValue(categoryStr.ToLower().Trim(), out category))
            errors.Add($"Danh mục '{categoryStr}' không tồn tại trong hệ thống.");

        if (errors.Any())
        {
            return new RowProcessResult
            {
                IsSuccess = false,
                RowNumber = rowNumber,
                ErrorMessage = string.Join(" | ", errors),
                RawData = rawData
            };
        }

        // Xác định loại giao dịch dựa vào danh mục (Lương = Thu nhập, còn lại = Chi tiêu)
        var transactionType = category!.Name.ToLower() == "lương" ? "Income" : "Expense";

        return new RowProcessResult
        {
            IsSuccess = true,
            RowNumber = rowNumber,
            Transaction = new Transaction
            {
                Amount = amount,
                Type = transactionType,
                CategoryId = category.Id,
                UserId = userId,
                Date = date.ToUniversalTime(),
                Description = description,
                CreatedAt = DateTime.UtcNow
            }
        };
    }

    // Lấy giá trị từ dictionary với nhiều tên cột khả năng
    private static string? GetValue(Dictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            var match = row.Keys.FirstOrDefault(k =>
                string.Equals(k.Trim(), key, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return row[match];
        }
        return null;
    }
}

