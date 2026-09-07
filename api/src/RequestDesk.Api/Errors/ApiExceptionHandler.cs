using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RequestDesk.Application.Common;
using RequestDesk.Domain.Common;

namespace RequestDesk.Api.Errors;

/// <summary>
/// One place that turns exceptions into RFC 9457 problem details. Domain and application
/// exceptions map to specific statuses; anything else is a 500 with no detail leaked.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = Map(exception);

        if (problem.Status is >= 500)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    private static ProblemDetails Map(Exception exception) => exception switch
    {
        ValidationException ex => new ValidationProblemDetails(
            ex.Errors
                .GroupBy(e => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more fields are invalid.",
        },

        DomainRuleException ex => Problem(StatusCodes.Status400BadRequest, "The request breaks a business rule.", ex.Message),

        AuthenticationFailedException ex => Problem(StatusCodes.Status401Unauthorized, "Sign-in failed.", ex.Message),

        PermissionDeniedException ex => Problem(StatusCodes.Status403Forbidden, "You are not allowed to do that.", ex.Message),

        NotFoundException ex => Problem(StatusCodes.Status404NotFound, "Not found.", ex.Message),

        IllegalTransitionException ex => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "That status change is not allowed.",
            Detail = ex.Message,
            Extensions =
            {
                ["from"] = ex.From.ToString(),
                ["to"] = ex.To.ToString(),
                ["legalTransitions"] = ex.LegalTargets.Select(t => t.ToString()).ToArray(),
            },
        },

        ConcurrencyConflictException ex => Problem(StatusCodes.Status409Conflict, "The request changed underneath you.", ex.Message),

        BadHttpRequestException ex => Problem(ex.StatusCode, "The request could not be read.", ex.Message),

        OperationCanceledException => Problem(StatusCodes.Status499ClientClosedRequest, "The client went away.", null),

        _ => Problem(StatusCodes.Status500InternalServerError, "Something went wrong on our side.", null),
    };

    private static ProblemDetails Problem(int status, string title, string? detail) =>
        new() { Status = status, Title = title, Detail = detail };
}
