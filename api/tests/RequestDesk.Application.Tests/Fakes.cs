using NSubstitute;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;
using RequestDesk.Domain.Users;

namespace RequestDesk.Application.Tests;

/// <summary>A clock that only moves when a test moves it.</summary>
internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class Fakes
{
    public static readonly Guid CustomerAccountA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid CustomerAccountB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    public static readonly Actor Admin = new(Guid.Parse("00000000-0000-0000-0000-00000000ad01"), UserRole.Admin, null);
    public static readonly Actor Agent = new(Guid.Parse("00000000-0000-0000-0000-00000000a601"), UserRole.Agent, null);
    public static readonly Actor CustomerA = new(Guid.Parse("00000000-0000-0000-0000-00000000c0a1"), UserRole.Customer, CustomerAccountA);
    public static readonly Actor CustomerB = new(Guid.Parse("00000000-0000-0000-0000-00000000c0b1"), UserRole.Customer, CustomerAccountB);

    public static readonly DateTimeOffset T0 = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    public static ICurrentUser CurrentUser(Actor actor)
    {
        var current = Substitute.For<ICurrentUser>();
        current.IsAuthenticated.Returns(true);
        current.Actor.Returns(actor);
        return current;
    }

    public static ICurrentUser Anonymous()
    {
        var current = Substitute.For<ICurrentUser>();
        current.IsAuthenticated.Returns(false);
        return current;
    }

    /// <summary>A request on customer account A, created by customer A.</summary>
    public static ServiceRequest Request() =>
        ServiceRequest.Create(CustomerAccountA, "Printer jamming", "Rear tray, every second page.", RequestPriority.Normal, CustomerA, T0);

    public static ServiceRequest RequestAt(RequestStatus status)
    {
        var request = Request();
        RequestStatus[] path = status switch
        {
            RequestStatus.New => [],
            RequestStatus.Triaged => [RequestStatus.Triaged],
            RequestStatus.InProgress => [RequestStatus.Triaged, RequestStatus.InProgress],
            RequestStatus.Blocked => [RequestStatus.Triaged, RequestStatus.InProgress, RequestStatus.Blocked],
            RequestStatus.Resolved => [RequestStatus.Triaged, RequestStatus.InProgress, RequestStatus.Resolved],
            RequestStatus.Closed => [RequestStatus.Triaged, RequestStatus.InProgress, RequestStatus.Resolved, RequestStatus.Closed],
            RequestStatus.Cancelled => [RequestStatus.Cancelled],
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        var minute = 1;

        foreach (var step in path)
        {
            request.ChangeStatus(step, Admin, null, T0.AddMinutes(minute++));
        }

        return request;
    }

    public static IServiceRequestRepository Repository(params ServiceRequest[] requests)
    {
        var repository = Substitute.For<IServiceRequestRepository>();

        repository.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(requests.FirstOrDefault(r => r.Id == call.Arg<Guid>())));

        return repository;
    }

    public static IUserDirectory Directory()
    {
        var directory = Substitute.For<IUserDirectory>();

        directory.GetSummariesAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyDictionary<Guid, UserSummary>>(
                call.Arg<IEnumerable<Guid>>().Distinct().ToDictionary(id => id, id => new UserSummary(id, $"User {id.ToString()[^4..]}", UserRole.Agent))));

        return directory;
    }
}
