using Microsoft.AspNetCore.Identity;

namespace RequestDesk.Infrastructure.Identity;

/// <summary>
/// The credential record. Nothing about the person lives here; that is <see cref="Domain.Users.AppUser"/>,
/// which shares this id. Keeping them apart means the domain never references ASP.NET Core Identity.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>;
