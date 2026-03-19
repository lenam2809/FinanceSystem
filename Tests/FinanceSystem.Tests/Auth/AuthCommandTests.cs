// Unit tests cho Auth commands: Login, RefreshToken, RevokeToken
// Sử dụng InMemory DB và Moq để mock các dependencies
using FinanceSystem.Application.Auth.Commands;
using FinanceSystem.Application.Common.Interfaces;
using FinanceSystem.Contracts.V1.Auth;
using FinanceSystem.Domain.Entities;
using FinanceSystem.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceSystem.Tests.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<IPasswordService> _passwordServiceMock;
    private readonly Mock<ILogger<LoginCommandHandler>> _loggerMock;

    public LoginCommandHandlerTests()
    {
        _jwtServiceMock = new Mock<IJwtService>();
        _passwordServiceMock = new Mock<IPasswordService>();
        _loggerMock = new Mock<ILogger<LoginCommandHandler>>();
    }

    // ─── Test 1: Đăng nhập thành công ───────────────────────
    [Fact(DisplayName = "Đăng nhập với thông tin hợp lệ → trả về token")]
    public async Task Handle_ValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        _passwordServiceMock
            .Setup(x => x.VerifyPassword("User@123", It.IsAny<string>()))
            .Returns(true);
        _jwtServiceMock
            .Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access_token_value");
        _jwtServiceMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token_value");

        var handler = new LoginCommandHandler(db, _jwtServiceMock.Object, _passwordServiceMock.Object, _loggerMock.Object);
        var command = new LoginCommand("user@finance.com", "User@123");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access_token_value");
        result.RefreshToken.Should().Be("refresh_token_value");
        result.UserEmail.Should().Be("user@finance.com");

        // Verify refresh token được lưu vào DB
        db.RefreshTokens.Should().HaveCount(1);
        db.RefreshTokens.First().Token.Should().Be("refresh_token_value");
    }

    // ─── Test 2: Sai mật khẩu ───────────────────────────────
    [Fact(DisplayName = "Đăng nhập với mật khẩu sai → ném UnauthorizedException")]
    public async Task Handle_WrongPassword_ThrowsUnauthorizedException()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        _passwordServiceMock
            .Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        var handler = new LoginCommandHandler(db, _jwtServiceMock.Object, _passwordServiceMock.Object, _loggerMock.Object);
        var command = new LoginCommand("user@finance.com", "WrongPassword");

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Email hoặc mật khẩu*");
    }

    // ─── Test 3: Email không tồn tại ────────────────────────
    [Fact(DisplayName = "Đăng nhập với email không tồn tại → ném UnauthorizedException")]
    public async Task Handle_EmailNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        _passwordServiceMock.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var handler = new LoginCommandHandler(db, _jwtServiceMock.Object, _passwordServiceMock.Object, _loggerMock.Object);
        var command = new LoginCommand("khong.ton.tai@finance.com", "Password123");

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedException>();
    }

    // ─── Test 4: Tài khoản bị vô hiệu hóa ───────────────────
    [Fact(DisplayName = "Đăng nhập với tài khoản bị khóa → ném UnauthorizedException")]
    public async Task Handle_InactiveAccount_ThrowsUnauthorizedException()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        db.Users.Add(new User
        {
            Id = 10,
            Email = "locked@finance.com",
            PasswordHash = "hash",
            FullName = "Locked User",
            Role = "User",
            IsActive = false // Tài khoản bị khóa
        });
        await db.SaveChangesAsync();

        _passwordServiceMock.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var handler = new LoginCommandHandler(db, _jwtServiceMock.Object, _passwordServiceMock.Object, _loggerMock.Object);
        var command = new LoginCommand("locked@finance.com", "Password123");

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*vô hiệu hóa*");
    }
}

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<ILogger<RefreshTokenCommandHandler>> _loggerMock;

    public RefreshTokenCommandHandlerTests()
    {
        _jwtServiceMock = new Mock<IJwtService>();
        _loggerMock = new Mock<ILogger<RefreshTokenCommandHandler>>();
    }

    // ─── Test 1: Refresh thành công ─────────────────────────
    [Fact(DisplayName = "Refresh với token hợp lệ → trả về token mới và rotate token cũ")]
    public async Task Handle_ValidToken_ReturnsNewTokenAndRotatesOld()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        var oldRefreshToken = new RefreshToken
        {
            Id = 1,
            UserId = 2,
            Token = "valid_refresh_token",
            ExpiryDate = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };
        db.RefreshTokens.Add(oldRefreshToken);
        await db.SaveChangesAsync();

        _jwtServiceMock.Setup(x => x.GetUserIdFromExpiredToken("expired_access")).Returns(2);
        _jwtServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>())).Returns("new_access_token");
        _jwtServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("new_refresh_token");

        var handler = new RefreshTokenCommandHandler(db, _jwtServiceMock.Object, _loggerMock.Object);
        var command = new RefreshTokenCommand("expired_access", "valid_refresh_token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.AccessToken.Should().Be("new_access_token");
        result.RefreshToken.Should().Be("new_refresh_token");

        // Token cũ phải bị thu hồi với ReplacedByToken trỏ đến token mới
        var revokedToken = db.RefreshTokens.Find(1)!;
        revokedToken.IsRevoked.Should().BeTrue();
        revokedToken.ReplacedByToken.Should().Be("new_refresh_token");
        revokedToken.RevokedAt.Should().NotBeNull();

        // Token mới phải được lưu
        db.RefreshTokens.Should().HaveCount(2);
    }

    // ─── Test 2: Token đã bị thu hồi → 401 ─────────────────
    [Fact(DisplayName = "Refresh với token đã bị thu hồi → thu hồi tất cả và ném 401")]
    public async Task Handle_RevokedToken_RevokesAllAndThrows401()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        // Thêm token đã bị thu hồi
        db.RefreshTokens.AddRange(
            new RefreshToken { UserId = 2, Token = "revoked_token",  IsRevoked = true,  ExpiryDate = DateTime.UtcNow.AddDays(1) },
            new RefreshToken { UserId = 2, Token = "active_token_1", IsRevoked = false, ExpiryDate = DateTime.UtcNow.AddDays(7) },
            new RefreshToken { UserId = 2, Token = "active_token_2", IsRevoked = false, ExpiryDate = DateTime.UtcNow.AddDays(7) }
        );
        await db.SaveChangesAsync();

        _jwtServiceMock.Setup(x => x.GetUserIdFromExpiredToken(It.IsAny<string>())).Returns(2);

        var handler = new RefreshTokenCommandHandler(db, _jwtServiceMock.Object, _loggerMock.Object);
        var command = new RefreshTokenCommand("any_access", "revoked_token");

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*bất thường*");

        // Tất cả token active của user phải bị thu hồi
        var activeTokens = db.RefreshTokens.Where(t => t.UserId == 2 && !t.IsRevoked).ToList();
        activeTokens.Should().BeEmpty("tất cả token phải bị thu hồi khi phát hiện tấn công");
    }

    // ─── Test 3: Token đã hết hạn → 401 ────────────────────
    [Fact(DisplayName = "Refresh với token hết hạn → ném 401")]
    public async Task Handle_ExpiredToken_ThrowsUnauthorizedException()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = 2,
            Token = "expired_refresh",
            IsRevoked = false,
            ExpiryDate = DateTime.UtcNow.AddDays(-1) // Đã hết hạn từ hôm qua
        });
        await db.SaveChangesAsync();

        _jwtServiceMock.Setup(x => x.GetUserIdFromExpiredToken(It.IsAny<string>())).Returns(2);

        var handler = new RefreshTokenCommandHandler(db, _jwtServiceMock.Object, _loggerMock.Object);
        var command = new RefreshTokenCommand("any_access", "expired_refresh");

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*hết hạn*");
    }

    // ─── Test 4: Access token không hợp lệ → 401 ───────────
    [Fact(DisplayName = "Refresh với access token không hợp lệ → ném 401")]
    public async Task Handle_InvalidAccessToken_ThrowsUnauthorizedException()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        _jwtServiceMock.Setup(x => x.GetUserIdFromExpiredToken(It.IsAny<string>())).Returns((int?)null);

        var handler = new RefreshTokenCommandHandler(db, _jwtServiceMock.Object, _loggerMock.Object);
        var command = new RefreshTokenCommand("invalid_access", "any_refresh");

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Access token không hợp lệ*");
    }
}

