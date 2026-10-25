# Generates zones/cinder_throne.json: the Epilogue's zone (level 20, M5 step 1), the Cinder Throne under the Reliquary,
# where Malachar Vane is building Ashur a body from the god's own bones and waiting for the Ember Heart. Deterministic.
#
# The stair from the Reliquary's Sanctum comes in at the south: a landing (a shrine), the Prophet's honour guard in the
# hall of black glass, and the throne cavern, a floor of glass on lava with the Throne on its dais to the north and the
# god's bones heaped behind it. Malachar holds court there: his Inquisitors, and the Echo of every Herald whose relic
# was left undealt with (the spec's Echo rule: faint now, at 40% and their first phase only), standing as figures of ash
# until Malachar calls them into the fight one at a time. If the beacon at the Ember
# Gate was lit, the Order's knights come in behind the hero as the fight begins; if it was kept dark, the Order's hidden
# way (a ward that knows them) runs round the honour guard to the cavern's west side.
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import math, sys
from cinder_common import Rng, legend, open_cell, pack, write, LEADS

REVISION = 1
W, H = 52, 64
rnd = Rng(20250)
grid = [[' '] * W for _ in range(H)]
cx = W // 2

def floor(x, y, ch='='):
    if 1 <= x < W - 1 and 1 <= y < H - 1:
        grid[y][x] = ch

# The landing and the stair up from the Reliquary.
for y in range(52, H - 1):
    for x in range(cx - 4, cx + 5):
        floor(x, y)
# The hall of black glass, north of the landing.
for y in range(36, 52):
    for x in range(cx - 7, cx + 8):
        floor(x, y, ',' if rnd() < 0.08 else '=')
# The throne cavern: a rough ellipse of glass over lava.
throne = (cx, 17)
for y in range(3, 34):
    for x in range(2, W - 2):
        r = ((x - throne[0]) / 22.0) ** 2 + ((y - throne[1]) / 14.0) ** 2
        if r <= 1 + 0.08 * (rnd() - 0.5):
            floor(x, y, ',' if rnd() < 0.1 else '=')
# Lava in channels across the cavern floor, crossed in three places.
for x in range(4, W - 4):
    for y0 in (26,):
        y = y0 + int(round(1.5 * math.sin(x / 3.5)))
        for dy in (0, 1):
            if grid[y + dy][x] in '=,':
                grid[y + dy][x] = '~'
for bx in (cx - 12, cx, cx + 12):
    for y in range(22, 31):
        for dx in (-1, 0, 1):
            if grid[y][bx + dx] == '~':
                grid[y][bx + dx] = '#'
# Lava pools at the cavern's sides, glowing.
for (px, py) in [(9, 15), (W - 10, 15), (12, 8), (W - 13, 8)]:
    for y in range(py - 2, py + 3):
        for x in range(px - 3, px + 4):
            if ((x - px) / 3.2) ** 2 + ((y - py) / 2.2) ** 2 <= 1 and grid[y][x] in '=,':
                grid[y][x] = '~'
    grid[py][px] = '!'
# The way up from the hall into the cavern.
for y in range(31, 37):
    for x in range(cx - 2, cx + 3):
        floor(x, y)
# The dais: pillars, statues of the Order's lectors defaced, braziers; the Throne itself and the god's bones behind it.
for (dx, dy) in [(-5, -2), (5, -2), (-5, 3), (5, 3)]:
    grid[throne[1] + dy][throne[0] + dx] = 'R'
for (dx, dy) in [(-9, -4), (9, -4), (-9, 5), (9, 5)]:
    grid[throne[1] + dy][throne[0] + dx] = 'Z'
for (dx, dy) in [(-3, 6), (3, 6), (-7, -6), (7, -6)]:
    grid[throne[1] + dy][throne[0] + dx] = 'B'
throne_at = [throne[0], throne[1] - 4]
grid[throne_at[1]][throne_at[0]] = 'T'
for (dx, dy) in [(-2, -6), (-1, -7), (0, -7), (1, -7), (2, -6), (0, -8), (-3, -7), (3, -7)]:
    grid[throne[1] + dy][throne[0] + dx] = 'b'
# The Order's hidden way: from the landing's west side, up the west of the hall, into the cavern's south-west.
hx = cx - 12
for y in range(27, 56):
    for x in (hx - 1, hx, hx + 1):
        floor(x, y)
for x in range(hx - 1, cx - 4):
    for y in (54, 55):
        floor(x, y)
hidden_ward = [[hx - 1, 53], [hx, 53], [hx + 1, 53]]

