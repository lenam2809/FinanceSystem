// Controller xử lý xác thực: đăng nhập, làm mới token, thu hồi token
// Rate limiting được áp dụng trên các endpoint nhạy cảm
using FinanceSystem.Application.Auth.Commands;
using FinanceSystem.Contracts.V1.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceSystem.API.Controllers;

/// <summary>
/// API xác thực người dùng
/// Áp dụng rate limiting để chống brute-force
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Đăng nhập bằng email và mật khẩu
    /// Trả về access token (15 phút) và refresh token (7 ngày)
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth-policy")] // Tối đa 5 request/phút
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var result = await _mediator.Send(new LoginCommand(dto.Email, dto.Password));
        return Ok(result);
    }

    /// <summary>
    /// Làm mới access token bằng refresh token còn hiệu lực
    /// Token cũ sẽ bị thu hồi ngay lập tức (token rotation)
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth-policy")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto dto)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(dto.AccessToken, dto.RefreshToken));
        return Ok(result);
    }

    /// <summary>
    /// Thu hồi refresh token hiện tại (đăng xuất)
    /// </summary>
    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Revoke([FromBody] RevokeTokenRequestDto dto)
    {
        await _mediator.Send(new RevokeTokenCommand(dto.RefreshToken));
        return NoContent();
    }

    /// <summary>
    /// Thu hồi toàn bộ phiên đăng nhập (tất cả thiết bị)
    /// Dùng khi đổi mật khẩu hoặc nghi ngờ tài khoản bị xâm phạm
    /// </summary>
    [HttpPost("revoke-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeAll()
    {
        var userId = int.Parse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!.Value);
        await _mediator.Send(new RevokeTokenCommand(string.Empty, RevokeAll: true, UserId: userId));
        return NoContent();
    }
}
