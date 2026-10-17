#!/usr/bin/env python3
"""Regenerates CREDITS.md from assets.manifest.json (the single record of every third-party asset)."""
import json, os

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
manifest = json.load(open(os.path.join(root, "assets.manifest.json")))
lines = ["# Credits", "", "Generated from `assets.manifest.json` by `tools/build_credits.py`. Every third-party asset the game ships is listed here.", ""]
for s in manifest["sources"]:
    commit = f" at commit `{s['commit'][:12]}`" if "commit" in s else ""
    lines += [f"## {s['title']}", "",
              f"- Source: [{s['url']}]({s['url']}){commit} (retrieved {s['retrieved']})",
              f"- Licence: {s['licence']} (`{s['licenceFile']}`)",
              f"- Authors: {s['authors']}",
              f"- Changes: {s['changes']}",
              f"- Files: {len(s['files'])} (listed in `assets.manifest.json`)"]
    if "credit" in s:
        lines.append(f"- Credit line: {s['credit']}")
    lines.append("")
lines += ["## Code", "", "The game's code is MIT licensed (`LICENSE`). Game mechanics follow Pathfinder 2e Remaster rules published under the ORC licence; the Pathfinder name, setting and trademarks are not used.", ""]
open(os.path.join(root, "CREDITS.md"), "w").write("\n".join(lines))
print(f"{len(manifest['sources'])} sources")
