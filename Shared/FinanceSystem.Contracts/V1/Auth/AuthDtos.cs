// DTOs xác thực - phiên bản V1
// Dùng chung giữa API, WPF và Blazor
namespace FinanceSystem.Contracts.V1.Auth;

/// <summary>
/// Yêu cầu đăng nhập
/// </summary>
public class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Phản hồi sau khi đăng nhập hoặc làm mới token thành công
/// </summary>
public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu làm mới access token bằng refresh token
/// </summary>
public class RefreshTokenRequestDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu thu hồi refresh token (đăng xuất)
/// </summary>
public class RevokeTokenRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}
