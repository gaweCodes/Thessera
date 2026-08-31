# 0002 — The composition root carries no runtime

- **Status:** Accepted
- **Date:** 2026-08-29

## Context

A transactional outbox needs a message engine, and this family uses Wolverine for it. But a host
that only dispatches commands and delivers domain events in process needs no broker, no database
and no message engine at all. If the composition root depends on the engine, every such host drags
it in anyway — and the family's claim that its layers are separable becomes a claim about naming
rather than about dependencies.

## Decision

`GaWeCodes.Thessera.Core` depends on `GaWeCodes.Thessera.Domain`,
`GaWeCodes.Thessera.Application` and the `Microsoft.Extensions.*.Abstractions` packages. Nothing
else. It names no vendor type anywhere on its public surface.

The runtime is reached through a seam instead: `IRuntimeActivator` with
`Activate(IHostApplicationBuilder, IWiringSnapshot)`, held by `RuntimeActivation`. A store or
transport package announces the runtime it needs through `UseRuntime<TActivator>` on its
registration context, and `RuntimeActivation.GetOrAdd` ensures a host ends up with exactly one — a
store and a transport that both want Wolverine share it rather than fighting over it.

## Consequences

- `AddThessera` has to be called on `IHostApplicationBuilder` whenever a runtime is involved,
  because the activator needs the builder. The `IServiceCollection` overload cannot activate one,
  and `WolverineRuntimeCheck` says so at startup rather than letting the host run without an
  engine.
- The separation is verified, not claimed. `CoreFacadeVendorNeutralityTests` asserts that no facade
  signature and no `using` in the composition root names Marten, EF Core, Npgsql, `RabbitMQ.Client`
  or Wolverine; `PackageMatrixTests` asserts that the vendor-free host projects resolve without
  those assemblies.
- Selecting two different runtimes in one host is an error, with a message that explains why: the
  runtime owns the outbox, the inbox and the local queues, so two of them would each hold half of
  the delivery guarantees.
- **The honest limit.** A transactional outbox has to know the message engine in order to enlist in
  its transaction. Any store adapter therefore references WolverineFx through
  `GaWeCodes.Thessera.Wolverine`, via `IOutboxDurabilityConfigurator`. The claim "the runtime is
  exchangeable" holds only for the case without persistence. It is stated that way in the package
  READMEs on purpose.

## Alternatives considered

**Depend on Wolverine directly in `Core`.** One seam fewer, less indirection, and a considerably
smaller amount of wiring code. It was rejected because it puts a message engine, a code-generation
dependency and a persistence-adjacent library into the graph of a project whose user asked for a
dispatcher.

**A full abstraction over the message engine, so that it is genuinely swappable.** Rejected as
dishonest rather than as impractical. Outbox durability cannot be abstracted without
reimplementing durability itself, so the abstraction would be a promise the code cannot keep — and
a broken promise about delivery guarantees is worse than an acknowledged coupling.

**Letting the host activate the runtime itself, with no activator seam.** Rejected because the
store knows which durability configuration its transaction needs and the host does not; pushing
that outward turns a wiring detail into something every consumer has to get right.
