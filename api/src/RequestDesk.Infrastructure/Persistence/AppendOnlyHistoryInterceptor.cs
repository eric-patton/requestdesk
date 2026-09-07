using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Infrastructure.Persistence;

/// <summary>
/// Refuses to save if any status history row is marked modified or deleted. The database trigger
/// would refuse too; this fails earlier, with a message that names the rule, before a round trip.
/// </summary>
public sealed class AppendOnlyHistoryInterceptor : SaveChangesInterceptor
{
    public const string Message = "request_status_history is append only. Rows are never updated or deleted.";

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Check(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Check(DbContextEventData eventData)
    {
        var context = eventData.Context;

        if (context is null)
        {
            return;
        }

        var offending = context.ChangeTracker
            .Entries<RequestStatusHistory>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (offending.Count > 0)
        {
            var detail = string.Join("; ", offending.Select(e =>
                $"{e.Entity.Id} is {e.State}" + (e.State == EntityState.Modified
                    ? " (" + string.Join(", ", e.Properties.Where(p => p.IsModified).Select(p => $"{p.Metadata.Name}: {p.OriginalValue ?? "null"} -> {p.CurrentValue ?? "null"}")) + ")"
                    : string.Empty)));

            throw new InvalidOperationException($"{Message} Offending rows: {detail}");
        }
    }
}
