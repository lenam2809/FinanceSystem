// Queries cho import: GetImportHistory (paged), GetImportErrors
using FinanceSystem.Application.Common.Interfaces;
using FinanceSystem.Contracts.V1.Common;
using FinanceSystem.Contracts.V1.Imports;
using FinanceSystem.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceSystem.Application.Imports.Queries;

// ─────────────────────────────────────────────
// GET IMPORT HISTORY (PAGED)
// ─────────────────────────────────────────────

public record GetImportHistoryQuery(
    ImportHistoryFilterDto Filter,
    int UserId,
    string UserRole) : IRequest<FinanceSystem.Contracts.V1.Common.PagedResultDto<ImportHistoryDto>>;

public class GetImportHistoryQueryHandler
    : IRequestHandler<GetImportHistoryQuery, FinanceSystem.Contracts.V1.Common.PagedResultDto<ImportHistoryDto>>
{
    private readonly IAppDbContext _db;

    public GetImportHistoryQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FinanceSystem.Contracts.V1.Common.PagedResultDto<ImportHistoryDto>> Handle(
        GetImportHistoryQuery request, CancellationToken ct)
    {
        var f = request.Filter;
        var query = _db.ImportHistories
            .Include(h => h.User)
            .AsNoTracking()
            .AsQueryable();

        // User thường chỉ xem lịch sử của mình
        if (request.UserRole != "Admin")
            query = query.Where(h => h.UserId == request.UserId);

        var total = await query.CountAsync(ct);

        // Sắp xếp
        query = f.SortDesc
            ? query.OrderByDescending(h => h.StartedAt)
            : query.OrderBy(h => h.StartedAt);

        var items = await query
            .Skip((f.Page - 1) * f.PageSize)
            .Take(f.PageSize)
            .Select(h => new ImportHistoryDto
            {
                Id = h.Id,
                FileName = h.FileName,
                TotalRows = h.TotalRows,
                SuccessCount = h.SuccessCount,
                ErrorCount = h.ErrorCount,
                Status = h.Status,
                StartedAt = h.StartedAt,
                CompletedAt = h.CompletedAt,
                UserEmail = h.User.Email
            })
            .ToListAsync(ct);

        return new FinanceSystem.Contracts.V1.Common.PagedResultDto<ImportHistoryDto>
        {
            Items = items,
            Page = f.Page,
            PageSize = f.PageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling((double)total / f.PageSize),
            SortBy = f.SortBy,
            SortDesc = f.SortDesc
        };
    }
}

// ─────────────────────────────────────────────
// GET IMPORT ERRORS
// ─────────────────────────────────────────────

public record GetImportErrorsQuery(int ImportId, int UserId, string UserRole) : IRequest<List<ImportErrorDto>>;

public class GetImportErrorsQueryHandler : IRequestHandler<GetImportErrorsQuery, List<ImportErrorDto>>
{
    private readonly IAppDbContext _db;

    public GetImportErrorsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ImportErrorDto>> Handle(GetImportErrorsQuery request, CancellationToken ct)
    {
        // Kiểm tra quyền truy cập
        var history = await _db.ImportHistories
            .FirstOrDefaultAsync(h => h.Id == request.ImportId, ct)
            ?? throw new NotFoundException("Lịch sử import", request.ImportId);

        if (history.UserId != request.UserId && request.UserRole != "Admin")
            throw new ForbiddenException();

        return await _db.ImportErrors
            .Where(e => e.ImportHistoryId == request.ImportId)
            .OrderBy(e => e.RowNumber)
            .Select(e => new ImportErrorDto
            {
                RowNumber = e.RowNumber,
                ErrorMessage = e.ErrorMessage,
                RawData = e.RawData
            })
            .ToListAsync(ct);
    }
}
