# 0003 — The stream key is a wire format

- **Status:** Accepted
- **Date:** 2026-08-29

## Context

An aggregate's identity does not stay inside the process. It becomes the stream key in the event
store, it appears in persisted rows, and it travels on every domain-event envelope and therefore
through the outbox. Once written, that text is permanent: everything already stored is addressed by
it.

If the text is whatever `ToString()` produces, it is decided by the runtime, the current culture
and the key's underlying value type — none of which the aggregate's author is thinking about, and
all of which can change without anyone editing the aggregate. A `decimal` keeps trailing zeros, an
`enum` writes a member name, a `DateTime` follows a calendar convention. When one of those changes,
existing streams become unreachable and nothing reports it.

## Decision

`EntityKeyFormatter` renders an identity as `<aggregate-name>/<key-value>`, with `/` as the
separator (`EntityKeyFormatter.StreamKeySeparator`). The aggregate name comes from
`[AggregateName]` and is validated as a contract name. The value rendering is pinned per type and
nothing else is accepted:

- `Guid` — format `D`, invariant culture.
- `string` — verbatim, but **rejected** if it contains `/`, because such a value would let two
  different aggregates address the same stream.
- `int` and `long` — invariant decimal, and **negatives are rejected**: a negative identity is
  almost always an uninitialised value or an error marker that would quietly open a stream of its
  own.
- Anything else — rejected outright, with a message that names why.

An empty key (`IEntityKey.IsEmpty`) is rejected too: it would produce a key that looks valid and is
shared by every unidentified aggregate of that type.

## Consequences

- Only four value types can back a typed key. An identity that is naturally a `decimal`, an `enum`
  or a date has to be modelled as one of the four.
- The rejections are loud errors at the moment of formatting, which is early — before anything is
  written — rather than a different string that reads as correct.
- `[AggregateName]` is mandatory. An aggregate without it is refused rather than silently named
  after its CLR type, and renaming the attribute value orphans every stream already written under
  the old name. Renaming the C# type costs nothing, which is the entire point of writing the name
  down.
- `PersistedSchema` in `GaWeCodes.Thessera.Testing` renders the stream-key shape into an approval
  snapshot, so a change to any of this shows up as a failing test in a pull request rather than as
  unreachable data in production.

## Alternatives considered

**Use `ToString()` on the key.** Free, obvious, and the source of exactly the failure this record
exists to prevent. The failure is invisible at the moment it is introduced and only surfaces as
data that cannot be found.

**Accept every value type, with a documented default rendering per type.** Rejected because the
defaults are the problem, not the documentation of them. A documented default is still a default,
and the day the framework changes one, the document is what turns out to have been wrong.

**Make the format configurable per aggregate or per host.** Rejected because it turns a wire format
into a per-host decision. Two services reading the same store could then disagree about how to
address the same stream, and the disagreement would look like missing data.
