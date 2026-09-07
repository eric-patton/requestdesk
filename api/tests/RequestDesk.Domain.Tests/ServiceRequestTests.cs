using RequestDesk.Domain.Common;
using RequestDesk.Domain.Requests;
using static RequestDesk.Domain.Requests.RequestStatus;

namespace RequestDesk.Domain.Tests;

public class ServiceRequestTests
{
    [Fact]
    public void Creating_a_request_starts_it_as_new_and_writes_the_first_history_row()
    {
        var request = RequestBuilder.New();

        request.Status.ShouldBe(New);
        request.IsOpen.ShouldBeTrue();
        request.CustomerId.ShouldBe(TestActors.CustomerAccountA);
        request.CreatedById.ShouldBe(TestActors.CustomerA.UserId);
        request.CreatedAt.ShouldBe(TestActors.T0);
        request.UpdatedAt.ShouldBe(TestActors.T0);
        request.AssignedAgentId.ShouldBeNull();

        var first = request.History.ShouldHaveSingleItem();
        first.RequestId.ShouldBe(request.Id);
        first.FromStatus.ShouldBeNull();
        first.ToStatus.ShouldBe(New);
        first.ActorId.ShouldBe(TestActors.CustomerA.UserId);
        first.Reason.ShouldBe("Request created");
        first.OccurredAt.ShouldBe(TestActors.T0);
    }

    [Fact]
    public void Staff_may_open_a_request_on_behalf_of_any_customer()
    {
        var request = RequestBuilder.New(createdBy: TestActors.Agent, customerId: TestActors.CustomerAccountB);

        request.CustomerId.ShouldBe(TestActors.CustomerAccountB);
        request.CreatedById.ShouldBe(TestActors.Agent.UserId);
    }

    [Fact]
    public void A_customer_may_not_open_a_request_on_another_customers_account()
    {
        var act = () => RequestBuilder.New(createdBy: TestActors.CustomerA, customerId: TestActors.CustomerAccountB);

        act.ShouldThrow<PermissionDeniedException>();
    }

    [Theory]
    [InlineData("", "A description")]
    [InlineData("   ", "A description")]
    [InlineData("A title", "")]
    [InlineData("A title", "   ")]
    public void Title_and_description_are_required(string title, string description)
    {
        var act = () => ServiceRequest.Create(TestActors.CustomerAccountA, title, description, RequestPriority.Normal, TestActors.Agent, TestActors.T0);

        act.ShouldThrow<DomainRuleException>();
    }

    [Fact]
    public void Title_and_description_are_trimmed()
    {
        var request = ServiceRequest.Create(TestActors.CustomerAccountA, "  Door sticks  ", "  Front door.  ", RequestPriority.Low, TestActors.Agent, TestActors.T0);

        request.Title.ShouldBe("Door sticks");
        request.Description.ShouldBe("Front door.");
    }

    [Fact]
    public void Title_and_description_respect_the_shared_limits()
    {
        var longTitle = new string('x', RequestLimits.MaxTitleLength + 1);
        var longDescription = new string('x', RequestLimits.MaxDescriptionLength + 1);

        var tooLongTitle = () => ServiceRequest.Create(TestActors.CustomerAccountA, longTitle, "ok", RequestPriority.Normal, TestActors.Agent, TestActors.T0);
        var tooLongDescription = () => ServiceRequest.Create(TestActors.CustomerAccountA, "ok", longDescription, RequestPriority.Normal, TestActors.Agent, TestActors.T0);

        tooLongTitle.ShouldThrow<DomainRuleException>();
        tooLongDescription.ShouldThrow<DomainRuleException>();
    }

    [Fact]
    public void An_unknown_priority_is_rejected()
    {
        var act = () => ServiceRequest.Create(TestActors.CustomerAccountA, "ok", "ok", (RequestPriority)99, TestActors.Agent, TestActors.T0);

        act.ShouldThrow<DomainRuleException>();
    }

    [Fact]
    public void A_legal_status_change_appends_one_history_row_with_from_to_actor_reason_and_time()
    {
        var request = RequestBuilder.New();

        var entry = request.ChangeStatus(Triaged, TestActors.Agent, "Looks like the rear tray sensor", TestActors.At(5));

        request.Status.ShouldBe(Triaged);
        request.UpdatedAt.ShouldBe(TestActors.At(5));
        request.History.Count.ShouldBe(2);
        request.History.Last().ShouldBeSameAs(entry);
        entry.FromStatus.ShouldBe(New);
        entry.ToStatus.ShouldBe(Triaged);
        entry.ActorId.ShouldBe(TestActors.Agent.UserId);
        entry.Reason.ShouldBe("Looks like the rear tray sensor");
        entry.OccurredAt.ShouldBe(TestActors.At(5));
    }

