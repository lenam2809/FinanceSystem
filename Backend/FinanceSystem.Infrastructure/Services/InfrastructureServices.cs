// Các dịch vụ hạ tầng: mã hóa mật khẩu, đọc Excel, thông báo SignalR
using FinanceSystem.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;
using OfficeOpenXml;

namespace FinanceSystem.Infrastructure.Services;

/// <summary>
/// Dịch vụ mã hóa mật khẩu sử dụng BCrypt
/// BCrypt tự động thêm salt và có work factor chống brute-force
/// </summary>
public class PasswordService : IPasswordService
{
    // Work factor 12 = ~400ms/hash - cân bằng giữa bảo mật và hiệu năng
    private const int WorkFactor = 12;

    public string HashPassword(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool VerifyPassword(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);
}

/// <summary>
/// Dịch vụ đọc file Excel sử dụng EPPlus
/// Chuyển đổi các dòng Excel thành Dictionary[tên cột → giá trị]
/// </summary>
public class ExcelService : IExcelService
{
    public ExcelService()
    {
        // EPPlus yêu cầu cài đặt license (NonCommercial cho dự án không thương mại)
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <summary>
    /// Đọc file Excel và trả về danh sách các dòng dữ liệu
    /// Dòng đầu tiên là header, từ dòng 2 trở đi là dữ liệu
    /// </summary>
    public async Task<List<Dictionary<string, string>>> ReadExcelAsync(Stream fileStream)
    {
        var result = new List<Dictionary<string, string>>();

        using var package = new ExcelPackage();
        await package.LoadAsync(fileStream);

        // Lấy worksheet đầu tiên
        var worksheet = package.Workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("File Excel không có sheet nào.");

        if (worksheet.Dimension == null)
            return result; // File rỗng

        var rowCount = worksheet.Dimension.Rows;
        var colCount = worksheet.Dimension.Columns;

        if (rowCount < 2)
            return result; // Chỉ có header, không có dữ liệu

        // Đọc tên các cột từ dòng header (dòng 1)
        var headers = new List<string>();
        for (int col = 1; col <= colCount; col++)
        {
            var header = worksheet.Cells[1, col].Text?.Trim() ?? $"Column{col}";
            headers.Add(header);
        }

        // Đọc từng dòng dữ liệu (từ dòng 2)
        for (int row = 2; row <= rowCount; row++)
        {
            // Bỏ qua dòng hoàn toàn rỗng
            var isEmptyRow = true;
            for (int col = 1; col <= colCount; col++)
            {
                if (!string.IsNullOrWhiteSpace(worksheet.Cells[row, col].Text))
                {
                    isEmptyRow = false;
                    break;
                }
            }
            if (isEmptyRow) continue;

            var rowData = new Dictionary<string, string>();
            for (int col = 1; col <= colCount; col++)
            {
                var cellValue = worksheet.Cells[row, col].Text?.Trim() ?? string.Empty;
                rowData[headers[col - 1]] = cellValue;
            }
            result.Add(rowData);
        }

        return result;
    }
}

/// <summary>
/// Hub SignalR để đẩy thông báo real-time đến client khi import hoàn thành
/// </summary>
public class ImportNotificationHub : Hub
{
    // Client tự đăng ký vào group theo userId khi kết nối
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
    }
}

/// <summary>
/// Dịch vụ gửi thông báo real-time qua SignalR
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IHubContext<ImportNotificationHub> _hubContext;

    public NotificationService(IHubContext<ImportNotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Gửi thông báo tiến độ import đến user cụ thể
    /// </summary>
    public async Task NotifyImportProgressAsync(int userId, int importId, string status, int successCount, int errorCount)
    {
        await _hubContext.Clients
            .Group($"user_{userId}")
            .SendAsync("ImportProgress", new
            {
                importId,
                status,
                successCount,
                errorCount,
                message = $"Đang xử lý: {successCount} thành công, {errorCount} lỗi"
            });
    }

    /// <summary>
    /// Gửi thông báo import hoàn thành đến user
    /// </summary>
    public async Task NotifyImportCompletedAsync(int userId, int importId, int successCount, int errorCount)
    {
        await _hubContext.Clients
            .Group($"user_{userId}")
            .SendAsync("ImportCompleted", new
            {
                importId,
                successCount,
                errorCount,
                message = $"Import hoàn tất! {successCount} giao dịch được thêm thành công, {errorCount} dòng có lỗi."
            });
    }
}
