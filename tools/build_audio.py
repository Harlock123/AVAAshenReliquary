#!/usr/bin/env python3
"""Rebuilds src/AshenReliquary.App/Assets/Audio from the original sound and music packs, with ffmpeg.

Usage: tools/build_audio.py <packs dir> [group ...]
  <packs dir>/kenney/OGG/...                         Kenney, 50 RPG sound effects (CC0)
  <packs dir>/lrsf/Fantasy Sound Library/Wav/...     Little Robot Sound Factory, Fantasy Sound Effects Library (CC-BY 3.0)
  <packs dir>/rpgpack/RPG Sound Pack/...             artisticdude, RPG Sound Pack (CC0)
  <packs dir>/swords/...                             StarNinjas, 20 sword sound effects (CC0)
  <packs dir>/music/Ossuary *.mp3                    Kevin MacLeod, incompetech.com (CC BY 4.0)
  <packs dir>/zhelanov/Battle Theme *.mp3            Alexandr Zhelanov, Battle themes (CC-BY 4.0)
  <packs dir>/ambience/dungeon_ambient_1_0.ogg       JaggedStone, Loopable Dungeon Ambience (CC0)
  <packs dir>/flare/...                              Flare's soundfx (mods/fantasycore/soundfx, CC-BY-SA 3.0)
  <packs dir>/creatures/...                          rubberduck, 80 CC0 creature SFX (CC0)

Naming groups converts only those, merged into the existing audio.json (handy when only one pack is on disk).

Effects become mono 44.1 kHz Ogg Vorbis (mono so OpenAL can place them left or right); music becomes stereo
Ogg Vorbis at about 112 kbps. Each game sound id may have several variants (one is picked at random).
Writes audio.json: id -> files, plus every file's source, author and licence.
"""
import json, os, subprocess, sys

K, L, R, S, F, C = "kenney/OGG", "lrsf/Fantasy Sound Library/Wav", "rpgpack/RPG Sound Pack", "swords", "flare", "creatures"
SOURCES = {
    "kenney": ("Kenney, 50 RPG sound effects", "https://opengameart.org/content/50-rpg-sound-effects", "CC0"),
    "lrsf": ("Little Robot Sound Factory, Fantasy Sound Effects Library", "https://opengameart.org/content/fantasy-sound-effects-library", "CC-BY 3.0"),
    "rpgpack": ("artisticdude, RPG Sound Pack", "https://opengameart.org/content/rpg-sound-pack", "CC0"),
    "swords": ("StarNinjas, 20 sword sound effects", "https://opengameart.org/content/20-sword-sound-effects-attacks-and-clashes", "CC0"),
    "music": ("Kevin MacLeod, incompetech.com", "https://incompetech.com/music/royalty-free/music.html", "CC BY 4.0"),
    "zhelanov": ("Alexandr Zhelanov, Battle themes", "https://opengameart.org/node/76534", "CC-BY 4.0"),
    "ambience": ("JaggedStone, Loopable Dungeon Ambience", "https://opengameart.org/content/loopable-dungeon-ambience", "CC0"),
    "flare": ("Flare: Empyrean Campaign, soundfx", "https://github.com/flareteam/flare-game", "CC-BY-SA 3.0"),
    "creatures": ("rubberduck, 80 CC0 creature SFX", "https://opengameart.org/content/80-cc0-creature-sfx", "CC0"),
}

def r(pattern, numbers): return [pattern.format(n) for n in numbers]

