// Commands xác thực: Login, RefreshToken, RevokeToken
// Sử dụng MediatR CQRS pattern
using FinanceSystem.Application.Common.Interfaces;
using FinanceSystem.Contracts.V1.Auth;
using FinanceSystem.Domain.Entities;
using FinanceSystem.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceSystem.Application.Auth.Commands;

// ─────────────────────────────────────────────
// LOGIN COMMAND
// ─────────────────────────────────────────────

/// <summary>
/// Command đăng nhập - trả về access token và refresh token
/// </summary>
public record LoginCommand(string Email, string Password) : IRequest<AuthResponseDto>;

/// <summary>
/// Xử lý đăng nhập: xác thực credentials, tạo token pair
/// </summary>
public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IAppDbContext _db;
    private readonly IJwtService _jwtService;
    private readonly IPasswordService _passwordService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IAppDbContext db,
        IJwtService jwtService,
        IPasswordService passwordService,
        ILogger<LoginCommandHandler> logger)
    {
        _db = db;
        _jwtService = jwtService;
        _passwordService = passwordService;
        _logger = logger;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken ct)
    {
        // Tìm user theo email (không phân biệt hoa thường)
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

        // Kiểm tra user tồn tại và mật khẩu đúng
        if (user == null || !_passwordService.VerifyPassword(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Email hoặc mật khẩu không chính xác.");

        // Kiểm tra tài khoản có bị khóa không
        if (!user.IsActive)
            throw new UnauthorizedException("Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.");

        // Tạo cặp token mới
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshTokenValue = _jwtService.GenerateRefreshToken();

        // Lưu refresh token vào DB
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiryDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Người dùng {Email} đăng nhập thành công.", user.Email);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            Expiry = DateTime.UtcNow.AddMinutes(15),
            UserEmail = user.Email,
            UserName = user.FullName,
            Role = user.Role
        };
    }
}

// ─────────────────────────────────────────────
// REFRESH TOKEN COMMAND
// ─────────────────────────────────────────────

/// <summary>
/// Command làm mới access token bằng refresh token còn hiệu lực
/// </summary>
public record RefreshTokenCommand(string AccessToken, string RefreshToken) : IRequest<AuthResponseDto>;

/// <summary>
/// Xử lý làm mới token: kiểm tra hợp lệ, rotate token, xử lý reuse attack
/// </summary>
public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    private readonly IAppDbContext _db;
    private readonly IJwtService _jwtService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IAppDbContext db,
        IJwtService jwtService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _db = db;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        // Lấy userId từ access token đã hết hạn
        var userId = _jwtService.GetUserIdFromExpiredToken(request.AccessToken)
            ?? throw new UnauthorizedException("Access token không hợp lệ.");

        // Tìm refresh token trong DB
        var storedToken = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken && t.UserId == userId, ct);

        if (storedToken == null)
            throw new UnauthorizedException("Refresh token không tồn tại.");

        // PHÁT HIỆN TẤN CÔNG TÁI SỬ DỤNG TOKEN:
        // Nếu token đã bị thu hồi mà vẫn được gửi lại → khả năng bị đánh cắp
        if (storedToken.IsRevoked)
        {
            _logger.LogWarning("Phát hiện tái sử dụng refresh token đã thu hồi cho userId={UserId}. Thu hồi tất cả token.", userId);

            // Thu hồi toàn bộ token của user để bảo vệ tài khoản
            await RevokeAllUserTokensAsync(userId, ct);
            throw new UnauthorizedException("Phát hiện hoạt động bất thường. Tất cả phiên đăng nhập đã bị hủy. Vui lòng đăng nhập lại.");
        }

        // Kiểm tra token có hết hạn chưa
        if (storedToken.ExpiryDate < DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token đã hết hạn. Vui lòng đăng nhập lại.");

        // Tạo cặp token mới
        var newAccessToken = _jwtService.GenerateAccessToken(storedToken.User);
        var newRefreshTokenValue = _jwtService.GenerateRefreshToken();

        // Thu hồi token cũ và ghi lại token mới thay thế (token rotation)
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.ReplacedByToken = newRefreshTokenValue;

        // Lưu refresh token mới
        var newRefreshToken = new RefreshToken
        {
            UserId = userId,
            Token = newRefreshTokenValue,
            ExpiryDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _db.RefreshTokens.Add(newRefreshToken);
        await _db.SaveChangesAsync(ct);

        return new AuthResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshTokenValue,
            Expiry = DateTime.UtcNow.AddMinutes(15),
            UserEmail = storedToken.User.Email,
            UserName = storedToken.User.FullName,
            Role = storedToken.User.Role
        };
    }

    // Thu hồi tất cả token active của một user
    private async Task RevokeAllUserTokensAsync(int userId, CancellationToken ct)
    {
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }
}

// ─────────────────────────────────────────────
// REVOKE TOKEN COMMAND
// ─────────────────────────────────────────────

/// <summary>
/// Command thu hồi refresh token (đăng xuất hoặc đổi mật khẩu)
/// </summary>
public record RevokeTokenCommand(string Token, bool RevokeAll = false, int? UserId = null) : IRequest<Unit>;

/// <summary>
/// Xử lý thu hồi token: hỗ trợ thu hồi đơn lẻ hoặc tất cả token của user
/// </summary>
public class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ILogger<RevokeTokenCommandHandler> _logger;

    public RevokeTokenCommandHandler(IAppDbContext db, ILogger<RevokeTokenCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Unit> Handle(RevokeTokenCommand request, CancellationToken ct)
    {
        // Thu hồi tất cả token của user (dùng khi đổi mật khẩu)
        if (request.RevokeAll && request.UserId.HasValue)
        {
            var allTokens = await _db.RefreshTokens
                .Where(t => t.UserId == request.UserId.Value && !t.IsRevoked)
                .ToListAsync(ct);

            foreach (var t in allTokens)
            {
                t.IsRevoked = true;
                t.RevokedAt = DateTime.UtcNow;
            }

            _logger.LogInformation("Thu hồi {Count} token của userId={UserId}.", allTokens.Count, request.UserId);
        }
        else
        {
            // Thu hồi token đơn lẻ (đăng xuất)
            var token = await _db.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == request.Token, ct)
                ?? throw new NotFoundException("RefreshToken", request.Token);

            if (token.IsRevoked)
                throw new BusinessException("Token này đã được thu hồi trước đó.");

            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;

            _logger.LogInformation("Thu hồi refresh token cho userId={UserId}.", token.UserId);
        }

        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
