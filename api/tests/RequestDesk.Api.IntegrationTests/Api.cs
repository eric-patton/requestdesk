using System.Net.Http.Json;
using System.Text.Json;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Api.IntegrationTests;

/// <summary>Small HTTP helpers so tests read as what they assert rather than how they call.</summary>
internal static class Api
{
    public static Task<T?> ReadAsync<T>(this HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<T>(ApiFactory.Json);

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement;
    }

    public static async Task<Guid> FirstCustomerIdAsync(HttpClient staff)
    {
        var customers = await staff.GetFromJsonAsync<IReadOnlyList<CustomerSummary>>("/api/customers", ApiFactory.Json);
        return customers!.First().Id;
    }

    /// <summary>Open a request as whoever <paramref name="client"/> is signed in as.</summary>
    public static async Task<RequestDetail> CreateRequestAsync(HttpClient client, Guid? customerId = null, string title = "Test request")
    {
        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new { title, description = "Created by the integration tests.", priority = "Normal", customerId },
            ApiFactory.Json);

        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<RequestDetail>())!;
    }

    public static Task<HttpResponseMessage> ChangeStatusAsync(HttpClient client, Guid id, RequestStatus to, string? reason = null) =>
        client.PatchAsJsonAsync($"/api/requests/{id}/status", new { to = to.ToString(), reason }, ApiFactory.Json);

    /// <summary>Walk a request along the shortest legal path to <paramref name="target"/> as the admin.</summary>
    public static async Task DriveToAsync(HttpClient admin, Guid id, RequestStatus target)
    {
        RequestStatus[] path = target switch
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

        foreach (var step in path)
        {
            var response = await ChangeStatusAsync(admin, id, step, $"step to {step}");
            response.EnsureSuccessStatusCode();
        }
    }

    public static Task<RequestDetail?> GetRequestAsync(HttpClient client, Guid id) =>
        client.GetFromJsonAsync<RequestDetail>($"/api/requests/{id}", ApiFactory.Json);
}
