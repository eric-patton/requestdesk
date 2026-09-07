namespace RequestDesk.Infrastructure.Demo;

/// <summary>
/// Demo mode: seed synthetic data on startup and reset it on a schedule. Off by default; the
/// docker compose file and the public demo turn it on.
/// </summary>
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    public bool Enabled { get; set; }

    /// <summary>Wipe and reseed at every multiple of this many minutes past the hour. Zero disables the reset.</summary>
    public int ResetIntervalMinutes { get; set; } = 60;

    /// <summary>One password for every demo account, shown on the login screen.</summary>
    public string Password { get; set; } = "Demo-Pass-2026!";

    public string AdminEmail { get; set; } = "admin@requestdesk.demo";

    public string AgentEmail { get; set; } = "agent@requestdesk.demo";

    public string CustomerEmail { get; set; } = "customer@requestdesk.demo";
}