# Walls round everything that is floor.
FLOOR = '=,~#!RZBTb'
for y in range(H):
    for x in range(W):
        if grid[y][x] != ' ':
            continue
        if any(0 <= x + dx < W and 0 <= y + dy < H and grid[y + dy][x + dx] in FLOOR for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
            grid[y][x] = 'W'
shrine = [cx - 3, 57]
grid[shrine[1]][shrine[0]] = 'S'
for (x, y) in [(cx - 6, 38), (cx + 6, 38), (cx - 6, 49), (cx + 6, 49)]:
    grid[y][x] = 'B'

rows = [''.join(r) for r in grid]
N, C, E, B, P = "Normal", "Champion", "Elite", "Herald", "Prophet"
INQ, ZEAL, CAPT = "inquisitor", "inquisitor_zealot", "inquisitor_captain"
# Faint in level too: an Echo at the Throne is an add, not a duel.
ECHO_LEVEL = 16
FAINT = {"echo": True, "echoHealth": 0.4, "echoPhases": 1, "dormant": True}
spawns = [
    # Malachar first: the HUD's bar follows the first boss awake.
    pack(("malachar", P, 1, None, LEADS), (INQ, N, 2), (ZEAL, N, 2), at=[throne[0], throne[1]], radius=2.5, level=20,
         unless={"act5.malachar": "spared"}),
    # The Echoes of the relics left undealt with, round the dais.
    pack(("mother_sallow", B, 1, None, FAINT), at=[throne[0] - 8, throne[1] + 1], radius=0, level=ECHO_LEVEL,
         unless={"act1.sallow_relic": "destroyed"}),
    pack(("ser_hadric", B, 1, None, FAINT), at=[throne[0] + 8, throne[1] + 1], radius=0, level=ECHO_LEVEL,
         unless={"act2.hadric_relic": "returned"}),
    pack(("choir_agnete", B, 1, None, FAINT), at=[throne[0] - 6, throne[1] - 5], radius=0, level=ECHO_LEVEL,
         requires={"act3.candles": ""}),
    pack(("choir_agnete", B, 1, None, {**FAINT, "alone": True}), at=[throne[0] - 6, throne[1] - 5], radius=0, level=ECHO_LEVEL,
         requires={"act3.candles": "snuffed"}),
    pack(("vorthrax", B, 1, None, FAINT), at=[throne[0] + 6, throne[1] - 5], radius=0, level=ECHO_LEVEL,
         unless={"act4.egg": "smashed"}),
    # The honour guard in the hall of black glass.
    pack((CAPT, E, 1, ["shielded"], LEADS), (INQ, N, 2), (ZEAL, N, 1), at=[cx, 44], radius=3, level=20),
]
gates = [
    {"id": "hidden_way", "name": "The Order's hidden ward", "cells": hidden_ward, "requires": {"act4.beacon": "dark"}},
]
start = [cx, 59]
zone = {
    "id": "cinder_throne", "name": "The Cinder Throne", "level": 20, "act": 4, "revision": REVISION, "xpScale": 0.8,
    "tileset": "tileset_cinder",
    "tagline": "He has been building a god out of bones, and he has kept a place for your heart.",
    "darkness": 0.88, "heroLight": 6.0,
    "rows": rows,
    "legend": legend({
        "!": {"ground": "lava", "light": 3.5, "lightColor": "#ff8a3a"},
        "Z": {"ground": "ash", "tiles": [1024, 1025], "objects": [1241, 1242, 1243, 1244], "blocks": True},
        "T": {"ground": "ash", "tiles": [1024], "objects": [1294], "blocks": True, "light": 4.0, "lightColor": "#ffb070"},
        "b": {"ground": "ash", "tiles": [52, 53, 54, 55], "objects": [1187, 1189, 1190, 1192], "blocks": True, "light": 2.0,
              "lightColor": "#ff9050"},
    }),
    "playerStart": start,
    "spawns": spawns,
    "gates": gates,
    "shrines": [{"id": "throne_landing_shrine", "name": "The Last Landing", "at": shrine}],
    "exits": [
        {"id": "stair", "label": "The Reliquary", "at": [cx, H - 2], "radius": 1.5, "to": "reliquary", "toExit": "throne",
         "arrive": start},
    ],
    # Malachar spared (shown his daughter's toy, M5 step 3) sits on the Throne's step.
    "npcs": [
        {"id": "malachar_spared", "name": "Malachar Vane", "title": "Spared", "at": [throne[0], throne[1] - 2], "facing": 2, "still": True,
         "dialogue": "malachar_spared", "requires": {"act5.malachar": "spared"},
         "look": {"head": "mage_hood", "chest": "mage_vest", "hands": "mage_sleeves", "legs": "mage_skirt", "feet": "mage_boots"}},
    ],
    "interactables": [
        {"id": "throne", "name": "The Cinder Throne", "at": [throne_at[0], throne_at[1] + 1], "dialogue": "cinder_throne_seat", "blocks": False},
    ],
}
write(zone, sys.argv[1])
