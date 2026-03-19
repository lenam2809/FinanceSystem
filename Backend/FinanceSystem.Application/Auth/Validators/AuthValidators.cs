// Validators cho các Auth commands sử dụng FluentValidation
// Tất cả message lỗi bằng tiếng Việt
using FinanceSystem.Application.Auth.Commands;
using FluentValidation;

namespace FinanceSystem.Application.Auth.Validators;

/// <summary>
/// Xác thực dữ liệu đăng nhập
/// </summary>
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.")
            .MaximumLength(256).WithMessage("Email không được vượt quá 256 ký tự.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự.")
            .MaximumLength(100).WithMessage("Mật khẩu không được vượt quá 100 ký tự.");
    }
}

/// <summary>
/// Xác thực yêu cầu làm mới token
/// </summary>
public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty().WithMessage("Access token không được để trống.");

        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token không được để trống.");
    }
}

/// <summary>
/// Xác thực yêu cầu thu hồi token
/// </summary>
public class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
    public RevokeTokenCommandValidator()
    {
        // Nếu không thu hồi tất cả thì phải có token cụ thể
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token không được để trống.")
            .When(x => !x.RevokeAll);
    }
}
