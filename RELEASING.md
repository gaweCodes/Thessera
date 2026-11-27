# Releasing Thessera

The version is derived from Git tags by MinVer (`MinVerTagPrefix` is `v`); there is no version
number to edit anywhere in the repository.

1. Add a `## [x.y.z] - YYYY-MM-DD` section to [`CHANGELOG.md`](CHANGELOG.md), moving the relevant
   `[Unreleased]` entries under it and leaving `[Unreleased]` empty above. Add the comparison link
   at the bottom of the file next to the other version links.
2. For a stable release only, promote the public surface in the same change
   ([ADR 0025](docs/architecture/0025-a-stable-release-promotes-the-public-surface.md)):
   - In every package under `src`, add each line of `PublicAPI.Unshipped.txt` to
     `PublicAPI.Shipped.txt`; a `*REMOVED*` line instead deletes its counterpart there. Leave only
     `#nullable enable` in the unshipped file.
   - Move the rule tables of `AnalyzerReleases.Unshipped.md` into `AnalyzerReleases.Shipped.md`
     under a new `## Release x.y.z` heading, and leave the unshipped file empty.
   - Choose the version to fit: a removed or changed member, and an analyzer rule that is new with
     severity `Error` or raised to it, need a new major version; an added member at least a new
     minor version.
3. Push that change to `main` through the normal pull request path and get it merged.
4. From `main`, create **one** annotated tag: `git tag -a v1.2.3 -m "v1.2.3"`, then
   `git push origin v1.2.3`.
5. The tag push triggers `release.yml`: it restores, builds, runs the full test suite with
   containers required, packs, and refuses to continue if the resolved package version does not
   match the tag itself. For a stable tag it also refuses an unshipped entry that was not promoted,
   and a version number too small for how `PublicAPI.Shipped.txt` and `AnalyzerReleases.Shipped.md`
   changed since the last stable tag. Then it publishes every package to nuget.org through Trusted
   Publishing.
6. Watch the `release` workflow run to completion, then open each package's nuget.org page and
   confirm the new version, the README, and the license are what you expect. A published version
   can be unlisted but never deleted, so this check happens after every release, not only the
   first one.

Nothing is ever packed or pushed from a local machine; `release.yml` is the only path to
nuget.org.

## Prereleases

Tags of the form `v1.0.0-preview.1` or `v1.0.0-rc.1` follow the same path, without step 2: a
prerelease promotes nothing, so its unshipped files show everything the stable release will
promote. They let VitalSync (or anyone else) test against a real published package before the
stable version exists. SemVer orders `preview` before `rc`, and both before the stable version.

## One-time setup

- A nuget.org Trusted Publishing policy for this repository, pointing at the workflow file
  `release.yml` and the GitHub environment `release`.
- A GitHub environment named `release` (`Settings > Environments`) that the `release` job runs
  under; add required reviewers there if a manual approval gate before publishing is wanted.
- The `NUGET_USER` repository or environment secret, holding the nuget.org username (not email)
  used by the Trusted Publishing login step.
- The `GaWeCodes.` package ID prefix reserved on nuget.org before the first package under that
  prefix is ever pushed.
