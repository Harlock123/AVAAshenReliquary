# Generates zones/bell_tower.json: Act 3's last zone (level 14), the Bell Tower of Saint Oriel, climbed from the
# catacombs' south stair, with the Choir of Whispers in its belfry. Deterministic: no randomness beyond a fixed LCG.
#
# The tower, bottom (south) to top: the tower foot (the stair up from the catacombs, a shrine; Brother Caddoc waits here
# if he was forgiven), the bell-ringers' hall, the sisters' three cells off its west side (a note in each: what colour
# each sister's candle was), the candle chapel off its east side (Three Candles: a stand for each sister, each burning
# the wrong candle, and the snuffer), the ringing chamber (a second shrine), and the belfry, a round arena under the
# silent bells where the Choir sings. Behind it the cliff road north waits on the Choir's death (a Herald gate; Act 4 is
# still to come).
#
# Walls as in the Catacombs of Oriel (Flare's dungeon tileset): a solid cell next to floor shows a face toward it; the far
# walls stand full height, the near walls are low. Dark: braziers light it; the hero carries a small light.
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import json, math, sys

REVISION = 1
W, H = 50, 84
grid = [['#'] * W for _ in range(H)]
seed = 271828
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

def room(x0, y0, x1, y1, ch='.'):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            grid[y][x] = ',' if ch == '.' and rnd() < 0.18 else ch
def put(x, y, ch):
    grid[y][x] = ch

# --- The rooms and the passages between them ---
BX, BY, BR = 25, 13, 10                                # the belfry: a round arena under the bells
for y in range(BY - BR, BY + BR + 1):
    for x in range(BX - BR, BX + BR + 1):
        if (x - BX) ** 2 + (y - BY) ** 2 <= (BR + 0.3) ** 2:
            grid[y][x] = ',' if rnd() < 0.18 else '.'
room(24, 2, 26, 3)                                     # the cliff road out, behind the belfry
room(24, 24, 26, 28)                                   # the stair down to the ringing chamber
room(13, 29, 37, 39)                                   # the ringing chamber
room(24, 40, 26, 45)
room(11, 46, 39, 60)                                   # the bell-ringers' hall
room(6, 42, 9, 60)                                     # the cells' corridor, west
room(10, 52, 10, 54)
for (y0, door) in [(42, 43), (48, 49), (54, 55)]:     # the sisters' cells: Agnete's, Brigid's, Cille's
    room(1, y0, 4, y0 + 3)
    room(5, door, 5, door)
room(42, 42, 48, 58)                                   # the candle chapel, east
room(40, 50, 41, 52)
room(24, 61, 26, 68)
room(17, 69, 33, 79)                                   # the tower foot

# --- Furniture ---
for (dx, dy) in [(-6, -6), (6, -6), (-6, 6), (6, 6)]:
    put(BX + dx, BY + dy, 'I')                         # the belfry's piers
for (x, y) in [(BX - 3, BY - 1), (BX + 3, BY - 1), (BX, BY + 3), (BX - 7, BY + 1), (BX + 7, BY + 1)]:
    put(x, y, 'r')                                     # bell ropes hanging from the dark
for y in range(46, 60, 4):                             # the chapel's pews
    put(44, y, 'p'); put(46, y, 'p')
put(45, 42, 'Y')                                       # the chapel's altar
for (x, y) in [(4, 45), (4, 51), (4, 57)]:
    put(x, y, 'd')                                     # the sisters' cots
for (x, y) in [(16, 48), (34, 48), (16, 58), (34, 58), (15, 31), (35, 37)]:
    put(x, y, 'I')                                     # pillars in the hall and the ringing chamber
for (x, y) in [(BX - 9, BY - 3), (BX + 9, BY - 3), (BX - 9, BY + 4), (BX + 9, BY + 4), (BX - 4, BY - 3), (BX + 4, BY - 3),
               (BX - 4, BY + 3), (BX + 4, BY + 3), (14, 30), (36, 30), (12, 47), (38, 47),
               (12, 59), (38, 59), (43, 57), (18, 70), (32, 70), (7, 43)]:
    put(x, y, 'Z')                                     # braziers
for (x, y) in [(20, 52), (31, 55), (8, 58), (22, 36), (29, 33), (21, 76), (BX + 4, BY + 7)]:
    put(x, y, 'q')                                     # bones