public class RevokeTokenCommandHandlerTests
{
    private readonly Mock<ILogger<RevokeTokenCommandHandler>> _loggerMock;

    public RevokeTokenCommandHandlerTests()
    {
        _loggerMock = new Mock<ILogger<RevokeTokenCommandHandler>>();
    }

    // ─── Test 1: Thu hồi token đơn lẻ ──────────────────────
    [Fact(DisplayName = "Thu hồi token đơn lẻ → đánh dấu IsRevoked = true")]
    public async Task Handle_RevokeToken_MarksAsRevoked()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = 1,
            UserId = 2,
            Token = "token_to_revoke",
            IsRevoked = false,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync();

        var handler = new RevokeTokenCommandHandler(db, _loggerMock.Object);
        var command = new RevokeTokenCommand("token_to_revoke");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var token = db.RefreshTokens.Find(1)!;
        token.IsRevoked.Should().BeTrue();
        token.RevokedAt.Should().NotBeNull();
    }

    // ─── Test 2: Thu hồi tất cả token của user ──────────────
    [Fact(DisplayName = "Thu hồi tất cả token của user → tất cả IsRevoked = true")]
    public async Task Handle_RevokeAll_RevokesAllUserTokens()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        db.RefreshTokens.AddRange(
            new RefreshToken { UserId = 2, Token = "token_1", IsRevoked = false, ExpiryDate = DateTime.UtcNow.AddDays(7) },
            new RefreshToken { UserId = 2, Token = "token_2", IsRevoked = false, ExpiryDate = DateTime.UtcNow.AddDays(7) },
            new RefreshToken { UserId = 1, Token = "admin_token", IsRevoked = false, ExpiryDate = DateTime.UtcNow.AddDays(7) }
        );
        await db.SaveChangesAsync();

        var handler = new RevokeTokenCommandHandler(db, _loggerMock.Object);
        var command = new RevokeTokenCommand(string.Empty, RevokeAll: true, UserId: 2);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert - Chỉ token của user 2 bị thu hồi
        db.RefreshTokens.Where(t => t.UserId == 2).Should().AllSatisfy(t => t.IsRevoked.Should().BeTrue());
        // Token của admin KHÔNG bị ảnh hưởng
        db.RefreshTokens.First(t => t.UserId == 1).IsRevoked.Should().BeFalse();
    }

    // ─── Test 3: Token không tồn tại ────────────────────────
    [Fact(DisplayName = "Thu hồi token không tồn tại → ném NotFoundException")]
    public async Task Handle_TokenNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var db = TestDbContextFactory.CreateWithSeedData();
        var handler = new RevokeTokenCommandHandler(db, _loggerMock.Object);
        var command = new RevokeTokenCommand("khong_ton_tai");

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }
}
