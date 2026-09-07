using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RequestDesk.Infrastructure.Demo;

namespace RequestDesk.Api.Controllers;

public sealed record DemoAccount(string Role, string Email, string Password);

public sealed record DemoInfo(bool Enabled, int ResetIntervalMinutes, DateTimeOffset? NextResetAt, IReadOnlyList<DemoAccount> Accounts);

/// <summary>
/// Tells the login screen which demo accounts exist and when the data next resets. Only answers
/// when demo mode is on; a real deployment of this code base returns 404 here.
/// </summary>
[ApiController]
[Route("api/demo")]
[Produces("application/json")]
public sealed class DemoController(IOptions<DemoOptions> options, DemoResetService reset) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DemoInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<DemoInfo> Get()
    {
        var demo = options.Value;

        if (!demo.Enabled)
        {
            return NotFound();
        }

        return new DemoInfo(
            true,
            demo.ResetIntervalMinutes,
            reset.NextResetAt,
            [
                new DemoAccount("Admin", demo.AdminEmail, demo.Password),
                new DemoAccount("Agent", demo.AgentEmail, demo.Password),
                new DemoAccount("Customer", demo.CustomerEmail, demo.Password),
            ]);
    }
}
