// MediatR Pipeline Behavior: tự động chạy validation trước khi xử lý command
// Tích hợp FluentValidation vào CQRS pipeline
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using ValidationException = FinanceSystem.Domain.Exceptions.ValidationException;

namespace FinanceSystem.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior tự động validate mọi command/query trước khi handler xử lý
/// Nếu có lỗi validation → ném ra ValidationException với message tiếng Việt
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Nếu không có validator nào → bỏ qua bước này
        if (!_validators.Any())
            return await next();

        // Chạy tất cả validators song song
        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        // Gom tất cả lỗi lại theo tên trường
        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray());

        if (failures.Any())
        {
            _logger.LogWarning("Lỗi validation cho {RequestType}: {@Errors}",
                typeof(TRequest).Name, failures);
            throw new ValidationException(failures);
        }

        return await next();
    }
}

/// <summary>
/// Pipeline behavior ghi log thời gian xử lý request (performance logging)
/// </summary>
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

    // Cảnh báo nếu request mất hơn 500ms
    private const int SlowRequestThresholdMs = 500;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
        {
            _logger.LogWarning("Request chậm: {RequestType} mất {ElapsedMs}ms",
                typeof(TRequest).Name, sw.ElapsedMilliseconds);
        }

        return response;
    }
}
