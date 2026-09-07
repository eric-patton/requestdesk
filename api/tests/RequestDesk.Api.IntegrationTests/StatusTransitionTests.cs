using System.Net;
using System.Net.Http.Json;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;
using static RequestDesk.Domain.Requests.RequestStatus;

namespace RequestDesk.Api.IntegrationTests;

/// <summary>
/// The state machine, proven over real HTTP against a real database. The domain tests say what the
/// rules are; these say the API actually enforces them and the history rows actually land.
/// </summary>
[Collection(ApiCollection.Name)]
public class StatusTransitionTests(ApiFactory factory)
{
    public static TheoryData<RequestStatus, RequestStatus> EveryPair()
    {
        var data = new TheoryData<RequestStatus, RequestStatus>();

        foreach (var from in Enum.GetValues<RequestStatus>())
        {
            foreach (var to in Enum.GetValues<RequestStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public async Task Every_pair_is_either_200_with_a_history_row_or_409_with_the_legal_set(RequestStatus from, RequestStatus to)
    {
        var admin = await factory.AdminAsync();
        var request = await Api.CreateRequestAsync(admin, await Api.FirstCustomerIdAsync(admin), $"matrix {from} -> {to}");
        await Api.DriveToAsync(admin, request.Id, from);
        var before = (await Api.GetRequestAsync(admin, request.Id))!;
        before.Status.ShouldBe(from);

        var response = await Api.ChangeStatusAsync(admin, request.Id, to, "matrix");
        var after = (await Api.GetRequestAsync(admin, request.Id))!;

        if (RequestStatusMachine.CanTransition(from, to))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var result = (await response.ReadAsync<StatusChangeResult>())!;
            result.Status.ShouldBe(to);
            result.Entry.FromStatus.ShouldBe(from);
            result.Entry.ToStatus.ShouldBe(to);
            result.AllowedTransitions.ShouldBe(RequestStatusMachine.LegalTargets(to));
            after.Status.ShouldBe(to);
            after.History.Count.ShouldBe(before.History.Count + 1);
            after.History.Last().FromStatus.ShouldBe(from);
            after.History.Last().ToStatus.ShouldBe(to);
            after.History.Last().Reason.ShouldBe("matrix");
        }
        else
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            var problem = await response.ReadJsonAsync();
            problem.GetProperty("status").GetInt32().ShouldBe(409);
            problem.GetProperty("from").GetString().ShouldBe(from.ToString());
            problem.GetProperty("to").GetString().ShouldBe(to.ToString());
            problem.GetProperty("legalTransitions").EnumerateArray().Select(e => e.GetString())
                .ShouldBe(RequestStatusMachine.LegalTargets(from).Select(s => s.ToString()));
            after.Status.ShouldBe(from);
            after.History.Count.ShouldBe(before.History.Count);
        }
    }

    [Fact]
    public async Task Creating_a_request_writes_the_first_history_row_and_assigns_a_reference_number()
    {
        var customer = await factory.CustomerAsync();

        var request = await Api.CreateRequestAsync(customer);

        request.ReferenceNumber.ShouldMatch(@"^RD-\d{6}$");
        request.Status.ShouldBe(New);
        var first = request.History.ShouldHaveSingleItem();
        first.FromStatus.ShouldBeNull();
        first.ToStatus.ShouldBe(New);
        first.Reason.ShouldBe("Request created");
        request.AllowedTransitions.ShouldBe([Cancelled]);
        request.Permissions.CanComment.ShouldBeTrue();
        request.Permissions.CanAttach.ShouldBeTrue();
        request.Permissions.CanChangeAssignment.ShouldBeFalse();
    }

    [Fact]
    public async Task Reference_numbers_are_unique_and_increasing()
    {
        var agent = await factory.AgentAsync();
        var customerId = await Api.FirstCustomerIdAsync(agent);

        var first = await Api.CreateRequestAsync(agent, customerId);
        var second = await Api.CreateRequestAsync(agent, customerId);

        second.ReferenceNumber.ShouldNotBe(first.ReferenceNumber);
        string.CompareOrdinal(second.ReferenceNumber, first.ReferenceNumber).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task An_agent_cannot_reopen_but_an_admin_can_and_the_api_tells_each_what_they_may_do()
    {
        var admin = await factory.AdminAsync();
        var agent = await factory.AgentAsync();
        var request = await Api.CreateRequestAsync(admin, await Api.FirstCustomerIdAsync(admin));
        await Api.DriveToAsync(admin, request.Id, Resolved);

        (await Api.GetRequestAsync(agent, request.Id))!.AllowedTransitions.ShouldBe([Closed]);
        (await Api.GetRequestAsync(admin, request.Id))!.AllowedTransitions.ShouldBe([InProgress, Closed]);

        var refused = await Api.ChangeStatusAsync(agent, request.Id, InProgress, "reopen");
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var reopened = await Api.ChangeStatusAsync(admin, request.Id, InProgress, "reopen");
        reopened.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Api.GetRequestAsync(admin, request.Id))!.Status.ShouldBe(InProgress);
    }

    [Fact]
    public async Task A_customer_can_cancel_their_own_request_and_cannot_see_anyone_elses()
    {
        var customer = await factory.CustomerAsync();
        var agent = await factory.AgentAsync();
        var own = await Api.CreateRequestAsync(customer);

        // Someone else's: a request the agent opens for a different customer account.
        var customers = await agent.GetFromJsonAsync<IReadOnlyList<CustomerSummary>>("/api/customers", ApiFactory.Json);
        var otherCustomerId = customers!.Select(c => c.Id).First(id => id != own.Customer.Id);
        var someoneElses = await Api.CreateRequestAsync(agent, otherCustomerId);

        var cancelled = await Api.ChangeStatusAsync(customer, own.Id, Cancelled, "Sorted it ourselves");
        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);

        var hidden = await customer.GetAsync($"/api/requests/{someoneElses.Id}");
        hidden.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var refused = await Api.ChangeStatusAsync(customer, someoneElses.Id, Cancelled);
        refused.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Api.GetRequestAsync(agent, someoneElses.Id))!.Status.ShouldBe(New);
    }

    [Fact]
    public async Task A_status_change_needs_a_signed_in_user()
    {
        var anonymous = factory.CreateClient();

        var response = await Api.ChangeStatusAsync(anonymous, Guid.NewGuid(), Triaged);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_status_value_is_a_400_not_a_500()
    {
        var admin = await factory.AdminAsync();
        var request = await Api.CreateRequestAsync(admin, await Api.FirstCustomerIdAsync(admin));

        var response = await admin.PatchAsync(
            $"/api/requests/{request.Id}/status",
            new StringContent("""{"to":"Teleported"}""", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("status").GetInt32().ShouldBe(400);
    }
}
