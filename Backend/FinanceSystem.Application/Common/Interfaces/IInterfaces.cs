// Các interface cốt lõi của Application layer
// Định nghĩa hợp đồng mà Infrastructure phải implement
using FinanceSystem.Domain.Entities;

namespace FinanceSystem.Application.Common.Interfaces;

/// <summary>
/// Interface cho DbContext - giúp tách biệt Application khỏi EF Core cụ thể
/// Dùng để mock trong unit test
/// </summary>
public interface IAppDbContext
{
    Microsoft.EntityFrameworkCore.DbSet<User> Users { get; }
    Microsoft.EntityFrameworkCore.DbSet<RefreshToken> RefreshTokens { get; }
    Microsoft.EntityFrameworkCore.DbSet<Transaction> Transactions { get; }
    Microsoft.EntityFrameworkCore.DbSet<Category> Categories { get; }
    Microsoft.EntityFrameworkCore.DbSet<ImportHistory> ImportHistories { get; }
    Microsoft.EntityFrameworkCore.DbSet<ImportError> ImportErrors { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface cho dịch vụ JWT - tạo và xác thực token
/// </summary>
public interface IJwtService
{
    // Tạo access token từ thông tin user
    string GenerateAccessToken(User user);

    // Tạo refresh token ngẫu nhiên
    string GenerateRefreshToken();

    // Lấy userId từ expired access token (dùng khi refresh)
    int? GetUserIdFromExpiredToken(string token);
}

/// <summary>
/// Interface cho dịch vụ mã hóa mật khẩu
/// </summary>
public interface IPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}

/// <summary>
/// Interface cho dịch vụ đọc file Excel
/// </summary>
public interface IExcelService
{
    // Đọc file Excel và trả về danh sách các dòng dữ liệu thô
    Task<List<Dictionary<string, string>>> ReadExcelAsync(Stream fileStream);
}

/// <summary>
/// Interface cho dịch vụ thông báo real-time qua SignalR
/// </summary>
public interface INotificationService
{
    // Thông báo tiến độ import đến user cụ thể
    Task NotifyImportProgressAsync(int userId, int importId, string status, int successCount, int errorCount);

    // Thông báo import hoàn thành
    Task NotifyImportCompletedAsync(int userId, int importId, int successCount, int errorCount);
}

/// <summary>
/// Interface lấy thông tin user hiện tại từ HTTP context
/// </summary>
public interface ICurrentUserService
{
    int? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
}

/// <summary>
/// Interface cho dịch vụ tạo file Excel template
/// Định nghĩa ở Application layer để TemplateController không phụ thuộc Infrastructure
/// </summary>
public interface IExcelTemplateService
{
    byte[] GenerateImportTemplate(IEnumerable<string> categoryNames);
}
