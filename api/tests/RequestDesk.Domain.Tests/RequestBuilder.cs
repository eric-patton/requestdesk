using RequestDesk.Domain.Requests;
using RequestDesk.Domain.Users;

namespace RequestDesk.Domain.Tests;

/// <summary>Builds a request owned by customer account A and drives it to a given status along a legal path.</summary>
internal static class RequestBuilder
{
    public static ServiceRequest New(Actor? createdBy = null, Guid? customerId = null) =>
        ServiceRequest.Create(
            customerId ?? TestActors.CustomerAccountA,
            "Printer on the third floor is jamming",
            "Every second page jams in the rear tray. Started after the toner change on Monday.",
            RequestPriority.Normal,
            createdBy ?? TestActors.CustomerA,
            TestActors.T0);

    /// <summary>
    /// Walks the shortest legal path from New to <paramref name="target"/> as the admin, so the
    /// test that follows is about the final step and nothing else.
    /// </summary>
    public static ServiceRequest At(RequestStatus target)
    {
        var request = New();
        var path = PathTo(target);
        var minute = 1;

        foreach (var step in path)
        {
            request.ChangeStatus(step, TestActors.Admin, $"step to {step}", TestActors.At(minute++));
        }

        request.Status.ShouldBe(target);
        return request;
    }

    private static RequestStatus[] PathTo(RequestStatus target) => target switch
    {
        RequestStatus.New => [],
        RequestStatus.Triaged => [RequestStatus.Triaged],
        RequestStatus.InProgress => [RequestStatus.Triaged, RequestStatus.InProgress],
        RequestStatus.Blocked => [RequestStatus.Triaged, RequestStatus.InProgress, RequestStatus.Blocked],
        RequestStatus.Resolved => [RequestStatus.Triaged, RequestStatus.InProgress, RequestStatus.Resolved],
        RequestStatus.Closed => [RequestStatus.Triaged, RequestStatus.InProgress, RequestStatus.Resolved, RequestStatus.Closed],
        RequestStatus.Cancelled => [RequestStatus.Cancelled],
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };
}
