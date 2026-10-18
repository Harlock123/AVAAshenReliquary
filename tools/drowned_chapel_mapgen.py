# Generates zones/drowned_chapel.json: Act 1's third zone (levels 3-4), a dungeon, the Lantern Order's chapel half
# under the Rotwater. Deterministic: no randomness beyond a fixed LCG.
#
# The chapel, top (north) to bottom: the narthex (the way in from the Fens, a shrine, a pilgrim's lantern), the nave
# (long, flooded, pillared, moonlight through the high windows) with three side chapels either side, the transept
# (a second shrine), the choir and sanctum (the Prior's guard and the zone's reliquary), and below the transept's ends
# the east crypt (cursed graves) and the west crypt (level 4, a third shrine) whose flooded tunnel runs on toward
# Sallow's Mire (zone 1.4).
#
# Walls: a solid cell next to floor shows a face toward it. The far walls (floor to their south-west or south-east on
# screen) stand full height in Flare's dungeon blocks; the near walls are low blocks, so they never hide the hero. It is
# dark: torches on the walls, braziers, moonlight shafts and shrines light it; the hero carries a small light.
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import json, sys

REVISION = 1
W, H = 64, 112
grid = [['#'] * W for _ in range(H)]
seed = 13579
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
room(24, 4, 39, 13)                                    # the narthex
room(30, 14, 33, 16)                                   # its doors into the nave
room(22, 17, 41, 64)                                   # the nave
side = [(20, 29), (36, 45), (52, 61)]
for (y0, y1) in side:
    room(8, y0, 17, y1 - 1)                            # west side chapels
    room(18, y0 + 3, 21, y0 + 5)
    room(46, y0, 55, y1 - 1)                           # east side chapels
    room(42, y0 + 3, 45, y0 + 5)
room(28, 65, 35, 67)                                   # the nave's south arch
room(12, 68, 51, 77)                                   # the transept
room(30, 78, 33, 80)                                   # into the choir
room(24, 81, 39, 94)                                   # the choir and sanctum
room(12, 78, 15, 81)                                   # down to the west crypt
room(4, 82, 21, 95)                                    # the west crypt
room(8, 96, 11, 98)
room(4, 99, 25, 105)                                   # the west crypt's flooded vault
room(12, 106, 15, 109)                                 # the tunnel toward the Mire
room(48, 78, 51, 81)                                   # down to the east crypt
room(42, 82, 59, 95)                                   # the east crypt

# --- Flooding: the Rotwater has come in (poison bog, on flagstones) ---
def flood(x0, y0, x1, y1, p=1.0):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if grid[y][x] in '.,' and rnd() < p:
                grid[y][x] = '~'
flood(23, 40, 40, 52, 0.9)                             # the nave's sunken middle
flood(22, 53, 30, 58, 0.7)
flood(4, 99, 25, 105, 0.85)                            # the west crypt's vault
flood(12, 106, 15, 109)
flood(13, 70, 18, 76, 0.6)

# --- Furniture ---
for y in range(20, 63, 5):                             # the nave's two rows of pillars
    for x in (25, 38):
        put(x, y, 'I')
for y in range(22, 38, 3):                             # pews, before the water
    for x in range(28, 36, 2):
        put(x, y, 'p')
for (x, y) in [(26, 6), (37, 6)]:                      # braziers either side of the narthex
    put(x, y, 'Z')
for (x, y) in [(15, 72), (48, 72), (27, 84), (36, 84)]:
    put(x, y, 'Z')
for (x, y) in [(29, 90), (30, 90), (33, 90), (34, 90)]:  # glowing runes before the altar
    put(x, y, 'R')
put(31, 92, 'Y'); put(32, 92, 'X')                     # the altar (a stone tomb)
for (x, y) in [(10, 22), (15, 26), (49, 23), (53, 41), (11, 41), (50, 57)]:
    put(x, y, 's' if rnd() < 0.5 else 'u')            # statues and sarcophagi in the side chapels
for (x, y) in [(6, 85), (9, 85), (12, 85), (15, 85), (6, 90), (9, 90), (12, 90), (15, 90)]:
    put(x, y, 'u')                                     # the west crypt's tombs
for (x, y) in [(45, 86), (50, 88), (55, 86), (47, 92), (56, 92)]:
    put(x, y, 'u')
