// Dịch vụ lấy thông tin người dùng đang đăng nhập từ JWT token
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinanceSystem.Application.Common.Interfaces;

namespace FinanceSystem.API.Services;

/// <summary>
/// Lấy thông tin người dùng hiện tại từ HTTP context (JWT claims)
/// Được inject vào các handler cần biết user đang thực hiện thao tác
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // Lấy userId từ claim "sub" trong JWT
    public int? UserId
    {
        get
        {
            var userIdStr = _httpContextAccessor.HttpContext?.User
                .FindFirstValue("userId");
            return int.TryParse(userIdStr, out var id) ? id : null;
        }
    }

    // Lấy email từ claim "email"
    public string? Email =>
        _httpContextAccessor.HttpContext?.User
            .FindFirstValue(JwtRegisteredClaimNames.Email);

    // Lấy role từ claim "role"
    public string? Role =>
        _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.Role);

    // Kiểm tra đã xác thực chưa
    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
