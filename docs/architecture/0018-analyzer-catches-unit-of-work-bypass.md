# 0018 — An analyzer rule catches a handler bypassing the unit of work

- **Status:** Accepted
- **Date:** 2026-09-16

## Context

`IUnitOfWork` commits an aggregate's state (or, on an event store, its appended events) and the
domain-event envelopes those changes raised into the outbox in one transaction, once per command —
that single commit is what
[`NoPublishOnFailedCommitTests`](../../tests/GaWeCodes.Thessera.Tests.PackageConventions/NoPublishOnFailedCommitTests.cs)
now proves empirically for both stores: a commit that fails publishes nothing, for either the state
change or its event. `IRepository<TAggregate, TKey>` is deliberately narrow to protect that guarantee
— `GetByIdAsync` and `AddAsync` only, no `Save` — so a handler has no repository-shaped way to commit
early.

Nothing stops a handler from sidestepping the repository altogether. `TContext` (an EF Core
`DbContext`-derived type) and Marten's `IDocumentSession` are both ordinary scoped DI services with no
guard against being injected straight into a command handler's constructor alongside, or instead of,
`IRepository<,>`. A handler that does this and calls `SaveChangesAsync` itself commits that change in its own, separate transaction — one with no
outbox row. If the process stops before the pipeline's own, later commit runs, that change is durable
but its domain event is never published; if the pipeline's commit does still run afterwards, whether
the event is published at all depends on store-specific, unreliable timing (a second EF Core
`SaveChanges` that may find nothing left to save, or a second Marten append at a stream version the
manual save already moved past). Either way, this is a silent hole in exactly the guarantee this
family exists to hold — and nothing before this record catches it before a host running that handler
finds out the hard way, in production, the first time a command commits and its event does not show
up.

This has the same shape as [0016](0016-one-store-per-aggregate-not-per-host.md)'s `THSS0007`: a
constraint the runtime and the type system cannot enforce on their own — a handler's constructor is
free to ask for anything registered in DI — but that a compile-time analyzer can, because the
violation is visible in a handler's declared parameter types alone, with no need for the DI container
to actually be assembled.

## Decision

A new Roslyn analyzer rule, `THSS0008`, in `GaWeCodes.Thessera.Analyzers`, flags a command handler
(a type implementing `ICommandHandler<>`/`ICommandHandler<,>`) whose constructor injects a parameter
that derives from (or is) `Microsoft.EntityFrameworkCore.DbContext`, or implements (or is) Marten's
`IDocumentSession`. Both vendor type names are resolved dynamically, through
`Compilation.GetTypeByMetadataName`, exactly like every other rule in this package resolves
`GaWeCodes.Thessera.Domain`'s own types — the analyzer package still has zero package or project
reference to EF Core, Marten, or `GaWeCodes.Thessera.Domain`, and silently reports nothing for a
compilation that does not reference the relevant vendor assembly at all.

The rule flags the dependency itself, unconditionally, mirroring `THSS0007`'s own "no escape hatch"
philosophy: it does not matter whether the handler also injects `IRepository<,>`, and it does not
matter whether the `DbContext`/`IDocumentSession` would only ever be used for a read in that
particular handler — the fix is to restructure (split the responsibility, or move the read into a
query handler), not to suppress the rule. There is one deliberate exception on each side. Marten's
`IQuerySession` — the supertype `IDocumentSession` extends, with no `SaveChangesAsync` at all — is not
flagged; it is safe by construction, so a handler injecting it has already done the safe thing. Query
handlers (`IQueryHandler<,>`) are out of scope entirely: they have no unit of work to bypass, and
injecting a `DbContext` directly for a read — typically with `AsNoTracking()` — is this family's own
documented pattern, exercised by `ListAccountsHandler` in `Examples/MixedPersistence` and its
`StateStored`/`StateStoredWithMessaging` siblings.

## Consequences

- A command handler that already only injects `IRepository<TAggregate, TKey>` — every handler in this
  repository's own examples and test fixtures, before this record — is unaffected; building the
  solution and `Examples.slnx` after adding this rule reports zero new diagnostics.
- A handler that does inject a raw session now fails to build with a diagnostic naming the handler,
  the injected type, and which store it belongs to ("EF Core" or "Marten"), rather than shipping a
  silent gap in the outbox guarantee that only a missing published event in production would reveal.
- This is the eighth rule in `GaWeCodes.Thessera.Analyzers`; its README, and the family's own package
  list repeated in every other package's README, are updated from "six"/"seven" to "eight" alongside
  this record.
- Like every other rule in this package, `THSS0008` is `Error` severity with no suppression attribute
  offered — consistent with `THSS0001`–`THSS0007`, the family does not offer an escape hatch for its
  own analyzer rules.

## Alternatives considered

**Leave this to code review and documentation alone**, the same way the family already documents
`IRepository<,>`'s narrow shape and the reasoning behind it. Rejected: the whole point of
`GaWeCodes.Thessera.Analyzers` is to move exactly this kind of violation — visible in a constructor's
parameter list, catchable without a running host — out of review and into the build, the same
argument [0014](0014-a-compile-time-analyzer-catches-four-startup-checks.md) already made for its
first four rules.

**Flag any handler injecting a raw session only if it does not also inject `IRepository<,>`.**
Rejected: a handler mixing both is arguably worse, not better — it has a legitimate way to commit
already, and adding the raw session on top has no purpose except to bypass it. Flagging unconditionally
keeps the rule's shape identical to `THSS0007`'s own choice, and removes any ambiguity about whether
"it also has a repository" makes the raw session dependency safe.

**Flag `IQuerySession` too**, on the theory that a command handler should not read outside its own
aggregate either. Rejected: `IQuerySession` has no `SaveChangesAsync`; it cannot split the unit of
work's commit no matter how it is used, so flagging it would not be catching a bypass of the
guarantee this record protects — it would be a different, broader rule about read/write separation
that this record does not attempt to make.
