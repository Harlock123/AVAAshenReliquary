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
    # The arsenal: the fighter's Cleave, Raise a Shield and Finishing Blow, the wizard's Shield cantrip
    "cleave": "lorc/axe-swing", "raise_shield": "sbed/shield", "finishing_blow": "lorc/decapitation", "shield": "lorc/magic-shield",
    # Subclass abilities (M3): Vanguard, Bulwark, Pyromancer, Stormcaller
    "earthshaker": "lorc/earth-crack", "challenge": "lorc/shouting", "burning_hands": "lorc/fire-breath",
    "thunderclap": "lorc/thunder-struck",
    # The Rogue (M3): class abilities, the Cutthroat's and Trickster's, class feats
    "rogue_strike": "lorc/stiletto", "sneak_strike": "lorc/backstab", "tumble": "lorc/dodging", "hide": "lorc/cowled",
    "eviscerate": "lorc/bloody-sword", "gutting_strike": "lorc/dripping-knife", "snare": "lorc/wolf-trap",
    "twin_feint": "lorc/daggers", "whirling_blades": "lorc/spinning-blades",
    # The Ranger (M3): class abilities, the Marksman's and the Beastmaster's
    "ranger_strike": "lorc/arrow-flights", "twin_shot": "lorc/double-shot", "hunt_prey": "lorc/archery-target",
    "disengage": "delapouite/jump-across", "aimed_shot": "lorc/supersonic-arrow", "piercing_shot": "lorc/arrow-cluster",
    "sic": "lorc/wolf-howl",
    # The Champion (M3): class abilities, the Lanternbearer's and the Oathbreaker's auras
    "champion_strike": "delapouite/sword-brandish", "smite": "lorc/shining-sword", "lay_on_hands": "delapouite/healing",
    "shield_of_faith": "lorc/checked-shield", "lanterns_radiance": "lorc/lantern-flame", "aura_of_dread": "lorc/death-zone",
    # The Necromancer (M3): class abilities, the Bonelord's and the Plaguebringer's
    "grave_bolt": "lorc/skull-bolt", "raise_bones": "skoll/raise-skeleton", "siphon_life": "lorc/life-tap",
    "bone_rush": "delapouite/skeletal-hand", "bone_armor": "lorc/ribcage", "corpse_explosion": "lorc/skull-crack",
    # Levels 6-10 (M4): each class's level-6 ability and each subclass's level-8 signature
    "intimidating_roar": "lorc/lion", "whirlwind_strike": "lorc/whirlwind", "shield_slam": "delapouite/shield-bash",
    "retributive_strike": "delapouite/shield-impact", "beacon_of_hope": "lorc/sunbeams", "dread_harvest": "lorc/reaper-scythe",
    "shadowstep": "lorc/shadow-follower", "fan_of_knives": "lorc/thrown-daggers", "smoke_bomb": "darkzaitzev/smoke-bomb",
    "rain_of_arrows": "lorc/arrows-shield", "deadeye": "lorc/arrow-scope", "call_of_the_pack": "lorc/direwolf",
    "blink": "lorc/teleport", "fireball": "lorc/burning-meteor", "chain_lightning": "willdabeast/chain-lightning",
    "bone_spear": "lorc/spears", "raise_bone_knight": "delapouite/black-knight-helm", "plague_cloud": "sbed/poison-cloud",
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
    "flourish": "lorc/hourglass", "potion": "lorc/heart-bottle", "step": "lorc/sprint", "antidote": "lorc/drink-me",
    "hidden": "lorc/hood", "companion": "lorc/wolf-head",
    # Item glyphs (items.json "glyph"), for things Flare's icon sheet lacks
    "lantern": "delapouite/old-lantern", "ember": "lorc/burning-embers",
    "oath_sword": "lorc/sword-in-stone", "cog": "lorc/cog", "spring": "delapouite/spring", "grille": "delapouite/robot-helmet",
    # Levels 11-15 (M4 step 5): a class ability at 11, a subclass signature at 13.
    "sundering_blow": "lorc/sword-break", "divine_wrath": "lorc/sun-radiations", "crippling_strike": "delapouite/achilles-heel",
    "pinning_shot": "lorc/arrowed", "cone_of_cold": "lorc/frozen-orb", "grasp_of_the_grave": "lorc/grasping-claws",
    "earthsplitter": "lorc/ground-sprout", "shield_rush": "lorc/shield-reflect", "dawnbreak": "lorc/sunrise",
    "ruinous_blow": "lorc/broken-skull", "assassinate": "delapouite/dagger-rose", "caltrops": "delapouite/caltrops",
    "killshot": "lorc/target-arrows", "hunt_as_one": "lorc/hound", "meteor": "lorc/meteor-impact", "ball_lightning": "lorc/ball-glow",
    "bone_colossus": "skoll/skeleton", "blight": "lorc/poison-gas",
    "pauldrons": "skoll/pauldrons", "cloak": "delapouite/cape",
}

AUTHORS = {"lorc": "Lorc", "delapouite": "Delapouite", "carl-olsen": "Carl Olsen", "sbed": "Sbed", "skoll": "Skoll",
           "darkzaitzev": "DarkZaitzev", "willdabeast": "Willdabeast"}

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
