using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Domain.Customers;
using RequestDesk.Domain.Requests;
using RequestDesk.Domain.Users;
using RequestDesk.Infrastructure.Identity;
using RequestDesk.Infrastructure.Persistence;

namespace RequestDesk.Infrastructure.Demo;

/// <summary>
/// Builds the demo data set. Everything is synthetic and deterministic: the same seed produces the
/// same customers, requests and timelines every time, so screenshots stay reproducible and nothing
/// in the database was ever a real person or a real request.
/// </summary>
public sealed class DemoSeeder(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IFileStorage storage,
    IOptions<DemoOptions> options,
    TimeProvider clock,
    ILogger<DemoSeeder> logger)
{
    private const int RandomSeed = 20260906;

    private static readonly string[] CustomerNames =
    [
        "Larkspur Dental", "Northgate Storage", "Bramble & Co. Bakery", "Fieldstone Veterinary",
        "Harbor Lights Bookshop", "Quill Street Studio", "Redwood Property Management", "Sunnyvale Pediatrics",
    ];

    private static readonly (string Title, string Description, RequestPriority Priority)[] RequestTemplates =
    [
        ("Printer on the third floor is jamming", "Every second page jams in the rear tray. Started after the toner change on Monday.", RequestPriority.Normal),
        ("Front door badge reader not responding", "The reader shows a solid red light and does not beep. Staff are propping the door open, which is a security problem.", RequestPriority.Urgent),
        ("Wi-Fi drops in the conference room", "Connections drop every ten to fifteen minutes during meetings. The rest of the office is fine.", RequestPriority.High),
        ("Replace flickering light in stairwell B", "The fixture between floors two and three flickers constantly. It is a trip hazard in the evening.", RequestPriority.Normal),
        ("Set up a laptop for the new hire starting Monday", "Standard configuration plus the accounting package. The new hire's name is on the onboarding ticket.", RequestPriority.High),
        ("Thermostat in the back office reads 82 degrees", "The unit is set to 70 but the room is stifling. May be a sensor problem rather than the HVAC itself.", RequestPriority.High),
        ("Shared calendar not syncing to phones", "Events added on desktop do not appear on mobile for anyone on the team. Started three days ago.", RequestPriority.Normal),
        ("Leak under the break room sink", "Slow drip from the supply line. A bucket is under it for now.", RequestPriority.High),
        ("Request an additional monitor for the reception desk", "The front desk works between two systems all day and a second screen would speed check-in.", RequestPriority.Low),
        ("Parking lot light out near the east entrance", "The pole light nearest the east door has been out for a week. Staff leaving after dark have complained.", RequestPriority.Normal),
        ("Email attachments over 10 MB bounce", "Anything with a large attachment comes back undeliverable. Smaller messages go through fine.", RequestPriority.Normal),
        ("Carpet stain in the waiting area", "Coffee spill about the size of a dinner plate near the magazine rack. Regular cleaning has not lifted it.", RequestPriority.Low),
        ("Password reset for the scheduling system", "Locked out after too many attempts. Need access restored before the morning appointments.", RequestPriority.Urgent),
        ("Dishwasher in the staff kitchen leaves residue", "Glasses come out cloudy and plates have a film. The rinse aid compartment is full.", RequestPriority.Low),
        ("Install a grab bar in the accessible restroom", "Requested by a patient. Should be installed per the accessibility guidelines for height and placement.", RequestPriority.High),
        ("Phone line two has static", "Callers on line two report crackling. Line one is clear.", RequestPriority.Normal),
        ("Move three desks to the second floor", "Reorganizing the team. Desks, chairs and two filing cabinets need to go from room 104 to 210.", RequestPriority.Low),
        ("Security camera over the loading dock is offline", "The feed shows as disconnected in the viewer since last night.", RequestPriority.Urgent),
        ("Label printer prints blank labels", "The printer feeds and cuts but nothing appears on the label. New roll installed, same result.", RequestPriority.Normal),
        ("Add two users to the shared drive", "Two new staff need read and write access to the operations folder.", RequestPriority.Normal),
        ("Automatic door closes too quickly", "The front entrance door closes before someone with a stroller or walker is through.", RequestPriority.High),
        ("Backup job failed for two nights running", "The nightly backup report shows failures on the file server both nights. No details in the summary email.", RequestPriority.Urgent),
        ("Replace worn chair in the consultation room", "The gas lift no longer holds and the chair sinks when anyone sits down.", RequestPriority.Low),
        ("Point of sale terminal freezes at checkout", "Freezes two or three times a day and needs a restart. Customers are waiting.", RequestPriority.Urgent),
        ("Gutter overflowing at the rear of the building", "Water sheets over the edge in heavy rain and pools by the back door.", RequestPriority.Normal),
        ("Scanner produces skewed pages", "Every page scans at a slight angle regardless of how it is loaded.", RequestPriority.Low),
        ("Request a quote for repainting the lobby", "Scuffed walls and a chipped baseboard. Would like a quote for a repaint in the same colour.", RequestPriority.Low),
        ("Smoke detector chirping in the storage room", "Chirps every minute. Battery replaced, still chirping.", RequestPriority.High),
        ("VPN disconnects when the laptop sleeps", "Has to reconnect manually every time the lid is opened. Started after the last update.", RequestPriority.Normal),
        ("Ice maker in the staff kitchen has stopped", "No ice for two days. The water line appears connected.", RequestPriority.Low),
    ];

    private static readonly string[] CommentPool =
    [
        "Thanks, I have picked this up and will take a look this afternoon.",
        "Could you let me know the best time to come by?",
        "Any time after two works for us.",
        "Ordered the replacement part. Should arrive in two working days.",
        "Part is in. Scheduling the install for tomorrow morning.",
        "Still happening as of this morning.",
        "Fixed and tested. Please let us know if it comes back.",
        "Confirmed working on our end. Thank you!",
        "Waiting on the vendor for a firmware update before this can be closed out.",
        "Escalated to the building manager since this needs a contractor.",
    ];

    /// <summary>Seed only if the database is empty. Safe to call on every startup.</summary>
    public async Task SeedIfEmptyAsync(CancellationToken cancellationToken)
    {
        if (await db.Profiles.AnyAsync(cancellationToken))
        {
            return;
        }

        await SeedAsync(cancellationToken);
    }

    /// <summary>Wipe every table and every stored attachment, then seed again.</summary>
    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Resetting demo data");

        var keys = await db.Attachments.Select(a => a.StorageKey).ToListAsync(cancellationToken);

        foreach (var key in keys)
        {
            await storage.DeleteAsync(key, cancellationToken);
        }

        // TRUNCATE does not fire the row-level trigger that protects request_status_history, which
        // is the one sanctioned way to empty it: as a whole, never a row at a time.
        await db.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                request_status_history, request_comments, request_attachments, service_requests,
                refresh_tokens, app_users, customers,
                identity_user_claims, identity_user_logins, identity_user_tokens, identity_users
            RESTART IDENTITY CASCADE;
            ALTER SEQUENCE request_reference_seq RESTART WITH 1000;
            """,
            cancellationToken);

        db.ChangeTracker.Clear();
        await SeedAsync(cancellationToken);
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var faker = new Faker { Random = new Randomizer(RandomSeed) };

        logger.LogInformation("Seeding demo data");

        // Customers
        var customers = new List<Customer>();

        foreach (var name in CustomerNames)
        {
            var slug = new string(name.ToLowerInvariant().Where(char.IsLetter).ToArray());
            customers.Add(Customer.Create(name, $"office@{slug}.example", faker.PickRandom("Main Street", "Riverside", "Downtown", "Westside") + " location", now.AddDays(-90)));
        }

        db.Customers.AddRange(customers);
        await db.SaveChangesAsync(cancellationToken);

        // Users. The three demo logins first, then more agents and one contact per remaining customer.
        var admin = await CreateUserAsync("Dana Whitfield", settings.AdminEmail, UserRole.Admin, null, settings.Password);
        var agents = new List<AppUser>
        {
            await CreateUserAsync("Marcus Bell", settings.AgentEmail, UserRole.Agent, null, settings.Password),
            await CreateUserAsync("Priya Nair", "priya.nair@requestdesk.demo", UserRole.Agent, null, settings.Password),
            await CreateUserAsync("Tomas Herrera", "tomas.herrera@requestdesk.demo", UserRole.Agent, null, settings.Password),
        };

        var customerUsers = new List<AppUser>
        {
            await CreateUserAsync("Jordan Lee", settings.CustomerEmail, UserRole.Customer, customers[0].Id, settings.Password),
        };

        for (var i = 1; i < customers.Count; i++)
        {
            var fullName = faker.Name.FullName();
            var email = $"{fullName.ToLowerInvariant().Replace(' ', '.').Replace("'", string.Empty, StringComparison.Ordinal)}@{new string(customers[i].Name.ToLowerInvariant().Where(char.IsLetter).ToArray())}.example";
            customerUsers.Add(await CreateUserAsync(fullName, email, UserRole.Customer, customers[i].Id, settings.Password));
        }

        List<AppUser> staff = [admin, .. agents];

        // Requests. Sixty of them spread over the last thirty days, each walked through a plausible history.
        var requests = new List<ServiceRequest>();

        for (var i = 0; i < 60; i++)
        {
            var template = RequestTemplates[i % RequestTemplates.Length];
            var customerUser = faker.PickRandom(customerUsers);
            var createdAt = now.AddMinutes(-faker.Random.Int(30, 30 * 24 * 60));
            var actor = customerUser.AsActor();

            var request = ServiceRequest.Create(
                customerUser.CustomerId!.Value,
                template.Title,
                template.Description,
                faker.Random.Bool(0.7f) ? template.Priority : faker.PickRandom<RequestPriority>(),
                actor,
                createdAt);

            WalkHistory(request, faker, staff, agents, customerUser, createdAt, now);
            requests.Add(request);
        }

        db.Requests.AddRange(requests);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Customers} customers, {Users} users and {Requests} requests", customers.Count, staff.Count + customerUsers.Count, requests.Count);
    }

    /// <summary>
    /// Move a fresh request along a random legal path, leaving some at each stage, with comments and
    /// an assignment where a real desk would have them. Timestamps advance monotonically.
    /// </summary>
    private static void WalkHistory(
        ServiceRequest request,
        Faker faker,
        List<AppUser> staff,
        List<AppUser> agents,
        AppUser customerUser,
        DateTimeOffset createdAt,
        DateTimeOffset now)
    {
        var t = createdAt;
        var ageHours = (now - createdAt).TotalHours;
        var agent = faker.PickRandom(agents);
        var admin = staff[0];

        DateTimeOffset Advance(int minMinutes, int maxMinutes)
        {
            var next = t.AddMinutes(faker.Random.Int(minMinutes, maxMinutes));
            t = next < now ? next : now;
            return t;
        }

        // Very recent requests stay New. Older ones are progressively more likely to have moved on.
        var stagesAvailable = ageHours switch
        {
            < 2 => 0,
            < 12 => faker.Random.Int(0, 2),
            < 72 => faker.Random.Int(1, 4),
            _ => faker.Random.Int(2, 6),
        };

        if (stagesAvailable == 0)
        {
            return;
        }

        // A few get cancelled straight away by the customer.
        if (faker.Random.Bool(0.06f))
        {
            request.ChangeStatus(RequestStatus.Cancelled, customerUser.AsActor(), faker.PickRandom("Sorted it ourselves.", "Duplicate of an earlier request.", "No longer needed."), Advance(20, 600));
            return;
        }

        request.Assign(agent.Id, admin.AsActor(), Advance(10, 240));
        request.ChangeStatus(RequestStatus.Triaged, agent.AsActor(), faker.PickRandom("Confirmed with the customer.", "Looks straightforward.", "Needs a site visit.", null), Advance(5, 120));
        request.AddComment(CommentPool[0], agent.AsActor(), Advance(1, 30));

        if (stagesAvailable == 1)
        {
            return;
        }

        request.ChangeStatus(RequestStatus.InProgress, agent.AsActor(), null, Advance(30, 24 * 60));

        if (faker.Random.Bool(0.5f))
        {
            request.AddComment(CommentPool[1], agent.AsActor(), Advance(5, 60));
            request.AddComment(CommentPool[2], customerUser.AsActor(), Advance(10, 240));
        }

        if (stagesAvailable == 2)
        {
            return;
        }

        // Some get blocked on a part or a vendor.
        if (faker.Random.Bool(0.35f))
        {
            request.ChangeStatus(RequestStatus.Blocked, agent.AsActor(), faker.PickRandom("Waiting on a replacement part.", "Vendor firmware needed.", "Contractor scheduled for next week."), Advance(60, 24 * 60));
            request.AddComment(faker.PickRandom(CommentPool[3], CommentPool[8], CommentPool[9]), agent.AsActor(), Advance(1, 30));

            if (stagesAvailable == 3)
            {
                return;
            }

            if (faker.Random.Bool(0.15f))
            {
                request.ChangeStatus(RequestStatus.Cancelled, admin.AsActor(), "Customer withdrew the request while it was blocked.", Advance(60, 48 * 60));
                return;
            }

            request.ChangeStatus(RequestStatus.InProgress, agent.AsActor(), CommentPool[4], Advance(6 * 60, 3 * 24 * 60));
        }

        if (stagesAvailable <= 3)
        {
            return;
        }

        request.ChangeStatus(RequestStatus.Resolved, agent.AsActor(), faker.PickRandom("Replaced and tested.", "Reconfigured; confirmed working.", "Repaired on site."), Advance(30, 2 * 24 * 60));
        request.AddComment(CommentPool[6], agent.AsActor(), Advance(1, 15));

        if (stagesAvailable == 4)
        {
            return;
        }

        // Occasionally the fix did not hold and an admin reopens it.
        if (faker.Random.Bool(0.2f))
        {
            request.AddComment(CommentPool[5], customerUser.AsActor(), Advance(60, 24 * 60));
            request.ChangeStatus(RequestStatus.InProgress, admin.AsActor(), "Reopened: the customer reports it is happening again.", Advance(5, 120));

            if (stagesAvailable == 5)
            {
                return;
            }

            request.ChangeStatus(RequestStatus.Resolved, agent.AsActor(), "Root cause found this time.", Advance(60, 24 * 60));
        }

        request.AddComment(CommentPool[7], customerUser.AsActor(), Advance(30, 24 * 60));
        request.ChangeStatus(RequestStatus.Closed, agent.AsActor(), "Confirmed by the customer.", Advance(5, 60));
    }

    private async Task<AppUser> CreateUserAsync(string displayName, string email, UserRole role, Guid? customerId, string password)
    {
        var id = Guid.CreateVersion7();
        var credential = new ApplicationUser
        {
            Id = id,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(credential, password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not create demo user {email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        var profile = AppUser.Create(id, displayName, email, role, customerId);
        db.Profiles.Add(profile);
        await db.SaveChangesAsync(CancellationToken.None);
        return profile;
    }
}
