# 0001. The status history is append only

Date: 2026-09-06. Status: accepted.

## Context

Every service request carries a history of status changes: who moved it, from what, to what, when,
and why. That history is the audit trail. If an agent marks something Resolved and later flips it
back, both rows have to survive, in order. Ticketing systems that let history rows be edited end up
with a record nobody trusts.

There are three places a row could be changed: through the domain model, through EF Core directly,
or by anyone with a database connection.

## Decision

The `RequestStatusHistory` type has no mutators. All of its properties are `init`-only and the only
factory is internal to the aggregate. That closes the first door.

An EF Core `SaveChangesInterceptor` throws if any history entity is tracked as Modified or Deleted.
That closes the second door, before a round trip, with an exception that names the rule.

A PostgreSQL trigger on `request_status_history` raises on `UPDATE` and `DELETE`, with error code
`restrict_violation`. That closes the third door for anyone connecting with `psql`, a migration
script, or a future service written in another language.

The demo reset uses `TRUNCATE`, which does not fire row-level triggers. That is deliberate: the
table can be emptied as a whole, never edited a row at a time.

## Consequences

- A status change is a new row, always. The current status on `service_requests` is a denormalised
  copy that the aggregate keeps in step; the history is the source of truth.
- Correcting a mistaken transition means making another transition (an admin can reopen), with the
  reason written down. That is a feature.
- Cascading a request delete would fail against the trigger. Requests are never deleted, and the
  test suite proves the trigger fires, so this is a guard rather than a bug.
- The integration test `AppendOnlyHistoryTests` exercises all three doors against a real
  PostgreSQL: raw `UPDATE`, raw `DELETE`, an EF modification, and an EF removal.
