#!/usr/bin/env python3
"""Rebuilds src/AshenReliquary.App/Assets/Portraits from the original portrait packs.

Usage: tools/build_portraits.py <flare portraits dir> <GearHead FPS1 dir> <Adonthell portraits dir> <PolygonDan dir> [id ...]

  Flare:      github.com/flareteam/flare-game, mods/fantasycore/images/portraits   (CC-BY-SA 3.0)
  GearHead:   gearhead.itch.io/fantasy-portait-set-1, FantasyPortraitSet1.zip/FPS1  (CC BY 4.0)
  Adonthell:  opengameart.org/content/adonthell-portraits, adonthell-portraits.zip   (CC-BY-SA 3.0)
  PolygonDan: opengameart.org/content/halforc-character-illustration, HalfOrc__0.jpg (CC-BY-SA 4.0)

Naming portrait ids ("polygondan/halforc__0") converts only those, merged into the existing portraits.json (handy when
only some packs are on disk).

Each portrait is cropped, graded and vignetted by `AssetTool portrait`; the options per portrait are below.
Writes portraits.json (id, ancestry, file, artist, licence) for the game.
"""
import json, os, subprocess, sys

FLARE_ARTIST = "Flare art team (portraits by Justin Nichol and others)"
GEARHEAD_ARTIST = "Joseph Hewitt (GearHead)"
ADONTHELL_ARTIST = "Adonthell project artists (via Grumbel, OpenGameArt)"
POLYGONDAN_ARTIST = "PolygonDan (OpenGameArt)"

def flare(name, *opts): return ("flare", name, opts)
def gear(name, *opts): return ("gearhead", name, opts)
def adon(name, *opts): return ("adonthell", name, ("--auto",) + opts)
def polygon(name, *opts): return ("polygondan", name, opts)

SETS = {
    "human": [flare(f"male{n:02d}") for n in [1, 2, 3, 4, 5, 6, 7, 9, 10, 11, 13, 14, 15, 16, 17, 18, 19, 20]]
           + [gear(n) for n in ["Human_002", "Human_006", "Human_005", "Knight", "Human_007", "Wizard_001", "Human_001",
                                "Human_003", "Human_008", "Adventurer", "Kathy", "Human_004", "Hood_002", "NatureWitch", "Armor"]]
           + [flare(f"female{n:02d}") for n in [1, 3, 5, 6, 7, 9, 11, 12, 13, 14, 15]],
    # Elf_001's head is taller than its canvas is wide: zoom out so the hair and chin both fit.
    "elf": [gear("Elf_001", "--top", "0", "--zoom", "1.25"), gear("Elf_004"), gear("Elf_002"), gear("Dwarf_002"), adon("Kidalethil")]
         + [flare(f"female{n:02d}") for n in [2, 4, 8]],
    "dwarf": [gear("Dwarf_003"), gear("Dwarf_001"), gear("Dwarf_004"), adon("Firun Bonesmiter"), adon("Helmerk Axehaft"),
              adon("Thorig Axehaft"), adon("Ynar Hammerhand"), adon("Jelom Rasgar"), adon("dwarf_guard_1")],
    # A greyscale full-length sketch on white paper: the head is a small square at the top centre, the paper toned to
    # parchment so it sits beside the paintings.
    "orc": [gear("Orc_001"), gear("Orc_002"), polygon("HalfOrc__0.jpg", "--zoom", "0.3", "--top", "0.04", "--parchment")],
    "ashborn": [gear("Hood_001"), gear("Elf_003"), adon("Agdan"), adon("Krelm")],
    # No free set has true halflings yet: round-faced, youthful portraits stand in.
    "halfling": [adon("Oliver"), adon("Mina Turvale"), adon("Elgar Basil"), flare("female10")],
}

def main():
    flare_dir, gear_dir, adon_dir, polygon_dir = sys.argv[1:5]
    only = set(sys.argv[5:])
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out = os.path.join(root, "src/AshenReliquary.App/Assets/Portraits")
    tool = os.path.join(root, "tools/AssetTool/bin/Debug/net10.0/AssetTool.dll")
    sources = {"flare": (flare_dir, FLARE_ARTIST, "CC-BY-SA 3.0"), "gearhead": (gear_dir, GEARHEAD_ARTIST, "CC BY 4.0"),
               "adonthell": (adon_dir, ADONTHELL_ARTIST, "CC-BY-SA 3.0"), "polygondan": (polygon_dir, POLYGONDAN_ARTIST, "CC-BY-SA 4.0")}
    listing = os.path.join(out, "portraits.json")
    kept = {p["id"]: p for p in json.load(open(listing))} if only else {}
    portraits = []
    for ancestry, items in SETS.items():
        for source, name, opts in items:
            folder, artist, licence = sources[source]
            stem, extension = os.path.splitext(name)
            if extension.lower() not in (".png", ".jpg"):
                stem, extension = name, ".png"
            pid = f"{source}/{stem.lower().replace(' ', '_')}"
            if only and pid not in only:
                portraits.append(kept[pid])
                continue
            os.makedirs(os.path.join(out, source), exist_ok=True)
            subprocess.run(["dotnet", tool, "portrait", os.path.join(folder, stem + extension), os.path.join(out, pid + ".png"), *opts],
                           check=True, stdout=subprocess.DEVNULL)
            portraits.append({"id": pid, "ancestry": ancestry, "file": pid + ".png", "artist": artist, "licence": licence})
    with open(os.path.join(out, "portraits.json"), "w") as f:
        json.dump(portraits, f, indent=1)
    print(f"{len(portraits)} portraits: " + ", ".join(f"{a} {sum(p['ancestry'] == a for p in portraits)}" for a in SETS))

if __name__ == "__main__":
    main()
