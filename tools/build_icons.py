#!/usr/bin/env python3
"""Rebuilds src/AshenReliquary.App/Assets/Icons from the game-icons.net repository (CC BY 3.0).

Usage: tools/build_icons.py <checkout of github.com/game-icons/icons>

Each SVG is rasterised by `AssetTool icon` (transparent, one ivory tint) and listed in icons.json with its author.
The game looks icons up by the ids below: abilities by ability id, conditions by condition name, and HUD extras.
"""
import json, os, subprocess, sys

ICONS = {
    # Abilities (abilities.json ids)
    "strike": "lorc/broadsword", "power_attack": "lorc/hammer-drop", "sudden_charge": "delapouite/charging-bull",
    "ignition": "lorc/fireball", "force_bolt": "delapouite/bolt-spell-cast", "staff_strike": "lorc/wizard-staff",
    # Feat abilities (M2)
    "intimidating_strike": "lorc/screaming", "brutish_shove": "delapouite/push", "knockdown": "lorc/stomp",
    "swipe": "lorc/sword-spin", "electric_arc": "lorc/lightning-arc", "ray_of_frost": "lorc/ice-bolt",
    # Conditions (ConditionKind names)
    "Frightened": "lorc/terror", "Sickened": "lorc/vomiting", "Clumsy": "lorc/tripwire", "Enfeebled": "sbed/weight-crush",
    "Stupefied": "delapouite/tired-eye", "Drained": "lorc/heart-drop", "Slowed": "lorc/snail",
    "Stunned": "delapouite/knocked-out-stars", "OffGuard": "skoll/shield-disabled", "Prone": "sbed/falling",
    "Grabbed": "lorc/grab", "Immobilized": "lorc/manacles", "Blinded": "delapouite/blindfold", "Dazzled": "lorc/sunbeams",
    # Persistent damage (DamageType names)
    "persistent-Fire": "carl-olsen/flame", "persistent-Bleed": "lorc/bleeding-wound", "persistent-Poison": "lorc/poison-bottle",
    "persistent": "lorc/dripping-blade",
    # HUD
    "healing": "lorc/heart-bottle", "tainted": "lorc/daemon-skull", "hero-point": "lorc/laurel-crown",
    "flourish": "lorc/hourglass", "potion": "lorc/heart-bottle", "step": "lorc/sprint",
    # Item glyphs (items.json "glyph"), for things Flare's icon sheet lacks
    "lantern": "delapouite/old-lantern",
}

AUTHORS = {"lorc": "Lorc", "delapouite": "Delapouite", "carl-olsen": "Carl Olsen", "sbed": "Sbed", "skoll": "Skoll"}

def main():
    repo = sys.argv[1]
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out = os.path.join(root, "src/AshenReliquary.App/Assets/Icons")
    tool = os.path.join(root, "tools/AssetTool/bin/Debug/net10.0/AssetTool.dll")
    os.makedirs(out, exist_ok=True)
    listing = []
    for key, source in ICONS.items():
        author, name = source.split("/")
        target = os.path.join(out, f"{key}.png")
        subprocess.run(["dotnet", tool, "icon", os.path.join(repo, source + ".svg"), target], check=True, stdout=subprocess.DEVNULL)
        if not os.path.exists(target):
            sys.exit(f"AssetTool did not write {target}")
        listing.append({"id": key, "file": f"{key}.png", "source": f"https://game-icons.net/1x1/{author}/{name}.html",
                        "author": AUTHORS[author], "licence": "CC BY 3.0"})
    with open(os.path.join(out, "icons.json"), "w") as f:
        json.dump(listing, f, indent=1)
    print(f"{len(listing)} icons")

if __name__ == "__main__":
    main()
