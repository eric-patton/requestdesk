using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;
using RequestDesk.Infrastructure.Persistence;

namespace RequestDesk.Infrastructure.Reporting;

/// <summary>
/// The dashboard numbers, written in SQL. This is the one place in the system that bypasses EF Core:
/// FILTER clauses, a percentile over open age and bucketed aging are one round trip in SQL and would
/// be four LINQ queries or an untranslatable one. The reasoning is in ADR 0002. It borrows EF's
/// connection so it sees the same database the rest of the request sees.
/// </summary>
internal sealed class SummaryQuery(AppDbContext db, TimeProvider clock) : ISummaryReport
{
    private static readonly (string Label, int MinDays, int? MaxDays)[] Buckets =
    [
        ("Under a day", 0, 1),
        ("1 to 3 days", 1, 3),
        ("3 to 7 days", 3, 7),
        ("1 to 2 weeks", 7, 14),
        ("Over 2 weeks", 14, null),
    ];

    private const string Sql = """
        -- 1. every status, including the empty ones, so the client never has to fill gaps
        SELECT s.status, COALESCE(c.count, 0)::int AS count
        FROM unnest(ARRAY[1, 2, 3, 4, 5, 6, 7]) AS s(status)
        LEFT JOIN (
            SELECT status, COUNT(*) AS count
            FROM service_requests
            GROUP BY status
        ) c ON c.status = s.status
        ORDER BY s.status;

        -- 2. open requests by priority
        SELECT p.priority, COALESCE(c.count, 0)::int AS count
        FROM unnest(ARRAY[1, 2, 3, 4]) AS p(priority)
        LEFT JOIN (
            SELECT priority, COUNT(*) AS count
            FROM service_requests
            WHERE status NOT IN (6, 7)
            GROUP BY priority
        ) c ON c.priority = p.priority
        ORDER BY p.priority;

        -- 3. headline tiles
        SELECT
            COUNT(*) FILTER (WHERE status NOT IN (6, 7))                                 AS "OpenCount",
            COUNT(*) FILTER (WHERE status = 3)                                           AS "InProgressCount",
            COUNT(*) FILTER (WHERE status = 4)                                           AS "BlockedCount",
            COUNT(*) FILTER (WHERE created_at >= @since)                                 AS "CreatedThisWeek",
            (
                SELECT COUNT(DISTINCT request_id)
                FROM request_status_history
                WHERE to_status = 5 AND occurred_at >= @since
            )                                                                            AS "ResolvedThisWeek",
            percentile_cont(0.5) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (@now - created_at)) / 3600.0)
                FILTER (WHERE status NOT IN (6, 7))                                      AS "MedianOpenAgeHours"
        FROM service_requests;

        -- 4. how long open requests have been waiting
        SELECT
            CASE
                WHEN age_days < 1  THEN 0
                WHEN age_days < 3  THEN 1
                WHEN age_days < 7  THEN 2
                WHEN age_days < 14 THEN 3
                ELSE 4
            END AS bucket,
            COUNT(*)::int AS count
        FROM (
            SELECT EXTRACT(EPOCH FROM (@now - created_at)) / 86400.0 AS age_days
            FROM service_requests
            WHERE status NOT IN (6, 7)
        ) ages
        GROUP BY bucket
        ORDER BY bucket;
        """;

    public async Task<SummaryReport> GetAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var connection = db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var command = new CommandDefinition(
            Sql,
            new { now = now.UtcDateTime, since = now.AddDays(-7).UtcDateTime },
            cancellationToken: cancellationToken);

        using var grid = await connection.QueryMultipleAsync(command);

        var byStatus = (await grid.ReadAsync<(int Status, int Count)>())
            .Select(row => new StatusCount((RequestStatus)row.Status, row.Count))
            .ToArray();

        var byPriority = (await grid.ReadAsync<(int Priority, int Count)>())
            .Select(row => new PriorityCount((RequestPriority)row.Priority, row.Count))
            .ToArray();

        var headline = await grid.ReadSingleAsync<HeadlineRow>();

        var bucketCounts = (await grid.ReadAsync<(int Bucket, int Count)>()).ToDictionary(row => row.Bucket, row => row.Count);
        var aging = Buckets
            .Select((bucket, index) => new AgingBucket(bucket.Label, bucket.MinDays, bucket.MaxDays, bucketCounts.GetValueOrDefault(index)))
            .ToArray();

        return new SummaryReport(
            (int)headline.OpenCount,
            (int)headline.InProgressCount,
            (int)headline.BlockedCount,
            (int)headline.ResolvedThisWeek,
            (int)headline.CreatedThisWeek,
            headline.MedianOpenAgeHours is { } median ? Math.Round(median, 1) : null,
            byStatus,
            byPriority,
            aging,
            now);
    }

    private sealed record HeadlineRow(
        long OpenCount,
        long InProgressCount,
        long BlockedCount,
        long CreatedThisWeek,
        long ResolvedThisWeek,
        double? MedianOpenAgeHours);
}