for (x, y) in [(10, 27), (13, 21), (52, 26), (47, 38), (14, 58), (54, 54), (8, 93), (20, 84), (58, 84), (43, 94),
               (24, 102), (33, 74), (40, 70)]:
    put(x, y, 'q')                                     # bones
for (x, y) in [(36, 9), (27, 11), (52, 44)]:
    put(x, y, 'k')                                     # barrels and crates
shrine1, shrine2, shrine3 = [31, 9], [31, 73], [18, 88]
for s in (shrine1, shrine2, shrine3):
    put(*s, 'S')
lantern = [35, 11]                                     # the pilgrim's lantern on its lectern (an interactable)

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

# Dress the far walls: torches every few cells (they light the chapel), banners, shelves, statues in alcoves, cracks;
# moonlight falls through the nave's west windows.
for y in range(H):
    for x in range(W):
        c = grid[y][x]
        if c not in 'AB':
            continue
        r = rnd()
        torch = (x * 7 + y * 3) % 9 == 0
        if torch:
            grid[y][x] = 'a' if c == 'A' else 'b'
        elif r < 0.10:
            grid[y][x] = 'e' if c == 'A' else 'f'
        elif r < 0.17:
            grid[y][x] = 'g' if c == 'A' else 'h'
        elif r < 0.22:
            grid[y][x] = 'i' if c == 'A' else 'j'
        elif r < 0.30:
            grid[y][x] = 'm' if c == 'A' else 'n'
for y in range(20, 62, 7):                             # moonlight shafts along the nave's west wall
    grid[y][22] = 'V'

rows = [''.join(r) for r in grid]

# Chests sit on the nearest plain floor cell with open floor beside it.
def open_cell(x, y):
    best = None
    for dy in range(-4, 5):
        for dx in range(-4, 5):
            cx, cy = x + dx, y + dy
            if not (1 <= cx < W - 1 and 1 <= cy < H - 1) or rows[cy][cx] != '.':
                continue
            if sum(rows[cy + ny][cx + nx] in '.,~' for nx, ny in ((1, 0), (-1, 0), (0, 1), (0, -1))) < 2:
                continue
            d = dx * dx + dy * dy
            if best is None or d < best[0]:
                best = (d, [cx, cy])
    return best[1]
def cache(id, x, y, kind="Cache", facing=0):
    return {"id": id, "at": open_cell(x, y), "kind": kind, "facing": facing}
caches = [
    cache("west_chapel_1", 9, 24),
    cache("east_chapel_1", 54, 24, facing=1),
    cache("west_chapel_2", 9, 43),
    cache("east_chapel_3", 54, 59, facing=1),
    cache("transept", 50, 70, facing=1),
    cache("sanctum", 36, 92, kind="Reliquary"),
    cache("west_crypt", 5, 93),
    cache("east_crypt", 58, 93, facing=1),
]

def pack(*members, at, radius=1.8, level=3, flag=None, value="cleared"):
    out = []
    for m in members:
        d = dict(zip(("creature", "rank", "count"), m[:3]))
        if len(m) > 3:
            d["affixes"] = m[3]
        out.append(d)
    spawn = {"at": at, "radius": radius, "pack": out, "level": level}
    if flag:
        spawn["flag"], spawn["value"] = flag, value
    return spawn
