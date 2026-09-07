using FluentValidation;
using MediatR;
using RequestDesk.Application.Common;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Application.Requests.Commands;

/// <summary>
/// Move a request to a new status. The aggregate enforces the state machine and the role rules;
/// an illegal move surfaces as a 409 carrying the legal targets, a forbidden one as a 403.
/// </summary>
public sealed record ChangeStatusCommand(Guid Id, RequestStatus To, string? Reason) : IRequest<StatusChangeResult>;

internal sealed class ChangeStatusCommandValidator : AbstractValidator<ChangeStatusCommand>
{
    public ChangeStatusCommandValidator()
    {
        RuleFor(c => c.To).IsInEnum();
        RuleFor(c => c.Reason).MaximumLength(RequestLimits.MaxReasonLength);
    }
}

internal sealed class ChangeStatusCommandHandler(
    IServiceRequestRepository requests,
    IUserDirectory users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock)
    : IRequestHandler<ChangeStatusCommand, StatusChangeResult>
{
    public async Task<StatusChangeResult> Handle(ChangeStatusCommand command, CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;
        var request = await requests.GetAsync(command.Id, cancellationToken);

        if (request is null || !RequestPolicy.CanView(request, actor))
        {
            throw new NotFoundException("Request", command.Id);
        }

        var entry = request.ChangeStatus(command.To, actor, command.Reason, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var names = await users.GetSummariesAsync([actor.UserId], cancellationToken);

        return new StatusChangeResult(
            request.Id,
            request.Status,
            RequestMapper.ToHistory(entry, names),
            RequestPolicy.AllowedTransitions(request, actor),
            request.UpdatedAt);
    }
}
