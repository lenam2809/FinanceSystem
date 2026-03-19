// Commands và Queries cho quản lý giao dịch tài chính
using FinanceSystem.Application.Common.Interfaces;
using FinanceSystem.Contracts.V1.Common;
using FinanceSystem.Contracts.V1.Transactions;
using FinanceSystem.Domain.Common;
using FinanceSystem.Domain.Entities;
using FinanceSystem.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceSystem.Application.Transactions.Commands;

// ─────────────────────────────────────────────
// CREATE TRANSACTION
// ─────────────────────────────────────────────

public record CreateTransactionCommand(CreateTransactionDto Dto, int UserId) : IRequest<TransactionDto>;

public class CreateTransactionCommandHandler : IRequestHandler<CreateTransactionCommand, TransactionDto>
{
    private readonly IAppDbContext _db;
    private readonly ILogger<CreateTransactionCommandHandler> _logger;

    public CreateTransactionCommandHandler(IAppDbContext db, ILogger<CreateTransactionCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TransactionDto> Handle(CreateTransactionCommand request, CancellationToken ct)
    {
        // Kiểm tra danh mục tồn tại
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.Dto.CategoryId && c.IsActive, ct)
            ?? throw new NotFoundException("Danh mục", request.Dto.CategoryId);

        var transaction = new Transaction
        {
            Amount = request.Dto.Amount,
            Type = request.Dto.Type,
            CategoryId = request.Dto.CategoryId,
            UserId = request.UserId,
            Date = request.Dto.Date.ToUniversalTime(),
            Description = request.Dto.Description,
            CreatedAt = DateTime.UtcNow
        };

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Tạo giao dịch mới: Amount={Amount}, Type={Type}, UserId={UserId}",
            transaction.Amount, transaction.Type, transaction.UserId);

        return MapToDto(transaction, category);
    }

    private static TransactionDto MapToDto(Transaction t, Category c) => new()
    {
        Id = t.Id,
        Amount = t.Amount,
        Type = t.Type,
        CategoryId = t.CategoryId,
        CategoryName = c.Name,
        CategoryColorHex = c.ColorHex,
        Date = t.Date,
        Description = t.Description,
        CreatedAt = t.CreatedAt
    };
}

// ─────────────────────────────────────────────
// UPDATE TRANSACTION
// ─────────────────────────────────────────────

public record UpdateTransactionCommand(int Id, UpdateTransactionDto Dto, int UserId) : IRequest<TransactionDto>;

public class UpdateTransactionCommandHandler : IRequestHandler<UpdateTransactionCommand, TransactionDto>
{
    private readonly IAppDbContext _db;

    public UpdateTransactionCommandHandler(IAppDbContext db) => _db = db;

    public async Task<TransactionDto> Handle(UpdateTransactionCommand request, CancellationToken ct)
    {
        // Tìm giao dịch và kiểm tra quyền sở hữu
        var transaction = await _db.Transactions
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException("Giao dịch", request.Id);

        if (transaction.UserId != request.UserId)
            throw new ForbiddenException();

        // Kiểm tra danh mục mới
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.Dto.CategoryId && c.IsActive, ct)
            ?? throw new NotFoundException("Danh mục", request.Dto.CategoryId);

        // Cập nhật các trường
        transaction.Amount = request.Dto.Amount;
        transaction.Type = request.Dto.Type;
        transaction.CategoryId = request.Dto.CategoryId;
        transaction.Date = request.Dto.Date.ToUniversalTime();
        transaction.Description = request.Dto.Description;
        transaction.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new TransactionDto
        {
            Id = transaction.Id,
            Amount = transaction.Amount,
            Type = transaction.Type,
            CategoryId = category.Id,
            CategoryName = category.Name,
            CategoryColorHex = category.ColorHex,
            Date = transaction.Date,
            Description = transaction.Description,
            CreatedAt = transaction.CreatedAt
        };
    }
}

// ─────────────────────────────────────────────
// DELETE TRANSACTION
// ─────────────────────────────────────────────

public record DeleteTransactionCommand(int Id, int UserId, string UserRole) : IRequest<Unit>;

public class DeleteTransactionCommandHandler : IRequestHandler<DeleteTransactionCommand, Unit>
{
    private readonly IAppDbContext _db;

    public DeleteTransactionCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteTransactionCommand request, CancellationToken ct)
    {
        var transaction = await _db.Transactions
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException("Giao dịch", request.Id);

        // Admin có thể xóa bất kỳ giao dịch nào, user chỉ xóa được của mình
        if (transaction.UserId != request.UserId && request.UserRole != "Admin")
            throw new ForbiddenException();

        _db.Transactions.Remove(transaction);
        await _db.SaveChangesAsync(ct);

        return Unit.Value;
    }
}

