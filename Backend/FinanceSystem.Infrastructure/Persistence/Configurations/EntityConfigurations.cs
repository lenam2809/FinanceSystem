// Cấu hình chi tiết cho từng entity trong EF Core
// Định nghĩa constraints, indexes, relationships
using FinanceSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceSystem.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình bảng Users
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("User");

        // Index unique trên Email
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        // Quan hệ 1-nhiều với RefreshTokens
        builder.HasMany(u => u.RefreshTokens)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Quan hệ 1-nhiều với Transactions
        builder.HasMany(u => u.Transactions)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Cấu hình bảng RefreshTokens
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(t => t.ReplacedByToken)
            .HasMaxLength(512);

        // Index unique trên Token để tra cứu nhanh
        builder.HasIndex(t => t.Token)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_Token");

        // Index trên UserId để query token theo user nhanh hơn
        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("IX_RefreshTokens_UserId");
    }
}

/// <summary>
/// Cấu hình bảng Transactions
/// </summary>
public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Amount)
            .IsRequired()
            .HasPrecision(18, 2); // Hỗ trợ số tiền lớn, tối đa 2 chữ số thập phân

        builder.Property(t => t.Type)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        // Index để query theo ngày và user thường xuyên
        builder.HasIndex(t => new { t.UserId, t.Date })
            .HasDatabaseName("IX_Transactions_UserId_Date");

        builder.HasIndex(t => t.CategoryId)
            .HasDatabaseName("IX_Transactions_CategoryId");

        // Quan hệ với Category
        builder.HasOne(t => t.Category)
            .WithMany(c => c.Transactions)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Cấu hình bảng Categories
/// </summary>
public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.ColorHex)
            .HasMaxLength(10)
            .HasDefaultValue("#000000");

        builder.Property(c => c.Icon)
            .HasMaxLength(10);

        builder.Property(c => c.Description)
            .HasMaxLength(300);
    }
}

/// <summary>
/// Cấu hình bảng ImportHistories
/// </summary>
public class ImportHistoryConfiguration : IEntityTypeConfiguration<ImportHistory>
{
    public void Configure(EntityTypeBuilder<ImportHistory> builder)
    {
        builder.ToTable("ImportHistories");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(h => h.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(h => new { h.UserId, h.StartedAt })
            .HasDatabaseName("IX_ImportHistories_UserId_StartedAt");

        // Quan hệ 1-nhiều với ImportErrors
        builder.HasMany(h => h.Errors)
            .WithOne(e => e.ImportHistory)
            .HasForeignKey(e => e.ImportHistoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Cấu hình bảng ImportErrors
/// </summary>
public class ImportErrorConfiguration : IEntityTypeConfiguration<ImportError>
{
    public void Configure(EntityTypeBuilder<ImportError> builder)
    {
        builder.ToTable("ImportErrors");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ErrorMessage)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.RawData)
            .HasMaxLength(2000);
    }
}
