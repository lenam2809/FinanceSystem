// Controller quản lý giao dịch tài chính
// Hỗ trợ CRUD, phân trang, lọc và thống kê
using FinanceSystem.Application.Transactions.Commands;
using FinanceSystem.Application.Transactions.Queries;
using FinanceSystem.Contracts.V1.Transactions;
using FinanceSystem.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceSystem.API.Controllers;

/// <summary>
/// API quản lý giao dịch thu chi
/// Yêu cầu xác thực JWT cho tất cả endpoints
/// </summary>
[ApiController]
[Route("api/transactions")]
[Authorize]
[Produces("application/json")]
public class TransactionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public TransactionsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Lấy danh sách giao dịch có phân trang, lọc và sắp xếp
    /// Admin xem tất cả, User chỉ xem của mình
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTransactions([FromQuery] TransactionFilterDto filter)
    {
        var result = await _mediator.Send(new GetTransactionsQuery(
            filter,
            _currentUser.UserId!.Value,
            _currentUser.Role!));
        return Ok(result);
    }

    /// <summary>
    /// Lấy thống kê tổng thu/chi
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo)
    {
        var result = await _mediator.Send(new GetTransactionSummaryQuery(
            _currentUser.UserId!.Value,
            _currentUser.Role!,
            dateFrom,
            dateTo));
        return Ok(result);
    }

    /// <summary>
    /// Tạo giao dịch mới
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionDto dto)
    {
        var result = await _mediator.Send(new CreateTransactionCommand(dto, _currentUser.UserId!.Value));
        return CreatedAtAction(nameof(GetTransactions), new { }, result);
    }

    /// <summary>
    /// Cập nhật giao dịch (chỉ owner hoặc Admin)
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTransaction(int id, [FromBody] UpdateTransactionDto dto)
    {
        var result = await _mediator.Send(new UpdateTransactionCommand(id, dto, _currentUser.UserId!.Value));
        return Ok(result);
    }

    /// <summary>
    /// Xóa giao dịch (chỉ owner hoặc Admin)
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTransaction(int id)
    {
        await _mediator.Send(new DeleteTransactionCommand(id, _currentUser.UserId!.Value, _currentUser.Role!));
        return NoContent();
    }
}
