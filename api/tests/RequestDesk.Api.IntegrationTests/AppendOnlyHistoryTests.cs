using Microsoft.EntityFrameworkCore;
using Npgsql;
using RequestDesk.Domain.Requests;
using RequestDesk.Infrastructure.Persistence;

namespace RequestDesk.Api.IntegrationTests;

/// <summary>
/// The status history is append only, and this proves it at both layers that could break it:
/// a raw SQL statement against PostgreSQL, and a tracked change through EF Core.
/// </summary>
[Collection(ApiCollection.Name)]
public class AppendOnlyHistoryTests(ApiFactory factory)
{
    private async Task<Guid> AnyHistoryRowIdAsync()
    {
        var admin = await factory.AdminAsync();
        var request = await Api.CreateRequestAsync(admin, await Api.FirstCustomerIdAsync(admin), "append-only probe");
        await Api.DriveToAsync(admin, request.Id, RequestStatus.Triaged);
        return (await Api.GetRequestAsync(admin, request.Id))!.History.Last().Id;
    }

    [Fact]
    public async Task The_database_refuses_a_raw_update()
    {
        var id = await AnyHistoryRowIdAsync();
        await using var scope = factory.CreateScope();
        var db = factory.CreateDbContext(scope);

        var ex = await Should.ThrowAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE request_status_history SET reason = 'tampered' WHERE id = {id}"));

        ex.SqlState.ShouldBe(PostgresErrorCodes.RestrictViolation);
        ex.MessageText.ShouldContain("append only");
    }

    [Fact]
    public async Task The_database_refuses_a_raw_delete()
    {
        var id = await AnyHistoryRowIdAsync();
        await using var scope = factory.CreateScope();
        var db = factory.CreateDbContext(scope);

        var ex = await Should.ThrowAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM request_status_history WHERE id = {id}"));

        ex.SqlState.ShouldBe(PostgresErrorCodes.RestrictViolation);
    }

    [Fact]
    public async Task Entity_framework_refuses_to_save_a_modified_row_before_it_reaches_the_database()
    {
        var id = await AnyHistoryRowIdAsync();
        await using var scope = factory.CreateScope();
        var db = factory.CreateDbContext(scope);
        var row = await db.StatusHistory.SingleAsync(h => h.Id == id);

        db.Entry(row).Property(nameof(RequestStatusHistory.Reason)).CurrentValue = "tampered";

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        ex.Message.ShouldStartWith(AppendOnlyHistoryInterceptor.Message);
    }

    [Fact]
    public async Task Entity_framework_refuses_to_delete_a_row()
    {
        var id = await AnyHistoryRowIdAsync();
        await using var scope = factory.CreateScope();
        var db = factory.CreateDbContext(scope);
        var row = await db.StatusHistory.SingleAsync(h => h.Id == id);

        db.StatusHistory.Remove(row);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        ex.Message.ShouldStartWith(AppendOnlyHistoryInterceptor.Message);
    }

    [Fact]
    public async Task The_row_is_still_there_and_unchanged_after_every_attempt()
    {
        var id = await AnyHistoryRowIdAsync();
        await using var scope = factory.CreateScope();
        var db = factory.CreateDbContext(scope);

        try
        {
            await db.Database.ExecuteSqlAsync($"UPDATE request_status_history SET reason = 'tampered' WHERE id = {id}");
        }
        catch (PostgresException)
        {
        }

        var row = await db.StatusHistory.AsNoTracking().SingleAsync(h => h.Id == id);
        row.Reason.ShouldBe("step to Triaged");
    }
}
