using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFactory factory)
{
    [Theory]
    [InlineData(ApiFactory.AdminEmail, "Admin")]
    [InlineData(ApiFactory.AgentEmail, "Agent")]
    [InlineData(ApiFactory.CustomerEmail, "Customer")]
    public async Task Each_demo_account_signs_in_with_its_role(string email, string role)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFactory.DemoPassword }, ApiFactory.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = (await response.ReadAsync<AuthResult>())!;
        auth.User.Role.ToString().ShouldBe(role);
        auth.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        (auth.CustomerId is not null).ShouldBe(role == "Customer");
    }

    [Fact]
    public async Task A_wrong_password_is_a_401_with_a_vague_message()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.AdminEmail, password = "nope-nope-nope" }, ApiFactory.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadJsonAsync()).GetProperty("detail").GetString().ShouldBe("The email address or password is incorrect.");
    }

    [Fact]
    public async Task A_refresh_token_works_once_and_reuse_kills_the_session()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.AgentEmail, password = ApiFactory.DemoPassword }, ApiFactory.Json);
        var first = (await login.ReadAsync<AuthResult>())!;

        var rotated = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken }, ApiFactory.Json);
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = (await rotated.ReadAsync<AuthResult>())!;
        second.RefreshToken.ShouldNotBe(first.RefreshToken);

        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken }, ApiFactory.Json);
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The replay revoked everything for the user, including the token that was still good.
        var afterReplay = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = second.RefreshToken }, ApiFactory.Json);
        afterReplay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_routes_need_a_token_and_role_routes_check_the_role()
    {
        var anonymous = factory.CreateClient();
        var customer = await factory.CustomerAsync();
        var agent = await factory.AgentAsync();

        (await anonymous.GetAsync("/api/requests")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await customer.GetAsync("/api/reports/summary")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await agent.GetAsync("/api/reports/summary")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await customer.GetAsync("/api/staff")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await agent.GetAsync("/api/staff")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

[Collection(ApiCollection.Name)]
public class ReportTests(ApiFactory factory)
{
    [Fact]
    public async Task The_summary_covers_every_status_and_bucket_and_adds_up()
    {
        var admin = await factory.AdminAsync();

        var report = (await admin.GetFromJsonAsync<SummaryReport>("/api/reports/summary", ApiFactory.Json))!;

        report.ByStatus.Select(s => s.Status).ShouldBe(Enum.GetValues<RequestStatus>());
        report.OpenByPriority.Select(p => p.Priority).ShouldBe(Enum.GetValues<RequestPriority>());
        report.Aging.Count.ShouldBe(5);
        report.Aging.Select(b => b.Label).ShouldBe(["Under a day", "1 to 3 days", "3 to 7 days", "1 to 2 weeks", "Over 2 weeks"]);

        await using var scope = factory.CreateScope();
        var db = factory.CreateDbContext(scope);
        var total = await db.Requests.CountAsync();
        var open = await db.Requests.CountAsync(r => r.Status != RequestStatus.Closed && r.Status != RequestStatus.Cancelled);

        report.ByStatus.Sum(s => s.Count).ShouldBe(total);
        report.OpenCount.ShouldBe(open);
        report.OpenByPriority.Sum(p => p.Count).ShouldBe(open);
        report.Aging.Sum(b => b.Count).ShouldBe(open);
        report.InProgressCount.ShouldBe(await db.Requests.CountAsync(r => r.Status == RequestStatus.InProgress));
        report.BlockedCount.ShouldBe(await db.Requests.CountAsync(r => r.Status == RequestStatus.Blocked));

        if (open > 0)
        {
            report.MedianOpenAgeHours.ShouldNotBeNull();
            report.MedianOpenAgeHours.Value.ShouldBeGreaterThanOrEqualTo(0);
        }
    }
}

[Collection(ApiCollection.Name)]
public class OperationsTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_checks_the_database_and_liveness_does_not()
    {
        var client = factory.CreateClient();

        var ready = await client.GetAsync("/health");
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        var readyBody = await ready.ReadJsonAsync();
        readyBody.GetProperty("status").GetString().ShouldBe("Healthy");
        readyBody.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ShouldContain("postgres");

        var live = await client.GetAsync("/health/live");
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await live.ReadJsonAsync()).GetProperty("checks").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task The_openapi_document_lists_the_endpoints()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paths = (await response.ReadJsonAsync()).GetProperty("paths");
        foreach (var path in new[]
        {
            "/api/auth/login", "/api/auth/refresh", "/api/requests", "/api/requests/{id}", "/api/requests/{id}/status",
            "/api/requests/{id}/assignment", "/api/requests/{id}/comments", "/api/requests/{id}/attachments",
            "/api/requests/{id}/attachments/{attachmentId}", "/api/reports/summary", "/api/staff", "/api/customers", "/api/demo",
        })
        {
            paths.TryGetProperty(path, out _).ShouldBeTrue(path);
        }
    }

    [Fact]
    public async Task The_demo_endpoint_names_the_three_accounts()
    {
        var client = factory.CreateClient();

        var demo = await client.GetFromJsonAsync<Controllers.DemoInfo>("/api/demo", ApiFactory.Json);

        demo!.Enabled.ShouldBeTrue();
        demo.Accounts.Select(a => a.Role).ShouldBe(["Admin", "Agent", "Customer"]);
        demo.Accounts.ShouldAllBe(a => a.Password == ApiFactory.DemoPassword);
    }

    [Fact]
    public async Task Comments_are_appended_with_their_author_and_show_in_the_detail()
    {
        var agent = await factory.AgentAsync();
        var request = await Api.CreateRequestAsync(agent, await Api.FirstCustomerIdAsync(agent));

        var response = await agent.PostAsJsonAsync($"/api/requests/{request.Id}/comments", new { body = "  On my way up.  " }, ApiFactory.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var comment = (await response.ReadAsync<CommentDto>())!;
        comment.Body.ShouldBe("On my way up.");
        comment.Author.DisplayName.ShouldBe("Marcus Bell");
        (await Api.GetRequestAsync(agent, request.Id))!.Comments.ShouldHaveSingleItem().Id.ShouldBe(comment.Id);

        var blank = await agent.PostAsJsonAsync($"/api/requests/{request.Id}/comments", new { body = "   " }, ApiFactory.Json);
        blank.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
