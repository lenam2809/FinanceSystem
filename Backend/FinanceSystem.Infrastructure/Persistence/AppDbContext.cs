// DbContext chính của ứng dụng - sử dụng Entity Framework Core với PostgreSQL
// Cấu hình entity mappings, seeding và audit fields
using FinanceSystem.Application.Common.Interfaces;
using FinanceSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceSystem.Infrastructure.Persistence;

/// <summary>
/// DbContext chính - quản lý tất cả bảng trong cơ sở dữ liệu
/// Implements IAppDbContext để Application layer không phụ thuộc trực tiếp vào EF Core
/// </summary>
public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Khai báo DbSet cho từng entity
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ImportHistory> ImportHistories => Set<ImportHistory>();
    public DbSet<ImportError> ImportErrors => Set<ImportError>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Áp dụng tất cả IEntityTypeConfiguration trong assembly này
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ─── SEEDING DỮ LIỆU MẪU ────────────────────────

        // Danh mục mặc định
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Ăn uống",    ColorHex = "#FF6B6B", Icon = "🍜", IsActive = true },
            new Category { Id = 2, Name = "Di chuyển",  ColorHex = "#4ECDC4", Icon = "🚗", IsActive = true },
            new Category { Id = 3, Name = "Lương",      ColorHex = "#45B7D1", Icon = "💰", IsActive = true },
            new Category { Id = 4, Name = "Mua sắm",    ColorHex = "#96CEB4", Icon = "🛍️", IsActive = true },
            new Category { Id = 5, Name = "Giải trí",   ColorHex = "#FFEAA7", Icon = "🎮", IsActive = true },
            new Category { Id = 6, Name = "Y tế",       ColorHex = "#DDA0DD", Icon = "🏥", IsActive = true },
            new Category { Id = 7, Name = "Giáo dục",   ColorHex = "#98D8C8", Icon = "📚", IsActive = true },
            new Category { Id = 8, Name = "Tiết kiệm",  ColorHex = "#F7DC6F", Icon = "🏦", IsActive = true }
        );

        // Tài khoản người dùng mẫu
        // QUAN TRỌNG: Không được gọi BCrypt.HashPassword() trong OnModelCreating
        // vì EF Core migration chạy lúc design-time → BCrypt tạo salt ngẫu nhiên mỗi lần
        // → migration file thay đổi mỗi lần add-migration → không deterministic
        // Giải pháp: dùng hash đã tính sẵn (pre-computed)
        //   Admin@123 → hash bên dưới
        //   User@123  → hash bên dưới
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Email = "admin@finance.com",
                PasswordHash = "$2a$12$tZ7KWsYT4ok3SzQ4o6n1O.ONy/gQEM/.DaLGoLCrrOd7TNfkSHZaC",
                FullName = "Quản trị viên",
                Role = "Admin",
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = 2,
                Email = "user@finance.com",
                PasswordHash = "$2a$12$E4Q6VCvI6xh/TSTJ6RVH2OTyNz1r3DJ5h2rPN18czoM2VcNxY7JQa",
                FullName = "Người dùng mẫu",
                Role = "User",
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
