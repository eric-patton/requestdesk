using RequestDesk.Domain.Requests;
using RequestDesk.Domain.Users;
using static RequestDesk.Domain.Requests.RequestStatus;

namespace RequestDesk.Domain.Tests;

/// <summary>
/// Who may make which legal move. Every status is crossed with every role, and the customer case
/// is split into own request and someone else's.
/// </summary>
public class RequestPolicyTests
{
    public static TheoryData<RequestStatus, UserRole> EveryStatusAndRole()
    {
        var data = new TheoryData<RequestStatus, UserRole>();

        foreach (var status in Enum.GetValues<RequestStatus>())
        {
            foreach (var role in Enum.GetValues<UserRole>())
            {
                data.Add(status, role);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryStatusAndRole))]
    public void Allowed_transitions_are_the_legal_set_narrowed_by_role(RequestStatus status, UserRole role)
    {
        var request = RequestBuilder.At(status);
        var legal = RequestStatusMachine.LegalTargets(status);

        var expected = role switch
        {
            UserRole.Admin => legal,
            UserRole.Agent => legal.Where(to => !(status == Resolved && to == InProgress)).ToArray(),
            UserRole.Customer => legal.Where(to => to == Cancelled).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(role)),
        };

        RequestPolicy.AllowedTransitions(request, TestActors.For(role)).ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(EveryStatusAndRole))]
    public void Allowed_transitions_are_always_a_subset_of_the_legal_set(RequestStatus status, UserRole role)
    {
        var request = RequestBuilder.At(status);

        var allowed = RequestPolicy.AllowedTransitions(request, TestActors.For(role));

        allowed.ShouldBeSubsetOf(RequestStatusMachine.LegalTargets(status));
    }

    [Fact]
    public void Admin_is_the_only_role_that_may_reopen()
    {
        var request = RequestBuilder.At(Resolved);

        RequestPolicy.CanTransition(request, TestActors.Admin, InProgress).ShouldBeTrue();
        RequestPolicy.CanTransition(request, TestActors.Agent, InProgress).ShouldBeFalse();
        RequestPolicy.CanTransition(request, TestActors.CustomerA, InProgress).ShouldBeFalse();
    }

    [Theory]
    [InlineData(New)]
    [InlineData(Triaged)]
    [InlineData(Blocked)]
    public void A_customer_may_cancel_their_own_request_from_any_status_that_allows_it(RequestStatus status)
    {
        var request = RequestBuilder.At(status);

        RequestPolicy.AllowedTransitions(request, TestActors.CustomerA).ShouldBe([Cancelled]);
    }

    [Theory]
    [InlineData(InProgress)]
    [InlineData(Resolved)]
    [InlineData(Closed)]
    [InlineData(Cancelled)]
    public void A_customer_has_no_moves_where_cancelling_is_not_legal(RequestStatus status)
    {
        var request = RequestBuilder.At(status);

        RequestPolicy.AllowedTransitions(request, TestActors.CustomerA).ShouldBeEmpty();
    }

    public static TheoryData<RequestStatus> EveryStatus() => new(Enum.GetValues<RequestStatus>());

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void A_customer_from_another_account_cannot_see_or_do_anything(RequestStatus status)
    {
        var request = RequestBuilder.At(status);

        RequestPolicy.CanView(request, TestActors.CustomerB).ShouldBeFalse();
        RequestPolicy.AllowedTransitions(request, TestActors.CustomerB).ShouldBeEmpty();
        RequestPolicy.CanComment(request, TestActors.CustomerB).ShouldBeFalse();
        RequestPolicy.CanAttach(request, TestActors.CustomerB).ShouldBeFalse();
        RequestPolicy.CanChangeAssignment(request, TestActors.CustomerB).ShouldBeFalse();
    }

    [Fact]
    public void Staff_can_see_every_request_and_the_owning_customer_can_see_theirs()
    {
        var request = RequestBuilder.New();

        RequestPolicy.CanView(request, TestActors.Admin).ShouldBeTrue();
        RequestPolicy.CanView(request, TestActors.Agent).ShouldBeTrue();
        RequestPolicy.CanView(request, TestActors.CustomerA).ShouldBeTrue();
        RequestPolicy.CanView(request, TestActors.CustomerB).ShouldBeFalse();
    }

    [Fact]
    public void Anyone_who_can_see_a_request_can_comment_on_it_even_after_it_closes()
    {
        var request = RequestBuilder.At(Closed);

        RequestPolicy.CanComment(request, TestActors.Admin).ShouldBeTrue();
        RequestPolicy.CanComment(request, TestActors.Agent).ShouldBeTrue();
        RequestPolicy.CanComment(request, TestActors.CustomerA).ShouldBeTrue();
    }

    [Theory]
    [InlineData(Closed)]
    [InlineData(Cancelled)]
    public void Nobody_can_attach_files_to_or_reassign_a_terminal_request(RequestStatus status)
    {
        var request = RequestBuilder.At(status);

        foreach (var actor in new[] { TestActors.Admin, TestActors.Agent, TestActors.CustomerA })
        {
            RequestPolicy.CanAttach(request, actor).ShouldBeFalse(actor.Role.ToString());
            RequestPolicy.CanChangeAssignment(request, actor).ShouldBeFalse(actor.Role.ToString());
            RequestPolicy.CanAssign(request, actor, TestActors.Agent.UserId).ShouldBeFalse(actor.Role.ToString());
        }
    }

    [Fact]
    public void An_agent_may_claim_an_unassigned_request_but_not_unassign_it()
    {
        var request = RequestBuilder.At(Triaged);

        RequestPolicy.CanChangeAssignment(request, TestActors.Agent).ShouldBeTrue();
        RequestPolicy.CanAssign(request, TestActors.Agent, TestActors.Agent.UserId).ShouldBeTrue();
        RequestPolicy.CanAssign(request, TestActors.Agent, TestActors.OtherAgent.UserId).ShouldBeTrue();
        RequestPolicy.CanAssign(request, TestActors.Agent, null).ShouldBeFalse("clearing an assignment is an admin action");
    }

    [Fact]
    public void An_agent_may_not_reassign_a_request_that_already_has_an_agent()
    {
        var request = RequestBuilder.At(Triaged);
        request.Assign(TestActors.Agent.UserId, TestActors.Admin, TestActors.At(10));

        RequestPolicy.CanChangeAssignment(request, TestActors.Agent).ShouldBeFalse();
        RequestPolicy.CanAssign(request, TestActors.Agent, TestActors.OtherAgent.UserId).ShouldBeFalse();
        RequestPolicy.CanAssign(request, TestActors.Agent, null).ShouldBeFalse();
    }

    [Fact]
    public void An_admin_may_assign_reassign_and_unassign_any_open_request()
    {
        var request = RequestBuilder.At(InProgress);

        RequestPolicy.CanAssign(request, TestActors.Admin, TestActors.Agent.UserId).ShouldBeTrue();
        request.Assign(TestActors.Agent.UserId, TestActors.Admin, TestActors.At(10));

        RequestPolicy.CanAssign(request, TestActors.Admin, TestActors.OtherAgent.UserId).ShouldBeTrue();
        RequestPolicy.CanAssign(request, TestActors.Admin, null).ShouldBeTrue();
        RequestPolicy.CanChangeAssignment(request, TestActors.Admin).ShouldBeTrue();
    }

    [Fact]
    public void A_customer_can_never_change_the_assignment()
    {
        var request = RequestBuilder.At(Triaged);

        RequestPolicy.CanChangeAssignment(request, TestActors.CustomerA).ShouldBeFalse();
        RequestPolicy.CanAssign(request, TestActors.CustomerA, TestActors.Agent.UserId).ShouldBeFalse();
    }
}
