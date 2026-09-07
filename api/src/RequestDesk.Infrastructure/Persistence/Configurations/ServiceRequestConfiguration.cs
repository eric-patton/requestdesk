using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Infrastructure.Persistence.Configurations;

internal sealed class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.ToTable("service_requests");
        builder.HasKey(r => r.Id);

        // Ids are minted by the domain, never by the database. Telling EF so matters: a child entity
        // discovered through a navigation with an already-set *generated* key would be tracked as
        // Modified rather than Added, and the append-only interceptor would refuse to save it.
        builder.Property(r => r.Id).ValueGeneratedNever();

        // RD-001000, RD-001001, ... assigned by the database from a sequence, so two requests created in
        // the same instant can never collide and the number is never guessable from the GUID.
        builder.Property(r => r.ReferenceNumber)
            .HasMaxLength(20)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql($"'RD-' || lpad(nextval('{AppDbContext.ReferenceSequence}')::text, 6, '0')");
        builder.HasIndex(r => r.ReferenceNumber).IsUnique();

        builder.Property(r => r.Title).HasMaxLength(RequestLimits.MaxTitleLength);
        builder.Property(r => r.Description).HasMaxLength(RequestLimits.MaxDescriptionLength);
        builder.Property(r => r.Priority);
        builder.Property(r => r.Status);

        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.Priority);
        builder.HasIndex(r => r.CustomerId);
        builder.HasIndex(r => r.AssignedAgentId);
        builder.HasIndex(r => r.UpdatedAt);

        builder.HasOne(r => r.Customer)
            .WithMany()
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.AssignedAgent)
            .WithMany()
            .HasForeignKey(r => r.AssignedAgentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(r => r.Comments)
            .WithOne()
            .HasForeignKey(c => c.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Comments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(r => r.Attachments)
            .WithOne()
            .HasForeignKey(a => a.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(r => r.History)
            .WithOne()
            .HasForeignKey(h => h.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.History).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Optimistic concurrency on PostgreSQL's own row version (the xmin system column). Two agents
        // saving the same request at once: the second gets a 409 instead of silently overwriting the first.
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Ignore(r => r.IsOpen);
    }
}