shrine1, shrine2 = [30, 73], [34, 33]
for s in (shrine1, shrine2):
    put(*s, 'S')

# --- Walls ---
def floor(x, y):
    return 0 <= x < W and 0 <= y < H and grid[y][x] not in '#'
out = [row[:] for row in grid]
for y in range(H):
    for x in range(W):
        if grid[y][x] != '#':
            continue
        sw, se, ne, nw = floor(x, y + 1), floor(x + 1, y), floor(x, y - 1), floor(x - 1, y)
        if sw and se:
            out[y][x] = 'C'
        elif sw:
            out[y][x] = 'A'
        elif se:
            out[y][x] = 'B'
        elif floor(x + 1, y + 1):
            out[y][x] = 'D'
        elif ne and nw:
            out[y][x] = 'O'
        elif ne:
            out[y][x] = 'L'
        elif nw:
            out[y][x] = 'M'
        elif floor(x - 1, y - 1):
            out[y][x] = 'N'
grid = out

# Dress the far walls: torches every few cells, banners, cracks.
for y in range(H):
    for x in range(W):
        c = grid[y][x]
        if c not in 'AB':
            continue
        r = rnd()
        if (x * 5 + y * 3) % 8 == 0:
            grid[y][x] = 'a' if c == 'A' else 'b'
        elif r < 0.16:
            grid[y][x] = 'e' if c == 'A' else 'f'
        elif r < 0.24:
            grid[y][x] = 'g' if c == 'A' else 'h'
grid[1][25] = 'H'                                      # the cliff road's stair, north
grid[80][25] = 'H'                                     # the stair down to the catacombs

rows = [''.join(r) for r in grid]

def open_cell(x, y):
    best = None
    for dy in range(-4, 5):
        for dx in range(-4, 5):
            cx, cy = x + dx, y + dy
            if not (1 <= cx < W - 1 and 1 <= cy < H - 1) or rows[cy][cx] != '.':
                continue
            if sum(rows[cy + ny][cx + nx] in '.,' for nx, ny in ((1, 0), (-1, 0), (0, 1), (0, -1))) < 2:
                continue
            d = dx * dx + dy * dy
            if best is None or d < best[0]:
                best = (d, [cx, cy])
    return best[1]
def cache(id, x, y, kind="Cache", facing=0):
    return {"id": id, "at": open_cell(x, y), "kind": kind, "facing": facing}
caches = [
    cache("tower_foot", 32, 78),
    cache("cilles_cell", 2, 57),
    cache("candle_chapel", 47, 57, facing=1),
    cache("ringing_chamber", 14, 38),
    cache("belfry", BX + 7, BY - 5, kind="Reliquary", facing=1),
]

def pack(*members, at, radius=1.8, level=None):
    out = []
    for m in members:
        d = dict(zip(("creature", "rank", "count"), m[:3]))
        if len(m) > 3:
            d["affixes"] = m[3]
        out.append(d)
    spawn = {"at": at, "radius": radius, "pack": out}
    if level:
        spawn["level"] = level
    return spawn
N, C, B = "Normal", "Champion", "Herald"
DEAD, ACO, WHISP, MIRROR, PHANT, WRAITH = ("catacomb_dead", "choir_acolyte", "whisperer", "mirror_cultist", "phantom", "frost_wraith")
spawns = [
    # The tower foot and the bell-ringers' hall (level 14)
    pack((DEAD, N, 3), (WHISP, N, 1), at=[22, 73]),
    pack((MIRROR, N, 2), (ACO, N, 1), at=[18, 51]),
    pack((PHANT, N, 2), (WRAITH, N, 1), at=[33, 52]),
    pack((MIRROR, C, 1, ["mirrored"]), (ACO, N, 1), at=[25, 57]),
    # The sisters' cells and the candle chapel
    pack((DEAD, N, 3), at=[7, 49], radius=1.0),
    pack((ACO, N, 2), (WHISP, N, 1), at=[45, 51]),
    # The ringing chamber
    pack((ACO, C, 1, ["hasted"]), (MIRROR, N, 2), at=[19, 34]),
    pack((WHISP, N, 2), (PHANT, N, 2), at=[30, 36]),
    # The stair to the belfry
    pack((MIRROR, C, 1, ["shielded", "mirrored"]), (ACO, N, 1), at=[25, 26], radius=1.0),
    # The Choir of Whispers
    pack(("choir_agnete", B, 1), at=[BX, BY], radius=0, level=13),            # a level under the hero who meets her, as Hadric is
]
FLOOR = [16, 17, 18, 19]
CRACKED = [36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47]
def wall(ids, light=0, color=None):
    e = {"ground": "none", "objects": ids, "blocks": True, "blocksSight": True}
    if light:
        e["light"] = light
        if color:
            e["lightColor"] = color
    return e
