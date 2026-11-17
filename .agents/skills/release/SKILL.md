---
name: release
description: Release a new version of Repilot: approve the notes, bump the version, tag, push, and check the GitHub Release and Store draft CI creates. Use when asked to release, ship or tag a version. Do not use for local dev builds (use the desktop skill).
---

# Release

Goal: a tagged version on `main` whose CI runs have created the GitHub Release from approved notes and uploaded a Store draft. Invoking this skill authorises its own commit, tag and push; it still stops for the three things below.

Stop for exactly these:

- **Notes approval.** Show the exact notes and wait for the maintainer, or someone the maintainer has delegated release approval to, to approve or edit them before any file changes. Approval of one draft does not carry to a later release.
- **A dirty tree or a moved branch.** Uncommitted changes, or `HEAD` not equal to `origin/main`. Report it; never commit unrelated work with the release.
- **Failed evidence.** A red `npm run validate`, `npm run package:desktop` or CI run. Report the failure; do not retry around it.

Steps:

1. Check the tree: `git fetch origin`, then `git status` is clean and `git rev-parse HEAD` equals `git rev-parse origin/main`.
2. Choose the version (semver, three parts) and read the range since the last tag: `git log --oneline $(git describe --tags --abbrev=0)..HEAD`. Draft the `en-US` notes as user-facing bullets only, with no heading and no disclosure line (CI adds both), then the six translations (`es-ES`, `fr-FR`, `de-DE`, `zh-CN`, `pt-BR`, `ja-JP`) using the terms in `Repilot/Strings/<lang>/Resources.resw`. The Canadian identities reuse `en-US` and `fr-FR`; they get no files. Each file stays under 1,500 characters (the Microsoft Store limit). STOP for approval.
3. After approval, write `release-notes/<version>.md` and `release-notes/<version>.<identity>.md` for the six translations, and set `<Version>` in `Directory.Build.props`. Run `npm run check:listing` (the listing and these notes), `npm run validate` and `npm run package:desktop` (the unsigned Store package, which compiles both executables). If `check:listing` is not yet part of `validate` in `package.json`, add it in this commit: it waited only for the first notes files.
4. Commit only those files, with the subject `Bump version to v<version>`.
5. Tag and push together: `git tag -a v<version> -m "Repilot <version>"` (annotated, always), then `git push --atomic origin main v<version>`.
6. Watch the "Build and Package" run for the tag (`gh run list --workflow build-msix.yml`, then `gh run watch <id> --exit-status`). It fails if a notes file is missing or too long. Check that the Release is titled `Repilot <version>`, its body is the `en-US` notes, the Get it paragraph and the AI disclosure line, it has no assets, and it is marked Latest.
7. Watch the "Publish to Microsoft Store" run for the same tag (`gh run list --workflow store-publish.yml`). It runs the gate, uploads both architectures at `Tier1012` with the notes as "What's new", and stops at a draft (REL-7); take the draft ID from its summary.
8. Do not submit the draft: that is the maintainer's step. Tell them the draft ID and that submitting is a manual run of "Publish to Microsoft Store" on the tag with `submit_draft` set to it and `no_commit` unchecked. Post-release steps outside this repo (the promo catalog entry, the website page) go to the maintainer as well.

Report: the version, the commit and tag, who approved the notes, each CI run's result with its link, the Release link, and what is left for the maintainer (submitting the Store draft, post-release steps).
