// Unit tests cho ImportTransactionsCommandHandler và GetImportHistoryQueryHandler
using FinanceSystem.Application.Common.Interfaces;
using FinanceSystem.Application.Imports.Commands;
using FinanceSystem.Application.Imports.Queries;
using FinanceSystem.Contracts.V1.Imports;
using FinanceSystem.Domain.Entities;
using FinanceSystem.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceSystem.Tests.Imports;

public class ImportTransactionsCommandHandlerTests
{
    private readonly Mock<IExcelService> _excelServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ILogger<ImportTransactionsCommandHandler>> _loggerMock;

    public ImportTransactionsCommandHandlerTests()
    {
        _excelServiceMock = new Mock<IExcelService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _loggerMock = new Mock<ILogger<ImportTransactionsCommandHandler>>();

        // Setup mặc định: notification không làm gì
        _notificationServiceMock
            .Setup(x => x.NotifyImportCompletedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
    }

    // ─── Test 1: Dữ liệu hợp lệ → import thành công ────────
    [Fact(DisplayName = "Import file hợp lệ → tất cả dòng thành công, không có lỗi")]
    public async Task Handle_AllValidRows_ReturnsAllSuccess()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        var validRows = new List<Dictionary<string, string>>
        {
            new() { ["Ngày"] = "2024-01-15", ["Số tiền"] = "150000", ["Danh mục"] = "Ăn uống",   ["Mô tả"] = "Bữa trưa" },
            new() { ["Ngày"] = "2024-01-16", ["Số tiền"] = "50000",  ["Danh mục"] = "Di chuyển", ["Mô tả"] = "Xe bus" },
            new() { ["Ngày"] = "2024-01-17", ["Số tiền"] = "5000000",["Danh mục"] = "Lương",     ["Mô tả"] = "Lương tháng 1" }
        };

        _excelServiceMock
            .Setup(x => x.ReadExcelAsync(It.IsAny<Stream>()))
            .ReturnsAsync(validRows);

        var handler = CreateHandler(db);
        var command = CreateCommand(fileName: "test.xlsx", fileSize: 1024);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("Completed");
        result.TotalRows.Should().Be(3);
        result.SuccessCount.Should().Be(3);
        result.ErrorCount.Should().Be(0);

        // Kiểm tra giao dịch được lưu vào DB
        db.Transactions.Should().HaveCount(3);
        db.ImportErrors.Should().BeEmpty();
    }

    // ─── Test 2: Dữ liệu không hợp lệ → lỗi validation ─────
    [Fact(DisplayName = "Import file với số tiền âm và ngày tương lai → ghi nhận lỗi")]
    public async Task Handle_InvalidRows_RecordsErrors()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        var invalidRows = new List<Dictionary<string, string>>
        {
            // Số tiền âm
            new() { ["Ngày"] = "2024-01-15", ["Số tiền"] = "-100", ["Danh mục"] = "Ăn uống", ["Mô tả"] = "Invalid" },
            // Ngày trong tương lai
            new() { ["Ngày"] = DateTime.Today.AddDays(10).ToString("yyyy-MM-dd"), ["Số tiền"] = "100000", ["Danh mục"] = "Ăn uống", ["Mô tả"] = "Future" },
            // Danh mục không tồn tại
            new() { ["Ngày"] = "2024-01-15", ["Số tiền"] = "50000", ["Danh mục"] = "DanhMucKhongTonTai", ["Mô tả"] = "Bad cat" }
        };

        _excelServiceMock
            .Setup(x => x.ReadExcelAsync(It.IsAny<Stream>()))
            .ReturnsAsync(invalidRows);

        var handler = CreateHandler(db);
        var command = CreateCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.SuccessCount.Should().Be(0);
        result.ErrorCount.Should().Be(3);
        db.Transactions.Should().BeEmpty("không có dòng hợp lệ nào");
        db.ImportErrors.Should().HaveCount(3);

