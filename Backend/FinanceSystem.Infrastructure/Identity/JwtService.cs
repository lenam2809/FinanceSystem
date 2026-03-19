// Triển khai dịch vụ JWT: tạo và xác thực access token, tạo refresh token ngẫu nhiên
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FinanceSystem.Application.Common.Interfaces;
using FinanceSystem.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FinanceSystem.Infrastructure.Identity;

/// <summary>
/// Triển khai IJwtService: tạo JWT access token và refresh token ngẫu nhiên
/// </summary>
public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    // Thời gian sống của access token: 15 phút
    private const int AccessTokenExpiryMinutes = 15;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Tạo JWT access token chứa thông tin user (userId, email, role)
    /// </summary>
    public string GenerateAccessToken(User user)
    {
        var jwtKey = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Cấu hình Jwt:Key chưa được thiết lập.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Định nghĩa các claims trong token
        var claims = new[]
        {
            new Claim("userId", user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("fullName", user.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // JWT ID duy nhất
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Tạo refresh token ngẫu nhiên 256-bit (cryptographically secure)
    /// </summary>
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Lấy userId từ access token đã hết hạn (dùng khi refresh)
    /// Chỉ validate chữ ký và cấu trúc, KHÔNG validate thời gian hết hạn
    /// </summary>
    public int? GetUserIdFromExpiredToken(string token)
    {
        var jwtKey = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Cấu hình Jwt:Key chưa được thiết lập.");

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidIssuer = _config["Jwt:Issuer"],
            ValidAudience = _config["Jwt:Audience"],
            // BẮT BUỘC: Bỏ qua validate thời gian hết hạn khi dùng để refresh
            ValidateLifetime = false
        };

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

            // Kiểm tra đây là JWT với thuật toán HMAC SHA256
            if (securityToken is not JwtSecurityToken jwtSecurityToken
                || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            // Lấy userId từ claim "sub"
            var userIdStr = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return int.TryParse(userIdStr, out var userId) ? userId : null;
        }
        catch
        {
            // Token không hợp lệ
            return null;
        }
    }
}
