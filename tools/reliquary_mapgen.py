# Generates zones/reliquary.json: Act 4's dungeon (levels 19-20), the Reliquary under Mount Cindral, the sealed vault
# where the Order of the Pale Lantern kept Ashur's bones until Malachar Vane broke the seals. Deterministic.
#
# The way goes down (south, high y) from the Ember Gate: the Narthex (a shrine), the Hall of Vigil (the Prophet's
# Inquisitors hold it, unless the beacon at the Ember Gate was lit: then the Order's knights took it first, and one waits
# there), the Muster (two packs led by elites; if the beacon was kept dark, the old stealth way, a passage round it,
# opens), then the gauntlet of Herald Echoes (the spec's Echo rule): the Vault of Rot (Mother Sallow's Echo, unless her
# relic was destroyed), the Vault of Iron (Ser Hadric's, unless his sword went back to his wife's grave), the Vault of
# Whispers (the Choir's, gone if the candles were set right, alone if they were snuffed) and the Vault of Flame
# (Vorthrax's, unless his egg was smashed; M4 step 9), each behind a ward that
# burns out when the Echo before it is laid to rest; and the Sanctum, the Reliquary's heart, where the way to the Cinder
# Throne opens (the Epilogue, M5). Every pack is led by an elite (Act 4's pack mix).
#
# Ruined walls (the Old Ruins at +1000) on flagstones, in the dark; braziers in the halls.
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import sys
from cinder_common import Rng, legend, open_cell, pack, write, LEADS

REVISION = 2
W, H = 56, 184
rnd = Rng(19209)
grid = [[' '] * W for _ in range(H)]

def room(x0, y0, x1, y1):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            grid[y][x] = ',' if rnd() < 0.06 else '='