        // Kiểm tra lỗi được ghi bằng tiếng Việt
        db.ImportErrors.Should().AllSatisfy(e => e.ErrorMessage.Should().NotBeNullOrEmpty());
    }

    // ─── Test 3: Mix hợp lệ và không hợp lệ ────────────────
    [Fact(DisplayName = "Import file hỗn hợp → lưu dòng hợp lệ, ghi lỗi dòng không hợp lệ")]
    public async Task Handle_MixedRows_SavesValidAndRecordsErrors()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        var mixedRows = new List<Dictionary<string, string>>
        {
            new() { ["Ngày"] = "2024-01-15", ["Số tiền"] = "150000", ["Danh mục"] = "Ăn uống",   ["Mô tả"] = "Hợp lệ 1" }, // OK
            new() { ["Ngày"] = "2024-01-16", ["Số tiền"] = "abc",    ["Danh mục"] = "Di chuyển", ["Mô tả"] = "Lỗi số" },    // LỖI
            new() { ["Ngày"] = "2024-01-17", ["Số tiền"] = "300000", ["Danh mục"] = "Mua sắm",   ["Mô tả"] = "Hợp lệ 2" }, // OK
            new() { ["Ngày"] = "không phải ngày", ["Số tiền"] = "100", ["Danh mục"] = "Ăn uống", ["Mô tả"] = "Lỗi ngày" }  // LỖI
        };

        _excelServiceMock
            .Setup(x => x.ReadExcelAsync(It.IsAny<Stream>()))
            .ReturnsAsync(mixedRows);

        var handler = CreateHandler(db);
        var command = CreateCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.SuccessCount.Should().Be(2);
        result.ErrorCount.Should().Be(2);
        db.Transactions.Should().HaveCount(2);
        db.ImportErrors.Should().HaveCount(2);
    }

    // ─── Test 4: File vượt quá 10MB → BusinessException ─────
    [Fact(DisplayName = "Upload file > 10MB → ném BusinessException")]
    public async Task Handle_FileTooLarge_ThrowsBusinessException()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        var handler = CreateHandler(db);
        var command = CreateCommand(fileSize: 11 * 1024 * 1024); // 11 MB

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<BusinessException>()
            .WithMessage("*10 MB*");
    }

    // ─── Test 5: File không phải .xlsx ──────────────────────
    [Fact(DisplayName = "Upload file .csv → ném BusinessException")]
    public async Task Handle_NonXlsxFile_ThrowsBusinessException()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        var handler = CreateHandler(db);
        var command = CreateCommand(fileName: "data.csv");

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<BusinessException>()
            .WithMessage("*.xlsx*");
    }

    // ─── Helpers ────────────────────────────────────────────
    private ImportTransactionsCommandHandler CreateHandler(Infrastructure.Persistence.AppDbContext db)
        => new(db, _excelServiceMock.Object, _notificationServiceMock.Object, _loggerMock.Object);

    private static ImportTransactionsCommand CreateCommand(
        string fileName = "test.xlsx",
        long fileSize = 1024,
        int userId = 2)
        => new(Stream.Null, fileName, fileSize, userId);
}

public class GetImportHistoryQueryHandlerTests
{
    // ─── Test 1: Trả về dữ liệu phân trang ──────────────────
    [Fact(DisplayName = "Lấy lịch sử import với dữ liệu → trả về đúng trang")]
    public async Task Handle_WithData_ReturnsPaginatedResult()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();

        // Thêm 5 bản ghi import
        for (int i = 1; i <= 5; i++)
        {
            db.ImportHistories.Add(new ImportHistory
            {
                UserId = 2,
                FileName = $"file_{i}.xlsx",
                TotalRows = 10,
                SuccessCount = 8,
                ErrorCount = 2,
                Status = "Completed",
                StartedAt = DateTime.UtcNow.AddDays(-i)
            });
        }
        await db.SaveChangesAsync();

        var handler = new GetImportHistoryQueryHandler(db);
        var query = new GetImportHistoryQuery(
            new ImportHistoryFilterDto { Page = 1, PageSize = 3 },
            UserId: 2, UserRole: "User");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(3);
        result.TotalItems.Should().Be(5);
        result.TotalPages.Should().Be(2);
        result.Page.Should().Be(1);
    }

    // ─── Test 2: Không có dữ liệu → trả về empty ────────────
    [Fact(DisplayName = "Lấy lịch sử import khi chưa có dữ liệu → trả về danh sách rỗng")]
    public async Task Handle_NoData_ReturnsEmptyResult()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        var handler = new GetImportHistoryQueryHandler(db);
        var query = new GetImportHistoryQuery(
            new ImportHistoryFilterDto { Page = 1, PageSize = 10 },
            UserId: 2, UserRole: "User");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
    }

    // ─── Test 3: Admin xem tất cả, User chỉ xem của mình ────
    [Fact(DisplayName = "Admin xem lịch sử → thấy tất cả users, User chỉ thấy của mình")]
    public async Task Handle_AdminSeesAll_UserSeesOwn()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        db.ImportHistories.AddRange(
            new ImportHistory { UserId = 1, FileName = "admin.xlsx", TotalRows = 5, Status = "Completed", StartedAt = DateTime.UtcNow },
            new ImportHistory { UserId = 2, FileName = "user.xlsx",  TotalRows = 3, Status = "Completed", StartedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var handler = new GetImportHistoryQueryHandler(db);

        // Truy vấn với quyền Admin
        var adminQuery = new GetImportHistoryQuery(
            new ImportHistoryFilterDto(), UserId: 1, UserRole: "Admin");
        var adminResult = await handler.Handle(adminQuery, CancellationToken.None);
        adminResult.TotalItems.Should().Be(2, "Admin thấy tất cả");

        // Truy vấn với quyền User
        var userQuery = new GetImportHistoryQuery(
            new ImportHistoryFilterDto(), UserId: 2, UserRole: "User");
        var userResult = await handler.Handle(userQuery, CancellationToken.None);
        userResult.TotalItems.Should().Be(1, "User chỉ thấy của mình");
        userResult.Items.First().FileName.Should().Be("user.xlsx");
    }
}
