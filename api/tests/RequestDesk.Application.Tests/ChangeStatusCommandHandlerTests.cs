using NSubstitute;
using RequestDesk.Application.Common;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Requests.Commands;
using RequestDesk.Domain.Common;
using RequestDesk.Domain.Requests;
using RequestDesk.Domain.Users;

namespace RequestDesk.Application.Tests;

public class ChangeStatusCommandHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeClock _clock = new(Fakes.T0.AddHours(1));

    private ChangeStatusCommandHandler Handler(Actor actor, params ServiceRequest[] requests) =>
        new(Fakes.Repository(requests), Fakes.Directory(), _unitOfWork, Fakes.CurrentUser(actor), _clock);

    [Fact]
    public async Task A_legal_move_saves_once_and_returns_the_new_history_entry_and_the_next_legal_moves()
    {
        var request = Fakes.Request();
        var handler = Handler(Fakes.Agent, request);

        var result = await handler.Handle(new ChangeStatusCommand(request.Id, RequestStatus.Triaged, "Looks straightforward"), CancellationToken.None);

        result.Id.ShouldBe(request.Id);
        result.Status.ShouldBe(RequestStatus.Triaged);
        result.Entry.FromStatus.ShouldBe(RequestStatus.New);
        result.Entry.ToStatus.ShouldBe(RequestStatus.Triaged);
        result.Entry.Actor.Id.ShouldBe(Fakes.Agent.UserId);
        result.Entry.Reason.ShouldBe("Looks straightforward");
        result.Entry.OccurredAt.ShouldBe(_clock.Now);
        result.AllowedTransitions.ShouldBe([RequestStatus.InProgress, RequestStatus.Cancelled]);
        result.UpdatedAt.ShouldBe(_clock.Now);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_illegal_move_surfaces_the_domain_exception_with_the_legal_set_and_saves_nothing()
    {
        var request = Fakes.Request();
        var handler = Handler(Fakes.Admin, request);

        var ex = await Should.ThrowAsync<IllegalTransitionException>(() =>
            handler.Handle(new ChangeStatusCommand(request.Id, RequestStatus.Closed, null), CancellationToken.None));

        ex.LegalTargets.ShouldBe([RequestStatus.Triaged, RequestStatus.Cancelled]);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_legal_move_the_role_may_not_make_is_a_permission_error_and_saves_nothing()
    {
        var request = Fakes.RequestAt(RequestStatus.Resolved);
        var handler = Handler(Fakes.Agent, request);

        await Should.ThrowAsync<PermissionDeniedException>(() =>
            handler.Handle(new ChangeStatusCommand(request.Id, RequestStatus.InProgress, "reopen"), CancellationToken.None));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_request_is_not_found()
    {
        var handler = Handler(Fakes.Admin);

        await Should.ThrowAsync<NotFoundException>(() =>
            handler.Handle(new ChangeStatusCommand(Guid.NewGuid(), RequestStatus.Triaged, null), CancellationToken.None));
    }

    [Fact]
    public async Task Another_customers_request_is_reported_as_not_found_rather_than_forbidden()
    {
        var request = Fakes.Request();
        var handler = Handler(Fakes.CustomerB, request);

        await Should.ThrowAsync<NotFoundException>(() =>
            handler.Handle(new ChangeStatusCommand(request.Id, RequestStatus.Cancelled, null), CancellationToken.None));

        request.Status.ShouldBe(RequestStatus.New);
    }

    [Fact]
    public async Task The_owning_customer_can_cancel()
    {
        var request = Fakes.Request();
        var handler = Handler(Fakes.CustomerA, request);

        var result = await handler.Handle(new ChangeStatusCommand(request.Id, RequestStatus.Cancelled, "Fixed it myself"), CancellationToken.None);

        result.Status.ShouldBe(RequestStatus.Cancelled);
        result.AllowedTransitions.ShouldBeEmpty();
    }
}
