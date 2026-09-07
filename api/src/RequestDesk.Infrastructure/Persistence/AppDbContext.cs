using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RequestDesk.Domain.Customers;
using RequestDesk.Domain.Requests;
using RequestDesk.Domain.Users;
using RequestDesk.Infrastructure.Identity;

namespace RequestDesk.Infrastructure.Persistence;

/// <summary>
/// One context for the domain tables and the identity credential tables. Table and column names
/// are snake_case via the naming convention plugin, so the SQL in the reporting query reads naturally.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public const string ReferenceSequence = "request_reference_seq";

    /// <summary>User profiles. Named to avoid colliding with the identity <c>Users</c> set on the base class.</summary>
    public DbSet<AppUser> Profiles => Set<AppUser>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<ServiceRequest> Requests => Set<ServiceRequest>();

    public DbSet<RequestComment> Comments => Set<RequestComment>();

    public DbSet<RequestAttachment> Attachments => Set<RequestAttachment>();

    public DbSet<RequestStatusHistory> StatusHistory => Set<RequestStatusHistory>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasSequence<long>(ReferenceSequence).StartsAt(1000).IncrementsBy(1);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // The identity tables keep their own prefix so nobody mistakes them for the domain.
        builder.Entity<ApplicationUser>().ToTable("identity_users");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("identity_user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("identity_user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("identity_user_tokens");
    }
}