EFFECTS = {
    "swing": [f"{R}/battle/swing.wav", f"{R}/battle/swing2.wav", f"{R}/battle/swing3.wav"],
    "hit": [f"{K}/knifeSlice.ogg", f"{K}/knifeSlice2.ogg", f"{K}/chop.ogg"],
    "crit": r(S + "/sword - StarNinjas/sword.{}.ogg", [1, 2, 3, 4]),
    "hero-hurt": r(S + "/sword_clash.{}.ogg", [1, 2, 3, 4, 5]),
    "zombie": r(R + "/NPC/gutteral beast/mnstr{}.wav", [1, 2, 3, 4, 5, 6]),
    "zombie-death": r(R + "/NPC/gutteral beast/mnstr{}.wav", [10, 11, 12]),
    "skeleton": r(R + "/NPC/shade/shade{}.wav", [1, 2, 3, 4, 5]),
    "skeleton-death": r(R + "/NPC/shade/shade{}.wav", [10, 11, 12]),
    "grave-rise": r(R + "/NPC/slime/slime{}.wav", [1, 2, 3]),
    "bow": [f"{K}/drawKnife1.ogg", f"{K}/drawKnife2.ogg"],
    "witch-cast": [f"{L}/Spell_02.wav"],
    "ignition": [f"{L}/Spell_00.wav", f"{L}/Spell_01.wav"],
    "force-bolt": [f"{R}/battle/magic1.wav", f"{R}/battle/spell.wav"],
    "impact": [f"{L}/Spell_03.wav", f"{L}/Spell_04.wav"],
    "gold": r(L + "/Pickup_Gold_0{}.wav", [0, 1, 2, 3, 4]),
    "loot-drop": [f"{K}/dropLeather.ogg", f"{K}/metalPot1.ogg"],
    "pickup-cloth": [f"{R}/inventory/cloth.wav", f"{R}/inventory/cloth-heavy.wav"],
    "pickup-metal": [f"{R}/inventory/metal-small1.wav", f"{R}/inventory/metal-small2.wav", f"{R}/inventory/chainmail1.wav"],
    "pickup-jewel": [f"{R}/inventory/beads.wav"],
    "potion": [f"{R}/inventory/bubble.wav", f"{R}/inventory/bubble2.wav"],
    "level-up": [f"{L}/Jingle_Achievement_00.wav"],
    "shrine": [f"{L}/Jingle_Win_00.wav"],
    "death": [f"{L}/Jingle_Lose_00.wav"],
    "zone-complete": [f"{L}/Jingle_Win_00.wav"],
    "footstep": r(L + "/Footsteps/Footstep_Dirt_0{}.wav", [0, 1, 2, 3, 4, 5]),
    "inventory": [f"{L}/Inventory_Open_00.wav", f"{L}/Inventory_Open_01.wav"],
    "click": [f"{R}/interface/interface1.wav", f"{R}/interface/interface2.wav"],
    # Flare's own sounds: chests, shields, bombs and the voices of crawlers, cultists and Mother Sallow.
    "chest": [f"{F}/wood_open.ogg"],
    "ward-shield": [f"{F}/inventory/inventory_heavy.ogg"],
    "ward-spell": [f"{F}/powers/shield.ogg"],
    "block": [f"{F}/powers/block.ogg"],
    "bomb-fire": [f"{F}/powers/fireball.ogg"],
    "bomb-cold": [f"{F}/powers/freeze.ogg"],
    "bomb-electricity": [f"{F}/powers/shock.ogg"],
    "bomb-acid": [f"{F}/powers/burn.ogg"],
    "crawler": [f"{F}/enemies/antlion_ment.ogg", f"{F}/enemies/antlion_phys.ogg"],
    "crawler-death": [f"{F}/enemies/antlion_die.ogg", f"{F}/enemies/antlion_critdie.ogg"],
    "cultist": [f"{F}/male_hit.ogg"],
    "cultist-death": [f"{F}/male_die.ogg"],
    "crone": [f"{F}/female_hit.ogg"],
    "crone-death": [f"{F}/female_die.ogg"],
    # M3: a Unique or Set piece dropping; the Beastmaster's wolf barking and falling.
    "rare-drop": [f"{F}/powers/heal.ogg"],
    "ready": [f"{F}/inventory/inventory_gem.ogg"],
    "wolf": [f"{C}/barking_01.ogg", f"{C}/barking_02.ogg"],
    "wolf-death": [f"{C}/howl.ogg"],
}

MUSIC = {
    "title": ["music/Ossuary 1 - A Beginning.mp3"],
    "explore": ["music/Ossuary 5 - Rest.mp3", "music/Ossuary 6 - Air.mp3"],
    "combat": ["zhelanov/Battle Theme 1.mp3", "zhelanov/Battle Theme 2.mp3"],
    "boss": ["zhelanov/Battle Theme 2.mp3"],
}

AMBIENCE = {"road": ["ambience/dungeon_ambient_1_0.ogg"]}

def slug(path): return os.path.splitext(os.path.basename(path))[0].lower().replace(" ", "-").replace(".", "-")

def main():
    packs, only = sys.argv[1], set(sys.argv[2:])
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out = os.path.join(root, "src/AshenReliquary.App/Assets/Audio")
    listing = {"effects": {}, "music": {}, "ambience": {}, "files": []}
    if only:
        with open(os.path.join(out, "audio.json")) as f:
            listing = json.load(f)
    elif os.path.exists(os.path.join(out, "audio.json")):
        # Keep the sounds synthesised by tools/build_gun_sounds.py: they come from no pack.
        with open(os.path.join(out, "audio.json")) as f:
            old = json.load(f)
        made = {e["file"] for e in old["files"] if e["source"].startswith("Ashen Reliquary, synthesised")}
        listing["files"] = [e for e in old["files"] if e["file"] in made]
        listing["effects"] = {k: v for k, v in old["effects"].items() if v and all(f in made for f in v)}
    def convert(kind, group, items, args):
        os.makedirs(os.path.join(out, kind), exist_ok=True)
        files = []
        for source in items:
            name = f"{kind}/{group}-{slug(source)}.ogg"
            listing["files"] = [entry for entry in listing["files"] if entry["file"] != name]
            target = os.path.join(out, name)
            subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", os.path.join(packs, source), *args, target], check=True)
            files.append(name)
            pack = source.split("/")[0]
            title, url, licence = SOURCES[pack]
            listing["files"].append({"file": name, "source": title, "url": url, "original": os.path.basename(source), "licence": licence})
        return files
    def wanted(group): return not only or group in only
    for group, items in EFFECTS.items():
        if wanted(group):
            listing["effects"][group] = convert("sfx", group, items, ["-ac", "1", "-ar", "44100", "-c:a", "libvorbis", "-q:a", "4"])
    for group, items in MUSIC.items():
        if wanted(group):
            listing["music"][group] = convert("music", group, items, ["-ac", "2", "-ar", "44100", "-c:a", "libvorbis", "-q:a", "3"])
    for group, items in AMBIENCE.items():
        if wanted(group):
            listing["ambience"][group] = convert("ambience", group, items, ["-ac", "2", "-ar", "44100", "-c:a", "libvorbis", "-q:a", "2"])
    with open(os.path.join(out, "audio.json"), "w") as f:
        json.dump(listing, f, indent=1)
    print(f"{len(listing['files'])} files: {len(EFFECTS)} effects, {len(MUSIC)} music groups, {len(AMBIENCE)} ambience")

if __name__ == "__main__":
    main()
