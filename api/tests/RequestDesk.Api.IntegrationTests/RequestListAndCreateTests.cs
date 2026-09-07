using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class RequestListTests(ApiFactory factory)
{
    [Fact]
    public async Task Paging_happens_on_the_server()
    {
        var admin = await factory.AdminAsync();

        var page = (await admin.GetFromJsonAsync<PagedResult<RequestListItem>>("/api/requests?page=1&pageSize=5", ApiFactory.Json))!;

        page.Items.Count.ShouldBe(5);
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(5);
        page.TotalCount.ShouldBeGreaterThan(5);
        page.TotalPages.ShouldBe((int)Math.Ceiling(page.TotalCount / 5.0));

        var last = (await admin.GetFromJsonAsync<PagedResult<RequestListItem>>($"/api/requests?page={page.TotalPages}&pageSize=5", ApiFactory.Json))!;
        last.Items.Count.ShouldBeInRange(1, 5);
        last.Items.Select(i => i.Id).ShouldNotContain(page.Items[0].Id);
    }

    [Fact]
    public async Task The_default_sort_is_most_recently_updated_first()
    {
        var admin = await factory.AdminAsync();

        var page = (await admin.GetFromJsonAsync<PagedResult<RequestListItem>>("/api/requests?pageSize=50", ApiFactory.Json))!;

        page.Items.Select(i => i.UpdatedAt).ShouldBeInOrder(SortDirection.Descending);
    }

    [Fact]
    public async Task A_customer_only_ever_sees_their_own_account()
    {
        var customer = await factory.CustomerAsync();
        await Api.CreateRequestAsync(customer, title: "mine");
        var own = (await Api.GetRequestAsync(customer, (await Api.CreateRequestAsync(customer)).Id))!.Customer.Id;

        var page = (await customer.GetFromJsonAsync<PagedResult<RequestListItem>>("/api/requests?pageSize=100", ApiFactory.Json))!;

        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldAllBe(i => i.CustomerId == own);

        await using var scope = factory.CreateScope();
        var db = factory.CreateDbContext(scope);
        page.TotalCount.ShouldBe(await db.Requests.CountAsync(r => r.CustomerId == own));
    }

    [Fact]
    public async Task Filters_narrow_by_status_and_priority_and_can_repeat()
    {
        var admin = await factory.AdminAsync();

        var blocked = (await admin.GetFromJsonAsync<PagedResult<RequestListItem>>("/api/requests?status=Blocked&pageSize=100", ApiFactory.Json))!;
        blocked.Items.ShouldAllBe(i => i.Status == RequestStatus.Blocked);

        var two = (await admin.GetFromJsonAsync<PagedResult<RequestListItem>>("/api/requests?status=New&status=Triaged&priority=Urgent&pageSize=100", ApiFactory.Json))!;
        two.Items.ShouldAllBe(i => (i.Status == RequestStatus.New || i.Status == RequestStatus.Triaged) && i.Priority == RequestPriority.Urgent);
    }

    [Fact]
    public async Task Search_matches_the_reference_number_case_insensitively()
    {
        var admin = await factory.AdminAsync();
        var created = await Api.CreateRequestAsync(admin, await Api.FirstCustomerIdAsync(admin), "searchable needle");

        var byReference = (await admin.GetFromJsonAsync<PagedResult<RequestListItem>>($"/api/requests?search={created.ReferenceNumber.ToLowerInvariant()}", ApiFactory.Json))!;
        byReference.Items.ShouldHaveSingleItem().Id.ShouldBe(created.Id);

        var byTitle = (await admin.GetFromJsonAsync<PagedResult<RequestListItem>>("/api/requests?search=NEEDLE", ApiFactory.Json))!;
        byTitle.Items.Select(i => i.Id).ShouldContain(created.Id);
    }

    [Fact]
    public async Task Like_wildcards_in_the_search_are_treated_as_text()
    {
        var admin = await factory.AdminAsync();

        var page = (await admin.GetFromJsonAsync<PagedResult<RequestListItem>>("/api/requests?search=%25", ApiFactory.Json))!;

        page.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Unassigned_and_assignee_filters_work()
    {
        var admin = await factory.AdminAsync();
        var staff = (await admin.GetFromJsonAsync<IReadOnlyList<UserSummary>>("/api/staff", ApiFactory.Json))!;
        var agent = staff.First(s => s.Role == Domain.Users.UserRole.Agent);

        var unassigned = (await admin.GetFromJsonAsync<PagedResult<RequestListItem>>("/api/requests?unassigned=true&pageSize=100", ApiFactory.Json))!;
        unassigned.Items.ShouldAllBe(i => i.AssignedAgentId == null);

        var theirs = (await admin.GetFromJsonAsync<PagedResult<RequestListItem>>($"/api/requests?assignedAgentId={agent.Id}&pageSize=100", ApiFactory.Json))!;
        theirs.Items.ShouldAllBe(i => i.AssignedAgentId == agent.Id && i.AssignedAgentName == agent.DisplayName);
    }

    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("sortBy=description", "sortBy")]
    public async Task Bad_paging_and_sorting_parameters_are_400_with_the_field_named(string query, string field)
    {
        var admin = await factory.AdminAsync();

        var response = await admin.GetAsync($"/api/requests?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.ReadJsonAsync();
        problem.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
    }
}

[Collection(ApiCollection.Name)]
public class CreateRequestTests(ApiFactory factory)
{
    [Fact]
    public async Task Creating_returns_201_with_a_location_header()
    {
        var customer = await factory.CustomerAsync();

        var response = await customer.PostAsJsonAsync(
            "/api/requests",
            new { title = "Door sticks", description = "Front door.", priority = "Low" },
            ApiFactory.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var detail = (await response.ReadAsync<RequestDetail>())!;
        response.Headers.Location!.ToString().ShouldEndWith($"/api/requests/{detail.Id}");
    }

    [Fact]
    public async Task Validation_failures_come_back_as_400_problem_details_keyed_by_field()
    {
        var customer = await factory.CustomerAsync();

        var response = await customer.PostAsJsonAsync(
            "/api/requests",
            new { title = "", description = new string('x', RequestLimits.MaxDescriptionLength + 1), priority = "Low" },
            ApiFactory.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.ReadJsonAsync();
        problem.GetProperty("title").GetString().ShouldBe("One or more fields are invalid.");
        var errors = problem.GetProperty("errors");
        errors.GetProperty("title").EnumerateArray().Select(e => e.GetString()).ShouldContain("A title is required.");
        errors.TryGetProperty("description", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Staff_must_name_the_customer()
    {
        var agent = await factory.AgentAsync();

        var response = await agent.PostAsJsonAsync(
            "/api/requests",
            new { title = "Door sticks", description = "Front door.", priority = "Low" },
            ApiFactory.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("errors").TryGetProperty("customerId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Staff_naming_a_customer_that_does_not_exist_get_a_400_business_rule_error()
    {
        var agent = await factory.AgentAsync();

        var response = await agent.PostAsJsonAsync(
            "/api/requests",
            new { title = "Door sticks", description = "Front door.", priority = "Low", customerId = Guid.NewGuid() },
            ApiFactory.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("detail").GetString().ShouldBe("That customer does not exist.");
    }
}
