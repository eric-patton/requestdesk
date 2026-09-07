using MediatR;
using RequestDesk.Application.Common;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Application.Requests.Queries;
using RequestDesk.Domain.Common;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Application.Requests.Commands;

/// <summary>Set or clear (null) the assigned agent. Who may do that is decided in <see cref="RequestPolicy"/>.</summary>
public sealed record AssignRequestCommand(Guid Id, Guid? AgentId) : IRequest<RequestDetail>;

internal sealed class AssignRequestCommandHandler(
    IServiceRequestRepository requests,
    IUserDirectory users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock,
    ISender sender)
    : IRequestHandler<AssignRequestCommand, RequestDetail>
{
    public async Task<RequestDetail> Handle(AssignRequestCommand command, CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;
        var request = await requests.GetAsync(command.Id, cancellationToken);

        if (request is null || !RequestPolicy.CanView(request, actor))
        {
            throw new NotFoundException("Request", command.Id);
        }

        if (command.AgentId is { } agentId)
        {
            var agent = await users.FindAsync(agentId, cancellationToken);

            if (agent is null || !agent.IsActive || !agent.AsActor().IsStaff)
            {
                throw new DomainRuleException("The assignee must be an active agent or admin.");
            }
        }

        request.Assign(command.AgentId, actor, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await sender.Send(new GetRequestQuery(request.Id), cancellationToken);
    }
}
