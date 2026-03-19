// Unit tests cho FluentValidation validators
// Kiểm tra tất cả trường hợp hợp lệ và không hợp lệ
using FinanceSystem.Application.Auth.Commands;
using FinanceSystem.Application.Auth.Validators;
using FluentAssertions;
using Xunit;

namespace FinanceSystem.Tests.Validators;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    // ─── Email ──────────────────────────────────────────────

    [Fact(DisplayName = "Email hợp lệ → không có lỗi")]
    public async Task Validate_ValidEmail_NoErrors()
    {
        var result = await _validator.ValidateAsync(new LoginCommand("user@example.com", "Password123"));
        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Email không hợp lệ → có lỗi")]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("@nodomain.com")]
    [InlineData("no-at-sign")]
    public async Task Validate_InvalidEmail_HasErrors(string email)
    {
        var result = await _validator.ValidateAsync(new LoginCommand(email, "Password123"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginCommand.Email));
    }

    [Fact(DisplayName = "Email quá dài (>256 ký tự) → có lỗi")]
    public async Task Validate_EmailTooLong_HasError()
    {
        var longEmail = new string('a', 250) + "@b.com";
        var result = await _validator.ValidateAsync(new LoginCommand(longEmail, "Password123"));
        result.IsValid.Should().BeFalse();
    }

    // ─── Password ───────────────────────────────────────────

    [Fact(DisplayName = "Mật khẩu để trống → có lỗi")]
    public async Task Validate_EmptyPassword_HasError()
    {
        var result = await _validator.ValidateAsync(new LoginCommand("user@example.com", ""));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginCommand.Password));
    }

    [Fact(DisplayName = "Mật khẩu < 6 ký tự → có lỗi")]
    public async Task Validate_ShortPassword_HasError()
    {
        var result = await _validator.ValidateAsync(new LoginCommand("user@example.com", "abc"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(LoginCommand.Password)
            && e.ErrorMessage.Contains("6 ký tự"));
    }

    [Fact(DisplayName = "Mật khẩu đúng định dạng (6-100 ký tự) → không có lỗi")]
    public async Task Validate_ValidPassword_NoErrors()
    {
        var result = await _validator.ValidateAsync(new LoginCommand("user@example.com", "ValidPass123"));
        result.IsValid.Should().BeTrue();
    }

    // ─── Tất cả trường hợp hợp lệ ──────────────────────────

    [Theory(DisplayName = "Login command hợp lệ → không có lỗi")]
    [InlineData("admin@finance.com", "Admin@123")]
    [InlineData("user@finance.com", "User@123")]
    [InlineData("test.user+filter@subdomain.example.org", "P@ssw0rd!")]
    public async Task Validate_ValidCommand_NoErrors(string email, string password)
    {
        var result = await _validator.ValidateAsync(new LoginCommand(email, password));
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}

public class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _validator = new();

    [Fact(DisplayName = "Cả hai token đều có giá trị → hợp lệ")]
    public async Task Validate_BothTokensProvided_IsValid()
    {
        var result = await _validator.ValidateAsync(
            new RefreshTokenCommand("access_token", "refresh_token"));
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Access token rỗng → không hợp lệ")]
    public async Task Validate_EmptyAccessToken_IsInvalid()
    {
        var result = await _validator.ValidateAsync(
            new RefreshTokenCommand("", "refresh_token"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RefreshTokenCommand.AccessToken));
    }

    [Fact(DisplayName = "Refresh token rỗng → không hợp lệ")]
    public async Task Validate_EmptyRefreshToken_IsInvalid()
    {
        var result = await _validator.ValidateAsync(
            new RefreshTokenCommand("access_token", ""));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RefreshTokenCommand.RefreshToken));
    }
}

public class RevokeTokenCommandValidatorTests
{
    private readonly RevokeTokenCommandValidator _validator = new();

    [Fact(DisplayName = "Revoke đơn lẻ có token → hợp lệ")]
    public async Task Validate_SingleRevokeWithToken_IsValid()
    {
        var result = await _validator.ValidateAsync(new RevokeTokenCommand("some_token"));
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Revoke tất cả (không cần token) → hợp lệ")]
    public async Task Validate_RevokeAll_IsValid()
    {
        var result = await _validator.ValidateAsync(
            new RevokeTokenCommand(string.Empty, RevokeAll: true, UserId: 1));
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Revoke đơn lẻ không có token → không hợp lệ")]
    public async Task Validate_SingleRevokeWithoutToken_IsInvalid()
    {
        var result = await _validator.ValidateAsync(
            new RevokeTokenCommand("", RevokeAll: false));
        result.IsValid.Should().BeFalse();
    }
}
