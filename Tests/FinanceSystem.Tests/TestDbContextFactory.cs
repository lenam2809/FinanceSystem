// Helper để tạo DbContext InMemory cho unit tests
// Không bao giờ test trực tiếp với EF Core PostgreSQL thật
using FinanceSystem.Domain.Entities;
using FinanceSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceSystem.Tests;

/// <summary>
/// Factory tạo AppDbContext sử dụng InMemory database cho unit tests
/// Mỗi test nên dùng một database name khác nhau để tránh xung đột
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Tạo AppDbContext InMemory với tên database ngẫu nhiên (isolated per test)
    /// </summary>
    public static AppDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Tạo DbContext đã có sẵn dữ liệu mẫu để test
    /// </summary>
    public static AppDbContext CreateWithSeedData(string? dbName = null)
    {
        var context = Create(dbName);

        // Thêm danh mục mẫu
        context.Categories.AddRange(
            new Category { Id = 1, Name = "Ăn uống",   ColorHex = "#FF6B6B", IsActive = true },
            new Category { Id = 2, Name = "Di chuyển", ColorHex = "#4ECDC4", IsActive = true },
            new Category { Id = 3, Name = "Lương",     ColorHex = "#45B7D1", IsActive = true },
            new Category { Id = 4, Name = "Mua sắm",   ColorHex = "#96CEB4", IsActive = true }
        );

        // Thêm users mẫu
        context.Users.AddRange(
            new User
            {
                Id = 1,
                Email = "admin@finance.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                FullName = "Quản trị viên",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = 2,
                Email = "user@finance.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("User@123"),
                FullName = "Người dùng mẫu",
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        );

        context.SaveChanges();
        return context;
    }
}
