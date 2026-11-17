#!/usr/bin/env python3
"""Frame the raw Store captures into the listing screenshots (LIST-8, ASSET-9).

Reads store-assets/manifest.json. For each Microsoft Store entry, the raw window capture from
store-assets/raw/ has its 1-pixel window border shaved (it shows the desktop through) and is centred at 1:1 (never scaled) on a
1366 x 768 background in the shot's theme,
with a soft shadow, and written flattened (no alpha) to store-assets/microsoft/screenshots/. Runs
ImageMagick in a disposable container, so the host needs only Docker.

  python3 scripts/render-screenshots.py           render every screenshot, then check them
  python3 scripts/render-screenshots.py --check   fail unless every file exists at 1366 x 768 or larger
"""
import json
import os
import struct
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "store-assets"
WIDTH, HEIGHT = 1366, 768
IMAGE = "alpine:3.20"
# Top and bottom of the background gradient for each theme.
BACKGROUNDS = {"light": ("#EEF3FB", "#D3E0F2"), "dark": ("#2A2F37", "#16191E")}


def png_size(path: Path) -> tuple[int, int]:
    """Width and height from a PNG header; raises if the file is not a PNG."""
    with path.open("rb") as handle:
        header = handle.read(24)
    if header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"{path} is not a PNG")
    return struct.unpack(">II", header[16:24])


def entries() -> list[dict]:
    """The Microsoft Store entries of the manifest, in listing order."""
    manifest = json.loads((ASSETS / "manifest.json").read_text(encoding="utf-8"))
    return manifest["microsoft"]


def render() -> None:
    """Frame every raw capture; the container writes as the calling user so ownership stays theirs."""
    commands = []
    for entry in entries():
        top, bottom = BACKGROUNDS[entry["theme"]]
        commands.append(
            f'magick -size {WIDTH}x{HEIGHT} gradient:"{top}"-"{bottom}" '
            f'\\( "{entry["source"]}" -shave 1x1 \\( +clone -background black -shadow 35x14+0+10 \\) +swap '
            f'-background none -layers merge +repage \\) -gravity center -composite '
            f'-alpha off -depth 8 -strip "microsoft/{entry["file"]}"'
        )
    owner = f"{os.getuid()}:{os.getgid()}"
    script = (
        "apk add -q --no-cache imagemagick >/dev/null && cd /assets && mkdir -p microsoft/screenshots && "
        + " && ".join(commands)
        + f" && chown -R {owner} microsoft/screenshots"
    )
    subprocess.run(
        ["docker", "run", "--rm", "--memory", "1g", "--memory-swap", "1g",
         "-v", f"{ASSETS}:/assets", IMAGE, "sh", "-c", script],
        check=True,
    )


def check() -> int:
    """Every manifest entry exists, is large enough, has a caption and alt text; nothing extra."""
    problems = []
    names = set()
    for entry in entries():
        path = ASSETS / "microsoft" / entry["file"]
        names.add(path.name)
        if not path.exists():
            problems.append(f"{entry['file']}: missing")
            continue
        width, height = png_size(path)
        if width < WIDTH or height < HEIGHT:
            problems.append(f"{entry['file']}: {width} x {height}, below {WIDTH} x {HEIGHT}")
        for field in ("caption", "alt"):
            if not entry.get(field):
                problems.append(f"{entry['file']}: no {field}")
    for path in (ASSETS / "microsoft" / "screenshots").glob("*.png"):
        if path.name not in names:
            problems.append(f"{path.name}: not in manifest.json")
    count = len(entries())
    if not 1 <= count <= 10:
        problems.append(f"{count} screenshots; the Store takes 1 to 10")
    for problem in problems:
        print("screenshots: " + problem, file=sys.stderr)
    print(f"screenshots: {'failed' if problems else 'passed'} ({count} files)")
    return 1 if problems else 0


if __name__ == "__main__":
    if "--check" in sys.argv[1:]:
        sys.exit(check())
    render()
    sys.exit(check())
