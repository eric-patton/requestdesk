using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AttachmentTests(ApiFactory factory)
{
    private static MultipartFormDataContent File(string name, string contentType, byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { content, "file", name } };
    }

    [Fact]
    public async Task An_allowed_file_round_trips_with_its_content_type_and_name()
    {
        var customer = await factory.CustomerAsync();
        var request = await Api.CreateRequestAsync(customer);
        var bytes = System.Text.Encoding.UTF8.GetBytes("The rear tray, photographed in words.");

        var upload = await customer.PostAsync($"/api/requests/{request.Id}/attachments", File("notes.txt", "text/plain", bytes));

        upload.StatusCode.ShouldBe(HttpStatusCode.Created);
        var attachment = (await upload.ReadAsync<AttachmentDto>())!;
        attachment.FileName.ShouldBe("notes.txt");
        attachment.ContentType.ShouldBe("text/plain");
        attachment.SizeBytes.ShouldBe(bytes.Length);

        var download = await customer.GetAsync($"/api/requests/{request.Id}/attachments/{attachment.Id}");
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
        download.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("notes.txt");
        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);

        (await Api.GetRequestAsync(customer, request.Id))!.Attachments.ShouldHaveSingleItem().Id.ShouldBe(attachment.Id);
    }

    [Fact]
    public async Task A_disallowed_content_type_is_refused_before_anything_is_stored()
    {
        var customer = await factory.CustomerAsync();
        var request = await Api.CreateRequestAsync(customer);

        var upload = await customer.PostAsync($"/api/requests/{request.Id}/attachments", File("evil.exe", "application/x-msdownload", [0x4D, 0x5A]));

        upload.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await upload.ReadJsonAsync()).GetProperty("errors").TryGetProperty("contentType", out _).ShouldBeTrue();
        (await Api.GetRequestAsync(customer, request.Id))!.Attachments.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_closed_request_does_not_accept_attachments()
    {
        var admin = await factory.AdminAsync();
        var request = await Api.CreateRequestAsync(admin, await Api.FirstCustomerIdAsync(admin));
        await Api.DriveToAsync(admin, request.Id, RequestStatus.Closed);

        var upload = await admin.PostAsync($"/api/requests/{request.Id}/attachments", File("late.txt", "text/plain", [1, 2, 3]));

        upload.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Another_customer_cannot_download_it()
    {
        var customer = await factory.CustomerAsync();
        var agent = await factory.AgentAsync();
        var request = await Api.CreateRequestAsync(customer);
        var upload = await customer.PostAsync($"/api/requests/{request.Id}/attachments", File("notes.txt", "text/plain", [1, 2, 3]));
        var attachment = (await upload.ReadAsync<AttachmentDto>())!;

        // Sign in as a customer user from a different account (the second seeded customer contact).
        await using var scope = factory.CreateScope();
        var db = factory.CreateDbContext(scope);
        var other = db.Profiles.First(u => u.Role == Domain.Users.UserRole.Customer && u.CustomerId != request.Customer.Id);
        var otherCustomer = await factory.LoginAsync(other.Email);

        var download = await otherCustomer.GetAsync($"/api/requests/{request.Id}/attachments/{attachment.Id}");
        download.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var staffDownload = await agent.GetAsync($"/api/requests/{request.Id}/attachments/{attachment.Id}");
        staffDownload.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

[Collection(ApiCollection.Name)]
public class AssignmentTests(ApiFactory factory)
{
    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, Guid id, Guid? agentId) =>
        client.PatchAsJsonAsync($"/api/requests/{id}/assignment", new { agentId }, ApiFactory.Json);

    [Fact]
    public async Task An_agent_claims_an_unassigned_request_but_cannot_hand_it_to_someone_else()
    {
        var agent = await factory.AgentAsync();
        var admin = await factory.AdminAsync();
        var request = await Api.CreateRequestAsync(agent, await Api.FirstCustomerIdAsync(agent));
        var staff = (await agent.GetFromJsonAsync<IReadOnlyList<UserSummary>>("/api/staff", ApiFactory.Json))!;
        var me = staff.Single(s => s.DisplayName == "Marcus Bell");
        var colleague = staff.First(s => s.Id != me.Id);

        (await Api.GetRequestAsync(agent, request.Id))!.Permissions.CanChangeAssignment.ShouldBeTrue();

        var claimed = await AssignAsync(agent, request.Id, me.Id);
        claimed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = (await claimed.ReadAsync<RequestDetail>())!;
        detail.AssignedAgent!.Id.ShouldBe(me.Id);
        detail.Permissions.CanChangeAssignment.ShouldBeFalse();

        var handoff = await AssignAsync(agent, request.Id, colleague.Id);
        handoff.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var reassigned = await AssignAsync(admin, request.Id, colleague.Id);
        reassigned.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await reassigned.ReadAsync<RequestDetail>())!.AssignedAgent!.Id.ShouldBe(colleague.Id);

        var cleared = await AssignAsync(admin, request.Id, null);
        cleared.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cleared.ReadAsync<RequestDetail>())!.AssignedAgent.ShouldBeNull();
    }

    [Fact]
    public async Task A_customer_is_refused_by_the_role_check()
    {
        var customer = await factory.CustomerAsync();
        var request = await Api.CreateRequestAsync(customer);

        var response = await AssignAsync(customer, request.Id, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Only_active_staff_can_be_assigned()
    {
        var admin = await factory.AdminAsync();
        var request = await Api.CreateRequestAsync(admin, await Api.FirstCustomerIdAsync(admin));

        await using var scope = factory.CreateScope();
        var db = factory.CreateDbContext(scope);
        var customerUser = db.Profiles.First(u => u.Role == Domain.Users.UserRole.Customer);

        var response = await AssignAsync(admin, request.Id, customerUser.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("detail").GetString().ShouldBe("The assignee must be an active agent or admin.");
    }
}
