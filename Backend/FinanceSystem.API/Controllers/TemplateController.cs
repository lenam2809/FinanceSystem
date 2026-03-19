// Controller cung cấp endpoint tải template Excel
// GET /api/imports/template → trả về file .xlsx để người dùng tải về
using FinanceSystem.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceSystem.API.Controllers;

/// <summary>
/// API tải file template Excel import giao dịch
/// </summary>
[ApiController]
[Route("api/imports")]
[Authorize]
public class TemplateController : ControllerBase
{
    private readonly IExcelTemplateService _templateService;
    private readonly IAppDbContext         _db;

    public TemplateController(IExcelTemplateService templateService, IAppDbContext db)
    {
        _templateService = templateService;
        _db              = db;
    }

    /// <summary>
    /// Tải file Excel template với header, dữ liệu mẫu,
    /// dropdown danh mục và hướng dẫn nhập liệu
    /// </summary>
    [HttpGet("template")]
    [AllowAnonymous]  // Cho phép tải không cần đăng nhập để tiện cho lần đầu dùng
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadTemplate()
    {
        // Lấy danh sách tên danh mục từ DB để tạo dropdown validation
        var categoryNames = await _db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => c.Name)
            .ToListAsync();

        var fileBytes = _templateService.GenerateImportTemplate(categoryNames);

        // Tên file gợi ý kèm ngày để người dùng dễ quản lý
        var fileName = $"Template_Import_GiaoDich_{DateTime.Now:yyyyMMdd}.xlsx";

        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
