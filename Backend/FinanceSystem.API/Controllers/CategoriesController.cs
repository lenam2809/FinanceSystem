// Controller lấy danh sách danh mục giao dịch
using FinanceSystem.Application.Common.Interfaces;
using FinanceSystem.Contracts.V1.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceSystem.API.Controllers;

/// <summary>
/// API danh mục giao dịch (chỉ đọc - quản lý qua migration/seed)
/// </summary>
[ApiController]
[Route("api/categories")]
[Authorize]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IAppDbContext _db;

    public CategoriesController(IAppDbContext db) => _db = db;

    /// <summary>
    /// Lấy tất cả danh mục đang hoạt động
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ColorHex = c.ColorHex,
                Icon = c.Icon
            })
            .ToListAsync();

        return Ok(categories);
    }
}
