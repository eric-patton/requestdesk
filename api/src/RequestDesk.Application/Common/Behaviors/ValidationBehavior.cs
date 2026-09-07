using FluentValidation;
using MediatR;

namespace RequestDesk.Application.Common.Behaviors;

/// <summary>
/// Runs every registered validator for a request before its handler. A failure becomes a
/// <see cref="ValidationException"/>, which the API turns into a 400 problem details response
/// with one entry per field.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        // One context per validator. A shared context accumulates failures across validators, and
        // every result then reports the same failure again.
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
