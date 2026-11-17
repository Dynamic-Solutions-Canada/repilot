# Store assets

| Folder | Holds |
| --- | --- |
| `microsoft/listings.json`, `listings.md` | The Store listing text in all nine locales, and the claims it makes (see `listings.md`) |
| `manifest.json` | Every Store screenshot in listing order: its raw source, theme, en-US caption and alt text (LIST-8) |
| `raw/microsoft/` | The raw captures: the app's window at 1:1, taken on the Windows test VM |
| `microsoft/screenshots/` | The listing screenshots, rendered from the raw captures; the README shows these too (ASSET-10) |

## Screenshots

Captured from the real installed app with synthetic settings, never from a personal machine (LIST-8). The captions in `manifest.json` are the en-US `screenshotCaptions` in `listings.json`, in the same order and count; each other locale has its own translation there.

1. **Capture** on the Windows 11 test VM with the windows-build-tools app-test recipe: install a dev package, then run the capture script (`shots.ps1`, kept with the release-prep notes) in the desktop session. It writes each shot's settings (theme, action), opens the window at 1040 x 700 and saves the window only. Copy the results into `raw/microsoft/` under the manifest's names.
2. **Render** with `npm run shots:render`: each raw capture is centred at 1:1, never scaled, on a 1366 x 768 background in its theme and written without an alpha channel to `microsoft/screenshots/` (ASSET-9). Reframing or recaptioning needs no recapture.
3. **Check** with `npm run check:screenshots` (part of `npm run validate`): every manifest entry exists at 1366 x 768 or larger with a caption and alt text, no stray file is present, and the count is 1 to 10.
4. **Sync** to Partner Center by hand with the next submission: upload the files in order and paste each locale's captions from `listings.json`.

A UI change that shows in a screenshot means recapturing that shot; the release skill's checklist says when.