    [Fact]
    public void A_blank_reason_is_stored_as_null()
    {
        var request = RequestBuilder.New();

        var entry = request.ChangeStatus(Triaged, TestActors.Agent, "   ", TestActors.At(5));

        entry.Reason.ShouldBeNull();
    }

    [Fact]
    public void An_illegal_move_is_refused_with_the_legal_set_and_changes_nothing()
    {
        var request = RequestBuilder.New();

        var ex = Should.Throw<IllegalTransitionException>(() => request.ChangeStatus(Resolved, TestActors.Admin, null, TestActors.At(1)));

        ex.From.ShouldBe(New);
        ex.To.ShouldBe(Resolved);
        ex.LegalTargets.ShouldBe([Triaged, Cancelled]);
        request.Status.ShouldBe(New);
        request.History.Count.ShouldBe(1);
        request.UpdatedAt.ShouldBe(TestActors.T0);
    }

    [Fact]
    public void Legality_is_checked_before_permission_so_everyone_gets_the_same_409_for_an_illegal_move()
    {
        var request = RequestBuilder.New();

        Should.Throw<IllegalTransitionException>(() => request.ChangeStatus(Closed, TestActors.CustomerA, null, TestActors.At(1)));
    }

    [Fact]
    public void A_legal_move_the_actor_may_not_make_is_a_permission_error_and_changes_nothing()
    {
        var request = RequestBuilder.At(Resolved);
        var before = request.History.Count;

        Should.Throw<PermissionDeniedException>(() => request.ChangeStatus(InProgress, TestActors.Agent, "reopen", TestActors.At(30)));

        request.Status.ShouldBe(Resolved);
        request.History.Count.ShouldBe(before);
    }

    [Fact]
    public void An_admin_can_reopen_a_resolved_request()
    {
        var request = RequestBuilder.At(Resolved);

        var entry = request.ChangeStatus(InProgress, TestActors.Admin, "Customer says it is still jamming", TestActors.At(30));

        request.Status.ShouldBe(InProgress);
        entry.FromStatus.ShouldBe(Resolved);
        entry.ToStatus.ShouldBe(InProgress);
    }

    [Fact]
    public void A_customer_can_cancel_their_own_request_but_not_someone_elses()
    {
        var own = RequestBuilder.New();
        var someoneElses = RequestBuilder.New();

        own.ChangeStatus(Cancelled, TestActors.CustomerA, "Fixed it myself", TestActors.At(2));
        own.Status.ShouldBe(Cancelled);

        Should.Throw<PermissionDeniedException>(() => someoneElses.ChangeStatus(Cancelled, TestActors.CustomerB, null, TestActors.At(2)));
        someoneElses.Status.ShouldBe(New);
    }

    [Fact]
    public void A_reason_longer_than_the_limit_is_rejected()
    {
        var request = RequestBuilder.New();
        var reason = new string('r', RequestLimits.MaxReasonLength + 1);

        Should.Throw<DomainRuleException>(() => request.ChangeStatus(Triaged, TestActors.Agent, reason, TestActors.At(1)));
        request.Status.ShouldBe(New);
    }

    [Fact]
    public void A_full_lifecycle_including_a_block_and_a_reopen_appends_exactly_one_row_per_move()
    {
        var request = RequestBuilder.New();
        RequestStatus[] path = [Triaged, InProgress, Blocked, InProgress, Resolved, InProgress, Resolved, Closed];

        for (var i = 0; i < path.Length; i++)
        {
            request.ChangeStatus(path[i], TestActors.Admin, null, TestActors.At(i + 1));
        }

        request.Status.ShouldBe(Closed);
        request.IsOpen.ShouldBeFalse();
        request.History.Count.ShouldBe(path.Length + 1);
        request.History.Select(h => h.ToStatus).ShouldBe([New, .. path]);
        request.History.Skip(1).Select(h => h.FromStatus!.Value).ShouldBe([New, .. path[..^1]]);
        request.History.Select(h => h.OccurredAt).ShouldBeInOrder();
    }

    [Fact]
    public void Nothing_moves_out_of_a_terminal_state()
    {
        foreach (var terminal in new[] { Closed, Cancelled })
        {
            var request = RequestBuilder.At(terminal);

            foreach (var to in Enum.GetValues<RequestStatus>())
            {
                Should.Throw<IllegalTransitionException>(() => request.ChangeStatus(to, TestActors.Admin, null, TestActors.At(99)));
            }

            request.Status.ShouldBe(terminal);
        }
    }

