// Controller xử lý import file Excel
// Upload file, xem lịch sử import và chi tiết lỗi
using FinanceSystem.Application.Imports.Commands;
using FinanceSystem.Application.Imports.Queries;
using FinanceSystem.Application.Common.Interfaces;
using FinanceSystem.Contracts.V1.Imports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceSystem.API.Controllers;

/// <summary>
/// API quản lý import dữ liệu từ file Excel
/// </summary>
[ApiController]
[Route("api/imports")]
[Authorize]
[Produces("application/json")]
public class ImportsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    // Kích thước file tối đa: 10 MB
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public ImportsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Upload và xử lý file Excel import giao dịch
    /// Định dạng file: .xlsx với cột Ngày, Số tiền, Danh mục, Mô tả
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)] // Giới hạn 10 MB ở mức middleware
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportTransactions(IFormFile file)
    {
        // Kiểm tra file đã được upload
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn file để upload." });

        // Kiểm tra kích thước
        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "Kích thước file vượt quá giới hạn 10 MB." });

        // Kiểm tra định dạng
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Chỉ chấp nhận file định dạng .xlsx." });

        using var stream = file.OpenReadStream();

        var result = await _mediator.Send(new ImportTransactionsCommand(
            stream,
            file.FileName,
            file.Length,
            _currentUser.UserId!.Value));

        return Ok(result);
    }

    /// <summary>
    /// Lấy lịch sử các lần import có phân trang
    /// Admin xem tất cả, User chỉ xem của mình
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(FinanceSystem.Contracts.V1.Common.PagedResultDto<ImportHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory([FromQuery] ImportHistoryFilterDto filter)
    {
        var result = await _mediator.Send(new GetImportHistoryQuery(
            filter,
            _currentUser.UserId!.Value,
            _currentUser.Role!));
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách lỗi chi tiết của một lần import
    /// </summary>
    [HttpGet("{id:int}/errors")]
    [ProducesResponseType(typeof(List<ImportErrorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetErrors(int id)
    {
        var result = await _mediator.Send(new GetImportErrorsQuery(
            id,
            _currentUser.UserId!.Value,
            _currentUser.Role!));
        return Ok(result);
    }
}
