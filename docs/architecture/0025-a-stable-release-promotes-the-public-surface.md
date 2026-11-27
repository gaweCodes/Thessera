# 0025 — A stable release promotes the public surface, and its number must match what changed

- **Status:** Accepted
- **Date:** 2026-11-27

## Context

[0008](0008-public-surface-is-a-tracked-file.md) puts the public surface into
`PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, but never said what separates the two. During
the previews the answer was "nothing": every addition went straight into the shipped file, and
`PublicApiFilesTests` failed on any entry left in the unshipped one. That was right while no
version promised anything — the shipped file was the 1.0 surface under construction.

From `1.0.0` on, the shipped file means something else: the surface a stable release promised.
Semantic versioning then turns every change to it into a version decision — an addition needs at
least a minor release, a removal or a changed signature a major one. With everything written
straight into the shipped file, the diff since the last release shows that a change happened, but
nothing connects it to the number on the next tag, and a breaking change could still go out as a
patch.

[`AnalyzerReleases.Shipped.md`](../../src/GaWeCodes.Thessera.Analyzers/AnalyzerReleases.Shipped.md)
and its unshipped twin have the same shape for the analyzer rules, and had the opposite habit: every
rule has sat in the unshipped file since the first preview.

## Decision

The shipped files hold what the last stable release published; the unshipped files hold what
changed since. A stable release moves the one into the other, and `release.yml` refuses a stable tag
whose number does not fit the move.

- **Between stable releases**, a new public member goes into `PublicAPI.Unshipped.txt`, which is
  where the analyzer's code fix puts it. A removed or changed member is not deleted from
  `PublicAPI.Shipped.txt`; the change is written as a `*REMOVED*` line in the unshipped file, which
  is the analyzer's own convention for it. A new or changed analyzer rule goes into
  `AnalyzerReleases.Unshipped.md`.
- **A prerelease** — `-preview.N`, `-rc.N` — promotes nothing. Its unshipped files carry everything
  since the last stable release.
- **A stable release promotes** in the same change as its changelog section: every unshipped entry
  is added to the shipped file, every `*REMOVED*` line deletes its counterpart there, and the
  analyzer rules move into `AnalyzerReleases.Shipped.md` under `## Release x.y.z`. The unshipped
  files are left empty.
- **`release.yml` checks it**, for stable tags only, in the step "Refuse a stable release the public
  surface does not allow". It fails while any unshipped file still carries an entry. It then
  compares against the last stable tag: a line removed from any `PublicAPI.Shipped.txt` requires a
  new major version, an added line at least a new minor one. A rule that
  `AnalyzerReleases.Shipped.md` newly lists with severity `Error`, or that it raises to `Error`,
  requires a new major version too: it can turn a consumer's green build red without any change on
  their side.
- **The previews' surface is the 1.0 baseline.** What the previews wrote into the shipped files
  stays there; this rule applies from `1.0.0-rc.1` on, and `1.0.0` has no earlier stable release to
  compare against.

`PublicApiFilesTests.NoUnshippedFile_StillCarriesAPendingEntry` is removed: an unshipped entry is
now the normal state between releases, not an error.

## Consequences

- The unshipped files answer "what does the next release change?" at any time, and a release
  candidate shows the same surface its stable release will promote.
- A breaking change cannot be tagged as a minor or patch release; the release fails before anything
  is packed for nuget.org.
- A stable release costs one more edit before the tag, and a forgotten promotion is only noticed when
  the release workflow runs, not in the pull request.
- A new analyzer rule of severity `Error` costs a major release, however small the rule. A rule that
  should ship sooner starts as `Warning` and is raised to `Error` with the next major version. A
  removed rule, or one lowered below `Error`, only relaxes a consumer's build and needs no new major
  version.
- A nullability change, or a member moved from one package to another, counts as a removal. Both
  can break a consumer, so the check errs on the safe side.
- Nothing stops a pull request from editing a shipped file directly between releases. It shows up
  in review as an edit to a file that normally changes only at a release, and the release check
  still compares against the tag, not against the file's history.

## Alternatives considered

**Keep writing everything into the shipped file**, as during the previews. Rejected: it leaves the
diff since the last tag as the only record of what a release changes, and nothing ties that record
to the version number.

**Compare the published packages with an assembly-diff tool** such as package validation's baseline
check. It catches the same breaks from the binaries and needs no promotion step. Rejected for now:
it duplicates what the tracking files already record, and [0008](0008-public-surface-is-a-tracked-file.md)
chose those files so that the change is argued in the pull request, not at release time.

**Promote after the release**, in the first change following the tag, as the Roslyn repository
does. It lets the release check read the unshipped files directly. Rejected: the tag would then
point at a commit whose shipped files do not describe what it shipped.
