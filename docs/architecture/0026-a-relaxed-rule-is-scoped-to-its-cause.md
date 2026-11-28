# 0026 — A relaxed rule is scoped to its cause

- **Status:** Accepted
- **Date:** 2026-11-28

## Context

The build runs every analyzer at the highest level and turns every warning into an error
(`Directory.Build.props`). A few rules still had to give way. Over time those exceptions ended up
in four different places: `NoWarn` in each test project, a solution-wide severity in the root
`.editorconfig`, scoped `.editorconfig` files, and `SuppressMessage` or `#pragma` in code. Nothing
said which place a new exception belongs in. That had consequences:

- `CA1040` and `CA1030` were off for the whole solution, although only a handful of `Domain` and
  `Application` types needed it. A new empty interface or a new `Raise…` method anywhere else went
  unnoticed.
- `CA1711` and `CA1859` were off for all tests, although every hit could simply be fixed.
- An `xUnit2024` suppression on a test class no longer suppressed anything.
- `CA2007` was relaxed through `NoWarn` in every test project, outside the `.editorconfig` the
  instructions name as the only place for a scope-wide relaxation.

A separate question came up with MSBuild's own warnings (`MSBxxxx`, `NETSDKxxxx` and the warnings
raised by package targets). `TreatWarningsAsErrors` does not cover them. `MSBuildTreatWarningsAsErrors`
does, but SourceLink then fails every local build whose Git remote is an SSH host alias, because it
does not recognise the alias as GitHub. That warning has no code, so it cannot be exempted
selectively.

## Decision

A rule is relaxed exactly as wide as the reason for relaxing it, and no wider. Where it is relaxed
follows from that reason.

**When the reason holds for every file of a scope, the rule is relaxed for that scope**, in that
scope's `.editorconfig`, never through `NoWarn`:

- `tests/.editorconfig`
  - `CA1707`: test names follow `Method_Case_Result`, with underscores.
  - `CA2007`: xUnit runs tests on its own `SynchronizationContext` and warns against
    `ConfigureAwait(false)` in a test method (`xUnit1030`), so the two rules contradict each other.
  - `CA1515`: xUnit requires public test classes (`xUnit1000`). Test projects on
    Microsoft.Testing.Platform are executables, so the rule applies to them.
  - `CA1812`: xUnit, the DI container and the test hosts instantiate test types through reflection.
- `src/GaWeCodes.Thessera.Domain/.editorconfig`
  - `CA1033`: the `Domain` package implements its infrastructure contracts (`IStateOwner`,
    `IDomainEventOwner`, `IDomainEventRaiser`, `IEventSourcedAggregateRoot<TKey>`) explicitly, so that
    an aggregate's author never sees them. Giving them a protected alternative, as the rule asks,
    would expose exactly what the explicit implementation hides.

**When the reason holds for specific members only, the rule is suppressed on those members**, with
`SuppressMessage` and a `Justification`, or with a `#pragma` pair around the one line:

- `CA1812` in a package's `GlobalSuppressions.cs`: an internal type that only DI or reflection
  instantiates.
- `IDE0130` on the entry-point extension files that deliberately live in the root
  `GaWeCodes.Thessera` namespace.
- `CA2225` on the `Result<TResult>` conversion operators.
- `CA1040` on the marker contracts `ICommand`, `ICommand<TResult>`, `IQuery<TResult>` and
  `IDomainEvent`. Their type is their whole meaning, and the generic ones carry the result type
  that handler resolution binds against.
- `CA1030` on `RaiseEvent` in `AggregateRoot` and `Entity`, and on `IDomainEventRaiser.Raise`.
  "Raising" a domain event is the vocabulary of [`glossary.md`](../glossary.md), not a .NET event.
- `CA1508` on the equality tests that compare an aggregate or entity with `null`, on the test
  method rather than the whole class.

**A rule whose hits can be fixed is not relaxed.** `CA1711` and `CA1859` apply to tests again:
the collection definitions are named `…CollectionDefinition`, and the private helpers return their
concrete type. Tests that are named after `RaiseEvent` start with `Raising…` instead, so `CA1030`
applies to tests too. The container fixtures catch only when the run may skip
(`catch (Exception) when (!ContainerRequirement.ContainersRequired)`), so `CA1031` applies to tests
as well.

**MSBuild's own warnings become errors on CI only.** `MSBuildTreatWarningsAsErrors` is set when
`GITHUB_ACTIONS` is `true`. CI clones over HTTPS, so SourceLink resolves the remote there.

The root `.editorconfig` relaxes no rule. `Examples/` is outside this decision: it resets MSBuild
strictness on purpose, to build the way a consumer does.

## Consequences

- A new empty interface, a new `Raise…` method or a new explicit gap in a test is reported again,
  everywhere except where the reason is written down.
- A suppression in code is visible at the member it affects, with its reason next to it. Reviewing
  a change shows a new one.
- More `SuppressMessage` attributes exist under `src`. Each of them is an exception in the sense of
  the contributing rules and has to be justified like one.
- A collection definition, a helper or a test name has to follow a rule that used to be off. The
  contributor pays that cost, not the reader.
- When `THESSERA_REQUIRE_CONTAINERS` is set and a container cannot start, the fixture's own
  exception now fails the run as it is, without the wrapping message that named the variable.
  `ContainerRequirement.ThrowIfRequired`, which produced that message, is gone.
- A local build can contain an MSBuild warning that CI then refuses. That is the price for not
  failing every local build over a remote alias.

## Alternatives considered

**Keep relaxing rules solution-wide when they are noisy.** Rejected: it hides new violations in
packages the reason never applied to, which is how `CA1040` and `CA1030` came to cover the whole
solution.

**Suppress everything per member, including in tests.** Rejected for the rules whose reason is a
property of the scope. `CA1707` alone would need an attribute on nearly every test method, and the
attributes would only repeat the naming convention.

**Fix `CA1812` with factory registrations** (`services.AddSingleton<IClock>(_ => new SystemClock())`).
Rejected: a factory registration has no implementation type. Wolverine's code generation then
falls back to service location, and constructor dependencies would have to be resolved by hand.

**Turn the marker interfaces into attributes, as `CA1040` suggests.** Rejected: an attribute cannot
carry `TResult` into a generic constraint, and the change would break the public surface.

**Rename `RaiseEvent`.** Rejected: it is the protected member every consumer aggregate calls, and
"raise" is the vocabulary of the glossary.

**Hide the `null` in the equality tests behind a helper, so that `CA1508` cannot see it.**
Rejected: it would defeat the analyzer instead of saying why the code is what it is.

**Make MSBuild warnings errors everywhere**, and require an HTTPS remote locally. Rejected:
SourceLink's warning has no code that could be exempted, so every contributor using an SSH alias
would get a build that cannot be made green.