def thing(ids, blocks=True, light=0, tiles=FLOOR, sight=False, color=None):
    e = {"ground": "floor", "tiles": tiles, "objects": ids, "blocks": blocks}
    if sight:
        e["blocksSight"] = True
    if light:
        e["light"] = light
        if color:
            e["lightColor"] = color
    return e
def note(id, at, dialogue):
    return {"id": id, "name": "A sister's hymnal", "at": at, "tile": 148, "dialogue": dialogue}
def stand(sister, at):
    return {"id": "candle_" + sister, "name": f"{sister.capitalize()}'s candle", "at": at, "tile": 166, "action": "candle"}
zone = {
    "id": "bell_tower", "name": "The Bell Tower", "level": 14, "act": 3, "revision": REVISION, "xpScale": 0.8,
    "tileset": "tileset_dungeon",
    "tagline": "Three voices, one song, and the bells silent above them.",
    "darkness": 0.88, "heroLight": 5.0,
    "rows": rows,
    "legend": {
        ".": {"ground": "floor", "tiles": FLOOR},
        ",": {"ground": "floor", "tiles": CRACKED},
        "#": {"ground": "none", "blocks": True, "blocksSight": True},
        "A": wall([65, 69, 97]),
        "B": wall([64, 68, 96]),
        "a": wall([103], 3.5),
        "b": wall([102], 3.5),
        "e": wall([107]),
        "f": wall([106]),
        "g": wall([101]),
        "h": wall([100]),
        "C": wall([73]),
        "D": wall([74]),
        "L": wall([80]),
        "M": wall([81]),
        "N": wall([88]),
        "O": wall([89]),
        "H": wall([285]),
        "I": thing([110, 111], sight=True),
        "r": thing([192, 193], blocks=False),
        "p": thing([136, 137]),
        "Y": thing([134], light=3.0, color="#c9a8ff"),
        "d": thing([202, 203, 204, 205]),
        "Z": thing([167], light=4.5),
        "q": thing([176, 177, 178, 179, 180, 181, 182, 183], blocks=False),
        "S": thing([265], blocks=False, light=3.5),
    },
    "playerStart": [25, 77],
    "spawns": spawns,
    "shrines": [
        {"id": "tower_foot_shrine", "name": "The Tower Foot Shrine", "at": shrine1},
        {"id": "ringing_chamber_shrine", "name": "The Ringing Chamber Shrine", "at": shrine2},
    ],
    "exits": [
        {"id": "catacombs", "label": "The Catacombs of Oriel", "at": [25, 79], "radius": 1.5, "to": "catacombs_of_oriel",
         "toExit": "bell_tower", "arrive": [25, 76]},
        {"id": "cliff_road", "label": "The Ember Gate Camp", "at": [25, 2], "radius": 1.5, "to": "ember_gate", "toExit": "cliff_road",
         "arrive": [25, 5], "requires": {"act3.choir": "slain"},
         "sealedText": "The Choir's song fills the belfry. There is no going on while they sing."},
    ],
    "caches": caches,
    "npcs": [
        {"id": "caddoc", "name": "Brother Caddoc", "title": "Penitent", "at": open_cell(20, 77), "facing": 2,
         "portrait": "flare/male10", "dialogue": "tower_caddoc", "requires": {"act3.penitent": "forgiven"},
         "look": {"head": "leather_hood", "chest": "mage_vest", "hands": "cloth_gloves", "legs": "cloth_pants", "feet": "cloth_sandals",
                  "main": "staff"}},
    ],
    "interactables": [
        note("agnete_hymnal", [1, 42], "tower_note_agnete"),
        note("brigid_hymnal", [1, 48], "tower_note_brigid"),
        note("cille_hymnal", [1, 54], "tower_note_cille"),
        stand("agnete", [43, 44]),
        stand("brigid", [45, 44]),
        stand("cille", [47, 44]),
        {"id": "snuffer", "name": "A brass snuffer", "at": [42, 56], "tile": 150, "dialogue": "tower_snuffer"},
    ],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
