# Store assets

| Folder | Holds |
| --- | --- |
| `microsoft/listings.json`, `listings.md` | The Store listing text in all nine locales, and the claims it makes (see `listings.md`) |
| `manifest.json` | Every Store screenshot in listing order: its raw source, theme, en-US caption and alt text (LIST-8) |
| `raw/microsoft/` | The raw captures: the app's window at 1:1, taken on the Windows test VM |
| `microsoft/screenshots/` | The listing screenshots, rendered from the raw captures; the README shows these too (ASSET-10) |

## Screenshots

Captured from the real installed app with synthetic settings, never from a personal machine (LIST-8). The captions in `manifest.json` are the en-US `screenshotCaptions` in `listings.json`, in the same order and count; each other locale has its own translation there.

1. **Capture** on the Windows 11 test VM with the windows-build-tools app-test recipe: install a dev package, then run the capture script (`shots.ps1`, kept with the release-prep notes) in the desktop session. It writes each shot's settings (dark theme, action), opens the window at 1040 x 700 and saves the window only. Copy the results into `raw/microsoft/` under the manifest's names.
2. **Render** with `npm run shots:render` (winui-app-kit's renderer; its README has the rules): each raw capture is centred at 1:1, never scaled, on the Windows 11 default dark wallpaper at 1366 x 768 and written without an alpha channel to `microsoft/screenshots/` (ASSET-9). Every shot is in dark mode (LIST-8). The wallpaper is read from the Windows install at render time and never committed. Reframing or recaptioning needs no recapture.
3. **Check** with `npm run check:screenshots` (part of `npm run validate`): every manifest entry is dark and exists at exactly 1366 x 768 as 8-bit RGB without alpha, with a caption and alt text; the captions match the en-US `screenshotCaptions`; no stray file is present; and the count is 1 to 10.
4. **Sync** to Partner Center with the next submission: the files in order and each locale's captions from `listings.json`. Listing images go into the same upload archive as that submission's package, never a separate upload: a second archive replaces the package the release run uploaded (seen with 1.1.0).

A UI change that shows in a screenshot means recapturing that shot; the release skill's checklist says when.