def corridor(x, y0, y1, width=3):
    for y in range(y0, y1 + 1):
        for dx in range(-(width // 2), width // 2 + 1):
            if grid[y][x + dx] == ' ':
                grid[y][x + dx] = '='

cx = W // 2
# Rooms, top to bottom.
narthex = (cx - 6, 4, cx + 6, 14)
vigil = (cx - 11, 20, cx + 11, 34)
muster = (cx - 12, 40, cx + 12, 56)
rot = (cx - 12, 64, cx + 12, 84)
iron = (cx - 12, 90, cx + 12, 110)
whispers = (cx - 12, 116, cx + 12, 136)
flame = (cx - 12, 142, cx + 12, 162)
sanctum = (cx - 10, 168, cx + 10, 180)
for r in (narthex, vigil, muster, rot, iron, whispers, flame, sanctum):
    room(*r)
corridor(cx, 1, 4)
corridor(cx, 14, 20)
corridor(cx, 34, 40)
corridor(cx, 56, 64)
corridor(cx, 84, 90)
corridor(cx, 110, 116)
corridor(cx, 136, 142)
corridor(cx, 162, 168)
# The stealth way round the Muster: from the Hall of Vigil's west end down to the Vault of Rot's door, behind a ward
# that opens only if the beacon was kept dark.
sx = cx - 17
for y in range(26, 61):
    for x in (sx - 1, sx, sx + 1):
        grid[y][x] = '='
for x in range(sx - 1, vigil[0] + 1):
    for y in (26, 27, 28):
        grid[y][x] = '='
for x in range(sx - 1, cx - 1):
    for y in (59, 60, 61):
        grid[y][x] = '='
stealth_ward = [[vigil[0] - 1, y] for y in (26, 27, 28)]

# Walls: everything next to the floor.
FLOOR = '=,'
for y in range(H):
    for x in range(W):
        if grid[y][x] != ' ':
            continue
        if any(0 <= x + dx < W and 0 <= y + dy < H and grid[y + dy][x + dx] in FLOOR
               for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
            grid[y][x] = 'W'
# Pillars in the halls and vaults; braziers.
for (x0, y0, x1, y1) in (vigil, muster, rot, iron, whispers, flame, sanctum):
    for px in (x0 + 3, x1 - 3):
        for py in range(y0 + 3, y1 - 1, 5):
            grid[py][px] = 'R'
    for (bx, by) in [(x0 + 1, y0 + 1), (x1 - 1, y0 + 1), (x0 + 1, y1 - 1), (x1 - 1, y1 - 1)]:
        grid[by][bx] = 'B'
for (bx, by) in [(narthex[0] + 1, narthex[1] + 1), (narthex[2] - 1, narthex[1] + 1)]:
    grid[by][bx] = 'B'
for y in range(30, 58, 6):
    grid[y][sx + 1] = 'B'
shrine1 = [cx - 3, 10]
shrine2 = [cx - 6, 177]
grid[shrine1[1]][shrine1[0]] = 'S'
grid[shrine2[1]][shrine2[0]] = 'S'
# The Sanctum: the empty biers where Ashur's bones lay.
for (x, y) in [(cx - 3, 173), (cx, 173), (cx + 3, 173)]:
    grid[y][x] = 'b'

rows = [''.join(r) for r in grid]
def cache(id, x, y, kind="Cache", facing=0):
    return {"id": id, "at": open_cell(rows, x, y, "=,"), "kind": kind, "facing": facing}
caches = [
    cache("narthex", narthex[2] - 2, narthex[3] - 2),
    cache("hall_of_vigil", vigil[0] + 2, vigil[3] - 2, facing=1),
    cache("stealth_way", sx, 44),
    cache("vault_of_rot", rot[2] - 2, rot[3] - 2, facing=1),
    cache("vault_of_iron", iron[0] + 2, iron[3] - 2),
    cache("vault_of_whispers", whispers[2] - 2, whispers[3] - 2, facing=1),
    cache("vault_of_flame", flame[0] + 2, flame[3] - 2),
    cache("sanctum", cx, 177, kind="Reliquary"),
]

def ward(y):
    return [[cx + dx, y] for dx in (-1, 0, 1)]
N, C, E, B = "Normal", "Champion", "Elite", "Herald"
HUSK, WRAITH, ANT, LION, INQ, ZEAL, CAPT, DRAKE = ("ash_husk", "cinder_wraith", "fire_ant", "tainted_antlion", "inquisitor",
                                                   "inquisitor_zealot", "inquisitor_captain", "cinder_drake")
ECHO = {"echo": True}
spawns = [
    # The Hall of Vigil: the Inquisitors' watch, unless the Order's knights took the hall first (the beacon lit).
    pack((CAPT, E, 1, ["shielded"], LEADS), (INQ, N, 2), (ZEAL, N, 2), at=[cx, 27], radius=3, level=19,
         unless={"act4.beacon": "lit"}),
    pack((WRAITH, E, 1, ["mirrored"], LEADS), (WRAITH, N, 2), at=[cx + 7, 23], level=19, unless={"act4.beacon": "lit"}),
    # The Muster
    pack((CAPT, E, 1, ["vengeful", "hasted"], LEADS), (INQ, N, 3), (ZEAL, N, 1), at=[cx - 5, 46], radius=3, level=19),
    pack((LION, E, 1, ["tainted"], LEADS), (LION, N, 1), (ANT, N, 3), at=[cx + 6, 51], radius=3, level=19),
    pack((DRAKE, E, 1, ["shielded"], LEADS), (DRAKE, N, 2), at=[cx, 54], level=19),
    # The gauntlet of Echoes: each comes unless its relic was dealt with; its fall (or its absence) opens the next ward.
    pack(("mother_sallow", B, 1, None, ECHO), at=[cx, 74], radius=0, level=19,
         unless={"act1.sallow_relic": "destroyed"}, flag="act4.echo.sallow", value="laid",
         says="The Echo of Mother Sallow sinks into the floor, and the ward across the vault's south door gutters out."),
    pack(("ser_hadric", B, 1, None, ECHO), at=[cx, 100], radius=0, level=19,
         unless={"act2.hadric_relic": "returned"}, flag="act4.echo.hadric", value="laid",
         says="Ser Hadric's Echo goes down on one knee and is ash. The ward beyond him burns out."),
    pack(("choir_agnete", B, 1, None, ECHO), at=[cx, 126], radius=0, level=19,
         requires={"act3.candles": ""}, flag="act4.echo.choir", value="laid",
         says="The Choir's Echo falls silent, and the last ward goes out."),
    pack(("choir_agnete", B, 1, None, {"echo": True, "alone": True}), at=[cx, 126], radius=0, level=19,
         requires={"act3.candles": "snuffed"}, flag="act4.echo.choir", value="laid",
         says="Agnete's Echo falls silent, alone, and the last ward goes out."),
    # The Sanctum
    pack(("vorthrax", B, 1, None, ECHO), at=[cx, 152], radius=0, level=19,
         unless={"act4.egg": "smashed"}, flag="act4.echo.vorthrax", value="laid",
         says="Vorthrax's Echo folds its wings and burns down to nothing, and the last ward before the Sanctum goes out."),
    pack((CAPT, E, 1, ["mirrored", "shielded"], LEADS), (ZEAL, N, 2), (INQ, N, 2), at=[cx, 176], radius=3, level=20),
    pack((WRAITH, E, 1, ["tainted"], LEADS), (HUSK, N, 3), at=[cx - 6, 171], level=20),
]
gates = [
    {"id": "stealth_way", "name": "The old stealth way's ward", "cells": stealth_ward, "requires": {"act4.beacon": "dark"}},
    {"id": "vault_of_rot", "name": "The ward of the Vault of Rot", "cells": ward(86), "requires": {"act4.echo.sallow": "laid"}},
    {"id": "vault_of_iron", "name": "The ward of the Vault of Iron", "cells": ward(112), "requires": {"act4.echo.hadric": "laid"}},
    {"id": "vault_of_whispers", "name": "The ward of the Vault of Whispers", "cells": ward(138), "requires": {"act4.echo.choir": "laid"}},
    {"id": "vault_of_flame", "name": "The ward of the Vault of Flame", "cells": ward(164), "requires": {"act4.echo.vorthrax": "laid"}},
]
start = [cx, 6]
zone = {
    "id": "reliquary", "name": "The Reliquary", "level": 19, "act": 4, "revision": REVISION, "xpScale": 0.8,
    "tileset": "tileset_cinder",
    "tagline": "Three hundred years the Order kept him here. Then a High Lector broke the seals.",
    "darkness": 0.92, "heroLight": 6.0,
    "rows": rows,
    "legend": legend({"b": {"ground": "ash", "tiles": [1024, 1025], "objects": [1291, 1292], "blocks": True}}),
    "playerStart": start,
    "spawns": spawns,
    "gates": gates,
    "shrines": [
        {"id": "reliquary_narthex_shrine", "name": "The Narthex", "at": shrine1},
        {"id": "sanctum_shrine", "name": "The Sanctum's Door", "at": shrine2},
    ],
    "exits": [
        {"id": "gate", "label": "The Ember Gate", "at": [cx, 2], "radius": 1.5, "to": "wyrms_nest", "toExit": "reliquary",
         "arrive": start},
        {"id": "throne", "label": "The Cinder Throne", "at": [cx, 179], "radius": 1.5, "to": "cinder_throne", "toExit": "stair",
         "arrive": [cx, 176]},
    ],
    "caches": caches,
    "npcs": [
        {"id": "aldren", "name": "Ser Aldren", "title": "Knight of the Last Lantern", "at": [cx - 4, 30], "facing": 3,
         "portrait": "flare/male03", "dialogue": "reliquary_aldren", "requires": {"act4.beacon": "lit"},
         "look": {"head": "plate_helm", "chest": "plate_cuirass", "hands": "plate_gauntlets", "legs": "plate_greaves", "feet": "plate_boots",
                  "main": "longsword", "off": "kite_shield"}},
    ],
    "interactables": [
        {"id": "biers", "name": "The empty biers", "at": [cx, 172], "dialogue": "reliquary_biers"},
        # Malachar's Daughter (M5 step 3): his journal, left open on a bier.
        {"id": "daughter_journal", "name": "Malachar's journal", "at": open_cell(rows, cx + 6, 172), "dialogue": "daughter_journal", "action": "keepsake", "blocks": False},
    ],
}
write(zone, sys.argv[1])
