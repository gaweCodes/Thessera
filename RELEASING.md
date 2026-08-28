# Releasing Thessera

The version is derived from Git tags by MinVer (`MinVerTagPrefix` is `v`); there is no version
number to edit anywhere in the repository.

1. Add a `## [x.y.z] - YYYY-MM-DD` section to [`CHANGELOG.md`](CHANGELOG.md), moving the relevant
   `[Unreleased]` entries under it and leaving `[Unreleased]` empty above. Add the comparison link
   at the bottom of the file next to the other version links.
2. Push that changelog change to `main` through the normal pull request path and get it merged.
3. From `main`, create **one** annotated tag: `git tag -a v1.2.3 -m "v1.2.3"`, then
   `git push origin v1.2.3`.
4. The tag push triggers `release.yml`: it restores, builds, runs the full test suite with
   containers required, packs, refuses to continue if the resolved package version does not match
   the tag itself, then publishes every package to nuget.org through Trusted Publishing.
5. Watch the `release` workflow run to completion, then open each package's nuget.org page and
   confirm the new version, the README, and the license are what you expect. A published version
   can be unlisted but never deleted, so this check happens after every release, not only the
   first one.

Nothing is ever packed or pushed from a local machine; `release.yml` is the only path to
nuget.org.

## Preview releases

Tags of the form `v1.0.0-preview.1` follow the same path and let VitalSync (or anyone else) test
against a real published package before `v1.0.0` exists.

## One-time setup

- A nuget.org Trusted Publishing policy for this repository, pointing at the workflow file
  `release.yml` and the GitHub environment `release`.
- A GitHub environment named `release` (`Settings > Environments`) that the `release` job runs
  under; add required reviewers there if a manual approval gate before publishing is wanted.
- The `NUGET_USER` repository or environment secret, holding the nuget.org username (not email)
  used by the Trusted Publishing login step.
- The `GaWeCodes.` package ID prefix reserved on nuget.org before the first package under that
  prefix is ever pushed.