    [Fact]
    public void An_agent_can_claim_an_unassigned_request()
    {
        var request = RequestBuilder.At(Triaged);

        request.Assign(TestActors.Agent.UserId, TestActors.Agent, TestActors.At(10));

        request.AssignedAgentId.ShouldBe(TestActors.Agent.UserId);
        request.UpdatedAt.ShouldBe(TestActors.At(10));
    }

    [Fact]
    public void An_agent_cannot_reassign_a_request_that_already_has_an_agent_but_an_admin_can()
    {
        var request = RequestBuilder.At(Triaged);
        request.Assign(TestActors.Agent.UserId, TestActors.Agent, TestActors.At(10));

        Should.Throw<PermissionDeniedException>(() => request.Assign(TestActors.OtherAgent.UserId, TestActors.Agent, TestActors.At(11)));
        request.AssignedAgentId.ShouldBe(TestActors.Agent.UserId);

        request.Assign(TestActors.OtherAgent.UserId, TestActors.Admin, TestActors.At(12));
        request.AssignedAgentId.ShouldBe(TestActors.OtherAgent.UserId);

        request.Assign(null, TestActors.Admin, TestActors.At(13));
        request.AssignedAgentId.ShouldBeNull();
    }

    [Fact]
    public void Assigning_the_same_agent_again_is_a_no_op()
    {
        var request = RequestBuilder.At(Triaged);
        request.Assign(TestActors.Agent.UserId, TestActors.Admin, TestActors.At(10));

        request.Assign(TestActors.Agent.UserId, TestActors.Admin, TestActors.At(20));

        request.UpdatedAt.ShouldBe(TestActors.At(10));
    }

    [Fact]
    public void A_terminal_request_cannot_be_reassigned()
    {
        var request = RequestBuilder.At(Closed);

        Should.Throw<PermissionDeniedException>(() => request.Assign(TestActors.Agent.UserId, TestActors.Admin, TestActors.At(99)));
    }

    [Fact]
    public void Comments_record_the_author_and_time_and_are_trimmed()
    {
        var request = RequestBuilder.New();

        var comment = request.AddComment("  On my way up.  ", TestActors.Agent, TestActors.At(3));

        request.Comments.ShouldHaveSingleItem().ShouldBeSameAs(comment);
        comment.RequestId.ShouldBe(request.Id);
        comment.Body.ShouldBe("On my way up.");
        comment.AuthorId.ShouldBe(TestActors.Agent.UserId);
        comment.CreatedAt.ShouldBe(TestActors.At(3));
        request.UpdatedAt.ShouldBe(TestActors.At(3));
    }

    [Fact]
    public void A_blank_or_oversized_comment_is_rejected()
    {
        var request = RequestBuilder.New();

        Should.Throw<DomainRuleException>(() => request.AddComment("   ", TestActors.Agent, TestActors.At(3)));
        Should.Throw<DomainRuleException>(() => request.AddComment(new string('c', RequestLimits.MaxCommentLength + 1), TestActors.Agent, TestActors.At(3)));
        request.Comments.ShouldBeEmpty();
    }

    [Fact]
    public void A_customer_from_another_account_cannot_comment()
    {
        var request = RequestBuilder.New();

        Should.Throw<PermissionDeniedException>(() => request.AddComment("Me too", TestActors.CustomerB, TestActors.At(3)));
    }

    [Fact]
    public void Attachments_record_their_metadata_and_a_storage_key()
    {
        var request = RequestBuilder.New();

        var attachment = request.AddAttachment("jam.jpg", "image/jpeg", 48_213, "2026/09/0f3c", TestActors.CustomerA, TestActors.At(4));

        request.Attachments.ShouldHaveSingleItem().ShouldBeSameAs(attachment);
        attachment.FileName.ShouldBe("jam.jpg");
        attachment.ContentType.ShouldBe("image/jpeg");
        attachment.SizeBytes.ShouldBe(48_213);
        attachment.StorageKey.ShouldBe("2026/09/0f3c");
        attachment.UploadedById.ShouldBe(TestActors.CustomerA.UserId);
    }

    [Fact]
    public void An_empty_attachment_is_rejected()
    {
        var request = RequestBuilder.New();

        Should.Throw<DomainRuleException>(() => request.AddAttachment("empty.txt", "text/plain", 0, "key", TestActors.Agent, TestActors.At(4)));
    }

    [Theory]
    [InlineData(Closed)]
    [InlineData(Cancelled)]
    public void Attachments_cannot_be_added_to_a_terminal_request(RequestStatus status)
    {
        var request = RequestBuilder.At(status);

        Should.Throw<PermissionDeniedException>(() => request.AddAttachment("late.txt", "text/plain", 10, "key", TestActors.Admin, TestActors.At(99)));
    }
}
