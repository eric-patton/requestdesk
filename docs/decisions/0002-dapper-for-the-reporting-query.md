# 0002. The reporting query is written in SQL with Dapper

Date: 2026-09-06. Status: accepted.

## Context

Everything else in the system goes through EF Core: the aggregate is loaded and saved through it,
the list page is composed as LINQ and translated to one SQL statement with paging. That is the
right default. It is type checked, migrations stay in step with the model, and the code reads as
intent rather than as SQL strings.

The dashboard is different. It wants, in one round trip:

- counts for every status, including the ones with zero requests;
- open requests by priority, again including zeros;
- five headline numbers, three of which are conditional counts over the same table;
- a median of the open requests' age, which is a percentile aggregate;
- open requests bucketed by age.

In LINQ that is four or five queries, and the percentile does not translate at all.

## Decision

`SummaryQuery` is one SQL statement with several result sets, executed with Dapper over the same
connection EF Core is using in that request. It uses `FILTER` on aggregates, `percentile_cont` for
the median, `unnest` to generate the full set of statuses so the client never has to fill gaps,
and a `CASE` to bucket ages.

It is the only place in the code base that contains SQL. The class comment says so and points here.

## Consequences

- The dashboard is one query, and the SQL is readable next to the domain it describes.
- Column names in that SQL are a dependency on the schema that EF Core does not see. The
  integration test `ReportTests` runs the query against the real schema on every build and checks
  the numbers add up, so a rename breaks the build rather than the dashboard.
- Anyone reading the code learns the rule from the exception: one query in SQL, with a reason, is
  fine. SQL scattered through handlers is not.
