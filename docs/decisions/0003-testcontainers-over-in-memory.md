# 0003. Integration tests run against a real PostgreSQL, not an in-memory provider

Date: 2026-09-06. Status: accepted.

## Context

EF Core ships an in-memory provider and SQLite can run in memory. Both are tempting for tests
because they start instantly and need nothing installed. Both also lie.

Things this system depends on that an in-memory provider does not do: the sequence-backed default
that assigns reference numbers, the trigger that protects the status history, `xmin` as a
concurrency token, `ILIKE` with an escape character, `FILTER` on aggregates, `percentile_cont`,
`unnest`, and the `TRUNCATE ... CASCADE` the demo reset uses. A test suite that passes against a
fake and fails against the real thing is worse than no suite.

## Decision

The API integration tests use Testcontainers to start `postgres:17-alpine` once per test run. The
application is hosted with `WebApplicationFactory`, pointed at the container, and allowed to run its
own migrations and seed. Tests then talk to it over HTTP like a client would.

Domain and application tests stay pure: no database, no container, milliseconds.

## Consequences

- The integration suite needs Docker. Locally that is Docker Desktop; on GitHub's hosted runners
  it is already there. The suite takes about ten seconds including container start.
- The tests exercise the real migration, the real trigger and the real SQL. The
  `has-pending-model-changes` step in CI covers the case where the model and the snapshot drift.
- The 49-case transition matrix runs twice: once in the domain tests, as the specification, and once
  over HTTP against the container, as proof the API enforces it.
