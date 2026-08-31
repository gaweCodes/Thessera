# 0005 — Reflection-based discovery, and therefore no trimming and no AOT

- **Status:** Accepted
- **Date:** 2026-08-29

## Context

Registering every handler, projection, mapper and domain event by hand is a second file to maintain
next to every use case, and the second file is the one people forget. The family therefore finds
them: `AddHandlersFrom(assembly)` and `AddDomainEventsFrom(assembly)` scan the assemblies the host
names, the dispatcher and the projection and mapper types are built with `MakeGenericType`, and a
typed key's value is read through an expression tree compiled at run time.

All three techniques are invisible to the IL linker and to ahead-of-time compilation.

## Decision

Keep the scanning, and state the consequence plainly rather than leaving consumers to discover it.
The family is **not trim-safe and not AOT-safe**. The affected public members carry
`[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`, with the messages in `TrimmingMessages`
naming the concrete failure rather than a generic warning — what gets removed, what stops working,
and what to publish without.

## Consequences

- Publish without `PublishTrimmed` and without `PublishAot`. Every package README says so under its
  own **Limits** heading.
- **One consequence deserves naming, because no analyzer reports it.** `HandlerRegistrationCheck`
  verifies that every discovered command and query has exactly one handler — using the same
  reflection the linker has just emptied. On a trimmed build it compares nothing with nothing,
  reports success, and lets the host start. The guard fails open. This is not fixable while
  discovery works this way, which is why it is written down here rather than only in a README.
- Registration stays declarative: adding a use case is one file, and nothing has to be remembered
  afterwards.
- The convention checks in `GaWeCodes.Thessera.Testing` are reflective too — which is fine, because
  none of that ships in a host.

## Alternatives considered

**A source generator that emits the registrations.** Trim-safe, AOT-safe, and the option most
likely to supersede this record one day. It was not taken for the first version because it moves
the discovery rules into a generator that has to be written, tested, and kept in step with the
runtime — and a generator that disagrees with the runtime produces a failure mode worse than the
one it removes.

**Explicit manual registration of every handler.** Safe, boring, and unpleasant in exactly the way
described above. It also makes the startup checks pointless, since the thing they verify would be
the thing the developer just typed.

**Claiming trim support and hoping the common paths survive.** Rejected. The failure is silent —
discovery finds nothing, the host starts, and the first request fails with a missing-service
message that points nowhere near the cause.
