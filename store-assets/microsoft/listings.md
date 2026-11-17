# Microsoft Store listing text

`listings.json` is the listing text in the shared `schemaVersion: 1` / `locales` shape (LIST-3), for the app's nine locale identities: `en-US`, `en-CA`, `es-ES`, `fr-FR`, `fr-CA`, `de-DE`, `zh-CN`, `pt-BR` and `ja-JP` (the same set the package manifest declares). Seven records hold wording; `en-CA` is `{ "copyFrom": "en-US" }` and `fr-CA` is `{ "copyFrom": "fr-FR" }`, matching the app's string folders. Fields are semantic names, not Partner Center field IDs. No publisher reads this file yet, so Partner Center is changed by hand to match it (LIST-9).

The text was taken from the live listing (submission 1152921505702429646, 2026-11-17): descriptions, features, keywords, and screenshot captions from each image's description. Image files and their URLs stay in Partner Center. `shortDescription` was empty live and is new: the `en-US` text is the README tagline, and the six translations were drafted by AI. Every translation in the listing and in the release notes is AI-drafted, without native-speaker review (LOC-12). Translated wording uses the terms in `Repilot/Strings/<lang>/Resources.resw` so the listing matches what people see.

Release notes are not in this file. Each version's "What's new" text is `release-notes/<version>.md` (`en-US`) plus one `release-notes/<version>.<identity>.md` per other wording catalog, the same text the GitHub Release uses (REL-6, LIST-10); `store-publish.yml` sends each listing locale its notes.

`npm run check:listing` checks this file and the current version's notes: the locales and aliases, every field, the limits below, matching caption counts, no em dashes or retired names, and that the README tagline is the first sentence of the `en-US` short description. It reads the wording catalogs from the `Repilot/Strings/<lang>` folders (`catalog_layout: "folders"` in `windows-build.json`). It is not part of `npm run validate` yet, because the current version (1.0.22) has no notes files; it joins `validate` in the release bump that adds the first ones.

## Limits

| Text | Limit | Longest |
| --- | --- | --- |
| Full description | 4,000 characters | 2,015 (fr-FR) |
| Short description | 270 characters | 167 (de-DE) |
| Features | Up to 20, each up to 200 characters | 9, longest 72 (de-DE) |
| Keywords | Up to 7, each up to 40 characters, 21 words combined | 7 per locale, at most 20 words (pt-BR) |
| Screenshot captions | One per screenshot, up to 200 characters | 3 per locale |
| Release notes (`release-notes/`) | 1,500 characters | none yet |

## Claims to keep true

- The action runs from a small handler that Windows starts on each key press and that exits afterwards; there is no service, tray app or startup task. The settings app runs only while open.
- The five actions: keyboard shortcut (including system ones such as Alt+Tab, Win+Tab and Ctrl+Shift+Esc), text (typed regardless of keyboard layout), app, file or link (installed apps searchable, with icons; paths, websites and `ms-settings:` links), Windows function (searchable by name), and nothing.
- Works with the dedicated Copilot key and Windows logo key + C. Assigning the key needs the Store (packaged) install.
- The settings app follows the Windows display language or a choice in Settings among the nine; a change needs the app reopened, and regional formats are not changed. Canadian English and French share wording with US English and French for France. (Shipped after 1.0.22; see below.)
- No account or sign-in; settings are stored locally. No telemetry or crash reports.
- Requires Windows 11 (the manifest's minimum, build 22000).

Known gap: the live listing (and its "What's new") already describes the nine app languages, but the live package is 1.0.22, which does not have them. The next release ships them.

## Console-only fields

| Field | Value |
| --- | --- |
| Store ID | 9PB5FJ08PNVJ (https://apps.microsoft.com/detail/9pb5fj08pnvj) |
| Package identity | `27766TechnicallyReal.CopilotKeyRemapper`, publisher `CN=C21E6CEF-D0D1-4497-93F9-3718D054DA0E` (permanent, BRAND-6; dev builds use `TechnicallyReal.Repilot`) |
| Category | Productivity |
| Developer name (devStudio) | Dynamic Solutions (BRAND-3). The live listing still shows the previous value; no workflow sets this field, so it is changed in Partner Center with the next submission |
| Privacy policy | https://www.dynamic-solutions.ca/privacy/repilot |
| Price | US $0.99 (`Tier1012`) |
