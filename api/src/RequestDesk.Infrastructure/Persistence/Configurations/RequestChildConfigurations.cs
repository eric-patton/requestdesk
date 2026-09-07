using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RequestDesk.Domain.Requests;
using RequestDesk.Domain.Users;

namespace RequestDesk.Infrastructure.Persistence.Configurations;

internal sealed class RequestCommentConfiguration : IEntityTypeConfiguration<RequestComment>
{
    public void Configure(EntityTypeBuilder<RequestComment> builder)
    {
        builder.ToTable("request_comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Body).HasMaxLength(RequestLimits.MaxCommentLength);
        builder.HasIndex(c => c.RequestId);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RequestAttachmentConfiguration : IEntityTypeConfiguration<RequestAttachment>
{
    public void Configure(EntityTypeBuilder<RequestAttachment> builder)
    {
        builder.ToTable("request_attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.FileName).HasMaxLength(RequestLimits.MaxFileNameLength);
        builder.Property(a => a.ContentType).HasMaxLength(100);
        builder.Property(a => a.StorageKey).HasMaxLength(200);
        builder.HasIndex(a => a.StorageKey).IsUnique();
        builder.HasIndex(a => a.RequestId);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(a => a.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// The append-only table. The trigger that refuses UPDATE and DELETE is created in the initial
/// migration; the EF interceptor refuses the same operations before they reach the database.
/// </summary>
internal sealed class RequestStatusHistoryConfiguration : IEntityTypeConfiguration<RequestStatusHistory>
{
    public void Configure(EntityTypeBuilder<RequestStatusHistory> builder)
    {
        builder.ToTable("request_status_history");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.Reason).HasMaxLength(RequestLimits.MaxReasonLength);
        builder.HasIndex(h => new { h.RequestId, h.OccurredAt });
        builder.HasIndex(h => new { h.ToStatus, h.OccurredAt });

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(h => h.ActorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