N, M, C = "Normal", "Minion", "Champion"
V = ["vengeful"]
spawns = [
    # The nave (level 3)
    pack(("drowned_dead", N, 2), at=[31, 20]),
    pack(("cultist", N, 2), ("cult_chanter", N, 1), at=[31, 30]),
    pack(("fen_zombie", N, 3), at=[28, 44]),
    pack(("drowned_dead", N, 2), ("bone_archer", N, 1), at=[36, 50]),
    pack(("bog_witch", N, 1), ("fen_zombie", M, 4), at=[27, 56]),
    pack(("grave_knight", N, 1), ("drowned_dead", N, 1), at=[33, 62]),
    # The side chapels (level 3)
    pack(("bone_archer", N, 1), ("drowned_dead", N, 1), at=[12, 25]),
    pack(("cultist", N, 2), at=[51, 25]),
    pack(("cursed_grave", N, 1), at=[12, 40], radius=0),
    pack(("fen_zombie", N, 2), ("drowned_dead", N, 1), at=[50, 41]),
    pack(("cultist", N, 1), ("cult_chanter", N, 1), at=[12, 57]),
    pack(("grave_knight", N, 1), ("bone_archer", N, 1), at=[51, 57]),
    # The transept (level 3)
    pack(("cultist", N, 2), ("cult_chanter", N, 1), at=[20, 72]),
    pack(("drowned_dead", N, 3), at=[42, 73]),
    # The choir and sanctum: the Prior's guard (level 3)
    pack(("bone_archer", N, 2), at=[28, 86]),
    pack(("grave_knight", C, 1, V), ("drowned_dead", N, 2), at=[32, 89], level=3, flag="act1.chapel"),  # the chapel cleansed
    # The east crypt (level 3)
    pack(("cursed_grave", N, 1), at=[46, 84], radius=0),
    pack(("bone_archer", N, 1), ("drowned_dead", N, 2), at=[52, 90]),
    pack(("cursed_grave", N, 1), at=[57, 88], radius=0),
    # The west crypt (level 4)
    pack(("drowned_dead", N, 2), ("grave_knight", N, 1), at=[10, 87], level=4),
    pack(("bog_witch", N, 1), ("fen_zombie", M, 3), at=[16, 92], level=4),
    pack(("fen_zombie", N, 3), at=[10, 102], level=4),
    pack(("cultist", C, 1, V), ("cultist", N, 1), ("cult_chanter", N, 1), at=[20, 102], level=4),
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
def thing(ids, blocks=True, light=0, color=None, tiles=FLOOR):
    e = {"ground": "floor", "tiles": tiles, "objects": ids, "blocks": blocks}
    if light:
        e["light"] = light
        if color:
            e["lightColor"] = color
    return e
zone = {
    "id": "drowned_chapel", "name": "The Drowned Chapel", "level": 3, "revision": REVISION, "tileset": "tileset_dungeon",
    "tagline": "The Lantern went out here first.",
    "darkness": 0.93, "heroLight": 4.5,
    "rows": rows,
    "legend": {
        ".": {"ground": "floor", "tiles": FLOOR},
        ",": {"ground": "floor", "tiles": CRACKED},
        "~": {"ground": "bog", "tiles": FLOOR + CRACKED},
        "#": {"ground": "none", "blocks": True, "blocksSight": True},
        "A": wall([65, 69, 97]),
        "B": wall([64, 68, 96]),
        "a": wall([103], 3.5),
        "b": wall([102], 3.5),
        "e": wall([107]),
        "f": wall([106]),
        "g": wall([101]),
        "h": wall([100]),
        "i": wall([99]),
        "j": wall([98]),
        "m": wall([105]),
        "n": wall([104]),
        "C": wall([73]),
        "D": wall([74]),
        "L": wall([80]),
        "M": wall([81]),
        "N": wall([88]),
        "O": wall([89]),
        "V": wall([241, 242], 3.2, "#8ea6ff"),
        "I": thing([110, 111]),
        "p": thing([136, 137]),
        "Z": thing([167], light=4.5),
        "R": thing([60, 61, 62, 63], blocks=False, light=1.4, color="#7f9cff", tiles=[60, 61, 62, 63]),
        "Y": thing([134]),
        "X": thing([]),
        "s": thing([128, 129, 130, 131]),
        "u": thing([118, 119, 120, 121, 122, 123, 124, 125, 126, 127]),
        "q": thing([176, 177, 178, 179, 180, 181, 182, 183], blocks=False),
        "k": thing([146, 147, 162, 163]),
        "S": thing([265], blocks=False, light=3.5, color="#6ea8ff"),
    },
    "playerStart": [31, 6],
    "spawns": spawns,
    "shrines": [
        {"id": "narthex_shrine", "name": "The Narthex Shrine", "at": shrine1},
        {"id": "transept_shrine", "name": "The Transept Shrine", "at": shrine2},
        {"id": "crypt_shrine", "name": "The Crypt Shrine", "at": shrine3},
    ],
    "interactables": [
        {"id": "pilgrims_lantern", "name": "Pilgrim's lantern", "at": lantern, "tile": 148, "dialogue": "chapel_lantern"},
    ],
    "exits": [
        {"id": "fens", "label": "The Rotwater Fens", "at": [31, 4], "radius": 1.5, "to": "rotwater_fens", "toExit": "chapel",
         "arrive": [31, 7]},
        {"id": "mire", "label": "Sallow's Mire", "at": [13, 109], "radius": 1.5, "to": "sallows_mire", "toExit": "chapel",
         "arrive": [13, 104]},
    ],
    "caches": caches,
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
