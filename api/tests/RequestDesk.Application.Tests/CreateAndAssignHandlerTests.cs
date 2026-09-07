using MediatR;
using NSubstitute;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Application.Requests.Commands;
using RequestDesk.Application.Requests.Queries;
using RequestDesk.Domain.Common;
using RequestDesk.Domain.Requests;
using RequestDesk.Domain.Users;

namespace RequestDesk.Application.Tests;

public class CreateRequestCommandHandlerTests
{
    private readonly IServiceRequestRepository _repository = Substitute.For<IServiceRequestRepository>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly FakeClock _clock = new(Fakes.T0);

    private static readonly RequestDetail AnyDetail = new(
        Guid.Empty, "RD-001000", "t", "d", RequestPriority.Normal, RequestStatus.New,
        new CustomerSummary(Guid.Empty, "c", null), null, new UserSummary(Guid.Empty, "u", UserRole.Agent),
        Fakes.T0, Fakes.T0, [], new RequestPermissions(true, true, false), [], [], []);

    public CreateRequestCommandHandlerTests()
    {
        _customers.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _sender.Send(Arg.Any<GetRequestQuery>(), Arg.Any<CancellationToken>()).Returns(AnyDetail);
    }

    private CreateRequestCommandHandler Handler(Actor actor) =>
        new(_repository, _customers, _unitOfWork, Fakes.CurrentUser(actor), _clock, _sender);

    [Fact]
    public async Task A_customers_request_lands_on_their_own_account_whatever_the_body_says()
    {
        ServiceRequest? added = null;
        _repository.When(r => r.Add(Arg.Any<ServiceRequest>())).Do(call => added = call.Arg<ServiceRequest>());

        await Handler(Fakes.CustomerA).Handle(
            new CreateRequestCommand("Door sticks", "Front door.", RequestPriority.Low, Fakes.CustomerAccountB),
            CancellationToken.None);

        added.ShouldNotBeNull();
        added.CustomerId.ShouldBe(Fakes.CustomerAccountA);
        added.CreatedById.ShouldBe(Fakes.CustomerA.UserId);
        added.Status.ShouldBe(RequestStatus.New);
        added.History.ShouldHaveSingleItem();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<GetRequestQuery>(q => q.Id == added.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Staff_open_the_request_for_the_customer_they_name()
    {
        ServiceRequest? added = null;
        _repository.When(r => r.Add(Arg.Any<ServiceRequest>())).Do(call => added = call.Arg<ServiceRequest>());

        await Handler(Fakes.Agent).Handle(
            new CreateRequestCommand("Door sticks", "Front door.", RequestPriority.Low, Fakes.CustomerAccountB),
            CancellationToken.None);

        added.ShouldNotBeNull();
        added.CustomerId.ShouldBe(Fakes.CustomerAccountB);
    }

    [Fact]
    public async Task Staff_must_name_a_customer_that_exists()
    {
        _customers.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        await Should.ThrowAsync<DomainRuleException>(() => Handler(Fakes.Agent).Handle(
            new CreateRequestCommand("Door sticks", "Front door.", RequestPriority.Low, Guid.NewGuid()),
            CancellationToken.None));

        _repository.DidNotReceive().Add(Arg.Any<ServiceRequest>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

public class AssignRequestCommandHandlerTests
{
    private readonly IUserDirectory _users = Fakes.Directory();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly FakeClock _clock = new(Fakes.T0.AddHours(2));

    private AssignRequestCommandHandler Handler(Actor actor, ServiceRequest request) =>
        new(Fakes.Repository(request), _users, _unitOfWork, Fakes.CurrentUser(actor), _clock, _sender);

    private void UserExists(Guid id, UserRole role, bool active = true)
    {
        var user = AppUser.Create(id, "Someone", $"{id:N}@example.test", role, role == UserRole.Customer ? Fakes.CustomerAccountA : null);

        if (!active)
        {
            user.Deactivate();
        }

        _users.FindAsync(id, Arg.Any<CancellationToken>()).Returns(user);
    }

    [Fact]
    public async Task An_agent_claims_an_unassigned_request()
    {
        var request = Fakes.RequestAt(RequestStatus.Triaged);
        UserExists(Fakes.Agent.UserId, UserRole.Agent);

        await Handler(Fakes.Agent, request).Handle(new AssignRequestCommand(request.Id, Fakes.Agent.UserId), CancellationToken.None);

        request.AssignedAgentId.ShouldBe(Fakes.Agent.UserId);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_assignee_must_be_active_staff()
    {
        var request = Fakes.RequestAt(RequestStatus.Triaged);
        var customerUserId = Guid.NewGuid();
        var inactiveAgentId = Guid.NewGuid();
        UserExists(customerUserId, UserRole.Customer);
        UserExists(inactiveAgentId, UserRole.Agent, active: false);

        await Should.ThrowAsync<DomainRuleException>(() => Handler(Fakes.Admin, request).Handle(new AssignRequestCommand(request.Id, customerUserId), CancellationToken.None));
        await Should.ThrowAsync<DomainRuleException>(() => Handler(Fakes.Admin, request).Handle(new AssignRequestCommand(request.Id, inactiveAgentId), CancellationToken.None));
        await Should.ThrowAsync<DomainRuleException>(() => Handler(Fakes.Admin, request).Handle(new AssignRequestCommand(request.Id, Guid.NewGuid()), CancellationToken.None));

        request.AssignedAgentId.ShouldBeNull();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_agent_may_not_reassign_but_an_admin_may_unassign()
    {
        var request = Fakes.RequestAt(RequestStatus.InProgress);
        var otherAgent = Guid.NewGuid();
        UserExists(Fakes.Agent.UserId, UserRole.Agent);
        UserExists(otherAgent, UserRole.Agent);
        request.Assign(Fakes.Agent.UserId, Fakes.Admin, Fakes.T0);

        await Should.ThrowAsync<PermissionDeniedException>(() => Handler(Fakes.Agent, request).Handle(new AssignRequestCommand(request.Id, otherAgent), CancellationToken.None));

        await Handler(Fakes.Admin, request).Handle(new AssignRequestCommand(request.Id, null), CancellationToken.None);
        request.AssignedAgentId.ShouldBeNull();
    }
}
