---
name: desktop
description: Build, verify, or install Repilot's Windows desktop app from WSL through the shared disposable Windows build bridge. Use for local desktop development, not releases or Store publishing.
---

# Windows desktop development

Goal: the requested build, compile check or install, run through the shared bridge with evidence of the result.

1. Read the [shared Windows build tools](https://github.com/Dynamic-Solutions-Canada/windows-build-tools) guide for how the bridge stages, signs and installs, and the Commands section of [AGENTS.md](../../../AGENTS.md) for what each command proves here.
2. Make sure the submodules are present (`git submodule update --init`); a build without them fails in ways that look like code errors.
3. Pick the root npm command that matches the request: `verify:desktop` to compile only, `build:desktop` for a signed dev package (do not install unless asked), `package:desktop` for an unsigned Store package, `install:desktop` to build, update in place and launch. "Rebuild and try it" means `install:desktop`. Add `-- --skip-build` to `install:desktop` only when asked to reinstall the last package.
4. Let the bridge own staging, dev version allocation, shared signing, in-place update, launch and cleanup. Do not recreate these steps by hand, edit tracked versions for a dev build, or change the package identity. Never uninstall or change the publisher to work around an error, and report build errors without suppressing warnings.

Report: the resulting version and the runtime evidence available. A compile or install dry run does not show the app works; pressing the Copilot key and checking the action needs the maintainer. If an install was requested, verify the installed version and the running package where possible.
