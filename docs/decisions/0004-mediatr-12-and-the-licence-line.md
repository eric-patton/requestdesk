# 0004. MediatR is pinned to 12.5.0, the last Apache-2.0 release

Date: 2026-09-06. Status: accepted.

## Context

The application layer uses MediatR for request and handler dispatch with a validation pipeline
behavior. From version 13 the package moved to a commercial licence. There is a free tier, but it
requires registering for a licence key and configuring it, and the terms can change.

This repository is MIT licensed and meant to be cloned and run by anyone with `docker compose up`.
A licence key requirement in the dependency tree, even a free one, is friction and a question mark
for a reader.

## Decision

Pin MediatR at 12.5.0, which is Apache-2.0 and works on .NET 10. Record the reason here so nobody
"helpfully" bumps it without knowing why it was held.

## Consequences

- No new MediatR features from 13 onward. Nothing in this system needs them.
- If the pin ever becomes a problem, the dispatch surface used here is small: `ISender.Send`,
  `IRequestHandler<,>` and one `IPipelineBehavior<,>`. Replacing it with a hand-rolled dispatcher
  or another library is a contained change in the application project and its DI registration.
- Every package version in the solution is in `Directory.Packages.props`, so the pin is visible in
  one place rather than buried in a project file.
