// Queries cho giao dịch: GetTransactions (paged), GetTransactionSummary
using FinanceSystem.Application.Common.Interfaces;
using FinanceSystem.Contracts.V1.Common;
using FinanceSystem.Contracts.V1.Transactions;
using FinanceSystem.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceSystem.Application.Transactions.Queries;

// ─────────────────────────────────────────────
// GET TRANSACTIONS (PAGED)
// ─────────────────────────────────────────────

public record GetTransactionsQuery(TransactionFilterDto Filter, int UserId, string UserRole) : IRequest<PagedResultDto<TransactionDto>>;

public class GetTransactionsQueryHandler : IRequestHandler<GetTransactionsQuery, PagedResultDto<TransactionDto>>
{
    private readonly IAppDbContext _db;

    public GetTransactionsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PagedResultDto<TransactionDto>> Handle(GetTransactionsQuery request, CancellationToken ct)
    {
        var f = request.Filter;

        // Xây dựng query cơ sở
        var query = _db.Transactions
            .Include(t => t.Category)
            .AsNoTracking()
            .AsQueryable();

        // User thường chỉ xem được giao dịch của mình
        if (request.UserRole != "Admin")
            query = query.Where(t => t.UserId == request.UserId);

        // Áp dụng bộ lọc
        if (f.DateFrom.HasValue)
            query = query.Where(t => t.Date >= f.DateFrom.Value.ToUniversalTime());
        if (f.DateTo.HasValue)
            query = query.Where(t => t.Date <= f.DateTo.Value.ToUniversalTime());
        if (f.CategoryId.HasValue)
            query = query.Where(t => t.CategoryId == f.CategoryId.Value);
        if (!string.IsNullOrWhiteSpace(f.Type))
            query = query.Where(t => t.Type == f.Type);
        if (f.AmountMin.HasValue)
            query = query.Where(t => t.Amount >= f.AmountMin.Value);
        if (f.AmountMax.HasValue)
            query = query.Where(t => t.Amount <= f.AmountMax.Value);
        if (!string.IsNullOrWhiteSpace(f.SearchText))
            query = query.Where(t => t.Description != null && t.Description.Contains(f.SearchText));

        // Đếm tổng để phân trang
        var totalItems = await query.CountAsync(ct);

        // Sắp xếp động
        query = (f.SortBy?.ToLower(), f.SortDesc) switch
        {
            ("amount", true) => query.OrderByDescending(t => t.Amount),
            ("amount", false) => query.OrderBy(t => t.Amount),
            ("category", true) => query.OrderByDescending(t => t.Category.Name),
            ("category", false) => query.OrderBy(t => t.Category.Name),
            (_, true) => query.OrderByDescending(t => t.Date),
            (_, false) => query.OrderBy(t => t.Date),
        };

        // Phân trang
        var items = await query
            .Skip((f.Page - 1) * f.PageSize)
            .Take(f.PageSize)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Type = t.Type,
                CategoryId = t.CategoryId,
                CategoryName = t.Category.Name,
                CategoryColorHex = t.Category.ColorHex,
                Date = t.Date,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResultDto<TransactionDto>
        {
            Items = items,
            Page = f.Page,
            PageSize = f.PageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling((double)totalItems / f.PageSize),
            SortBy = f.SortBy,
            SortDesc = f.SortDesc
        };
    }
}

// ─────────────────────────────────────────────
// GET TRANSACTION SUMMARY
// ─────────────────────────────────────────────

public record GetTransactionSummaryQuery(int UserId, string UserRole, DateTime? DateFrom = null, DateTime? DateTo = null)
    : IRequest<TransactionSummaryDto>;

public class GetTransactionSummaryQueryHandler : IRequestHandler<GetTransactionSummaryQuery, TransactionSummaryDto>
{
    private readonly IAppDbContext _db;

    public GetTransactionSummaryQueryHandler(IAppDbContext db) => _db = db;

    public async Task<TransactionSummaryDto> Handle(GetTransactionSummaryQuery request, CancellationToken ct)
    {
        var query = _db.Transactions.AsNoTracking().AsQueryable();

        if (request.UserRole != "Admin")
            query = query.Where(t => t.UserId == request.UserId);
        if (request.DateFrom.HasValue)
            query = query.Where(t => t.Date >= request.DateFrom.Value.ToUniversalTime());
        if (request.DateTo.HasValue)
            query = query.Where(t => t.Date <= request.DateTo.Value.ToUniversalTime());

        var totalIncome = await query.Where(t => t.Type == "Income").SumAsync(t => t.Amount, ct);
        var totalExpense = await query.Where(t => t.Type == "Expense").SumAsync(t => t.Amount, ct);
        var count = await query.CountAsync(ct);

        return new TransactionSummaryDto
        {
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            Balance = totalIncome - totalExpense,
            TransactionCount = count
        };
    }
}
