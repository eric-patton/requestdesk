using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RequestDesk.Application.Contracts;
using RequestDesk.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RequestDesk.Api.IntegrationTests;

/// <summary>
/// Hosts the real API against a real PostgreSQL in a container. One container and one seeded
/// database for the whole collection; tests create their own requests rather than relying on the
/// seed being untouched.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "Demo-Pass-2026!";
    public const string AdminEmail = "admin@requestdesk.demo";
    public const string AgentEmail = "agent@requestdesk.demo";
    public const string CustomerEmail = "customer@requestdesk.demo";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("requestdesk_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "requestdesk-tests", Guid.NewGuid().ToString("N"));

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Touch the server so the host builds, migrates and seeds before the first test runs.
        using var client = CreateClient();
        var health = await client.GetAsync("/health");
        health.EnsureSuccessStatusCode();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();

        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("Demo:Enabled", "true");
        builder.UseSetting("Demo:ResetIntervalMinutes", "0");
        builder.UseSetting("Demo:Password", DemoPassword);
        builder.UseSetting("Storage:Root", _storageRoot);
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", "100000");

        // Quiet by default. The append-only tests make the database refuse statements on purpose,
        // and the test server has no body-size feature; neither is news worth a line in the test log.
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.AspNetCore", "Error");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command", "Critical");
        builder.UseSetting("Logging:LogLevel:RequestDesk", "Error");
    }

    /// <summary>A fresh scope for tests that need to look at the database directly.</summary>
    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    public AppDbContext CreateDbContext(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<AppDbContext>();

    public Task<HttpClient> AdminAsync() => LoginAsync(AdminEmail);

    public Task<HttpClient> AgentAsync() => LoginAsync(AgentEmail);

    public Task<HttpClient> CustomerAsync() => LoginAsync(CustomerEmail);

    public async Task<HttpClient> LoginAsync(string email, string password = DemoPassword)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password }, Json);
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResult>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
