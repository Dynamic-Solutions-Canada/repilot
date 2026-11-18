# Store assets

| Folder | Holds |
| --- | --- |
| `microsoft/listings.json`, `listings.md` | The Store listing text in all nine locales, and the claims it makes (see `listings.md`) |
| `manifest.json` | Every Store screenshot in listing order: its raw source, theme, en-US caption and alt text (LIST-8) |
| `raw/microsoft/` | The raw captures: the test VM's whole desktop at 1:1 around the app's window, each with the `.json` sidecar the capture helper writes |
| `microsoft/screenshots/` | The listing screenshots, rendered from the raw captures; the README shows these too (ASSET-10) |

## Screenshots

Captured from the real installed app with synthetic settings, never from a personal machine (LIST-8). The captions in `manifest.json` are the en-US `screenshotCaptions` in `listings.json`, in the same order and count; each other locale has its own translation there.

1. **Capture** on the Windows 11 test VM with the windows-build-tools app-test helpers, from this repo's root: install a dev package, run `screenshot-desktop.ps1 -Mode Prepare` (dark mode, 1920 x 1080, the dark wallpaper, window effects on), then the capture script (`shots.ps1`, kept with the release-prep notes) in the desktop session. It writes each shot's settings (dark theme, action), centres the focused window on the work area and saves the whole desktop with its `.json` sidecar (`Save-DesktopCapture`). Copy each `.png` and `.json` into `raw/microsoft/` under the manifest's names, then end the visit with `app_test_clean`.
2. **Render** with `npm run shots:render` (winui-app-kit's renderer; its README has the rules): each capture is cropped to 1366 x 768 around the window, with nothing composited, shaved or scaled, and written without an alpha channel to `microsoft/screenshots/` (ASSET-9). Every shot is in dark mode (LIST-8). Recaptioning needs no recapture; a new look does.
3. **Check** with `npm run check:screenshots` (part of `npm run validate`): every capture has its sidecar and was taken dark, at 100% scale, with effects on and the dark wallpaper; the window in each screenshot is dark and fits its crop; every screenshot was written by the renderer at exactly 1366 x 768 as 8-bit RGB without alpha, with a caption and alt text; the captions match the en-US `screenshotCaptions`; no stray file is present; and the count is 1 to 10.
4. **Sync** to Partner Center with the next submission: the files in order and each locale's captions from `listings.json`. Listing images go into the same upload archive as that submission's package, never a separate upload: a second archive replaces the package the release run uploaded (seen with 1.1.0).

A UI change that shows in a screenshot means recapturing that shot; the release skill's checklist says when.
