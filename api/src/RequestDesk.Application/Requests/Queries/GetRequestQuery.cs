using MediatR;
using RequestDesk.Application.Common;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Application.Requests.Queries;

public sealed record GetRequestQuery(Guid Id) : IRequest<RequestDetail>;

internal sealed class GetRequestQueryHandler(
    IServiceRequestRepository requests,
    IUserDirectory users,
    ICurrentUser currentUser)
    : IRequestHandler<GetRequestQuery, RequestDetail>
{
    public async Task<RequestDetail> Handle(GetRequestQuery query, CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;
        var request = await requests.GetAsync(query.Id, cancellationToken);

        // A request the caller may not see is reported as missing, not forbidden.
        if (request is null || !RequestPolicy.CanView(request, actor))
        {
            throw new NotFoundException("Request", query.Id);
        }

        var names = await users.GetSummariesAsync(RequestMapper.UserIdsIn(request), cancellationToken);
        return RequestMapper.ToDetail(request, actor, names);
    }
}
