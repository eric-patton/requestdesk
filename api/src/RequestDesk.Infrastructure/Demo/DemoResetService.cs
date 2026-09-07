using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RequestDesk.Infrastructure.Demo;

/// <summary>
/// Resets the demo database on a fixed schedule so a public demo cannot drift into a mess. The
/// next reset time is published through <see cref="NextResetAt"/> for the banner on the login screen.
/// </summary>
public sealed class DemoResetService(
    IServiceScopeFactory scopes,
    IOptions<DemoOptions> options,
    TimeProvider clock,
    ILogger<DemoResetService> logger) : BackgroundService
{
    public DateTimeOffset? NextResetAt { get; private set; }

    public static DateTimeOffset NextBoundary(DateTimeOffset now, int intervalMinutes)
    {
        var interval = TimeSpan.FromMinutes(intervalMinutes);
        var sinceHour = now - new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
        var elapsedIntervals = (long)Math.Floor(sinceHour / interval);
        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset) + interval * (elapsedIntervals + 1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled || settings.ResetIntervalMinutes <= 0)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = clock.GetUtcNow();
            NextResetAt = NextBoundary(now, settings.ResetIntervalMinutes);

            try
            {
                await Task.Delay(NextResetAt.Value - now, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                using var scope = scopes.CreateScope();
                var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
                await seeder.ResetAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Demo reset failed; will try again at the next boundary");
            }
        }
    }
}
