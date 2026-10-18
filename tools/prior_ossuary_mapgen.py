# Generates zones/prior_ossuary.json: the Prior's Ossuary, an optional dungeon (level 4) under the Drowned Chapel's
# east crypt, down a stair added in the chapel's revision 2. Deterministic: no randomness beyond a fixed LCG.
#
# Top (north) to bottom: the stair hall (the way back up, a shrine), the bone gallery (a long hall of burial niches),
# then three ways down: west to the flooded charnel pit, east to the reliquary vault (the zone's reliquary chest), and
# straight on down the processional. All three meet in the lower hall (a second shrine), and below it lies the Prior's
# tomb, where Prior Aldous Venn of the Pale Lantern, a named elite, keeps his guard. He always drops a rare, and his
# confession lies on a lectern in his tomb. Champions here carry the Hasted, Shielded and Tainted affixes.
#
# Walls, lights and the legend are the Drowned Chapel's (tools/drowned_chapel_mapgen.py): far walls stand full height,
# near walls are low blocks, torches and braziers light the dark.
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import json, sys

REVISION = 1
W, H = 56, 76
grid = [['#'] * W for _ in range(H)]
seed = 24680
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

def room(x0, y0, x1, y1, ch='.'):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            grid[y][x] = ',' if ch == '.' and rnd() < 0.22 else ch
def put(x, y, ch):
    grid[y][x] = ch

# --- The rooms and the passages between them ---
room(22, 3, 33, 10)                                    # the stair hall
room(26, 11, 29, 14)                                   # down into the gallery
room(6, 15, 49, 22)                                    # the bone gallery
room(6, 23, 9, 34)                                     # west, down to the charnel pit
room(3, 35, 22, 48)                                    # the charnel pit
room(46, 23, 49, 30)                                   # east, to the reliquary vault
room(36, 31, 53, 41)                                   # the reliquary vault
room(25, 23, 30, 44)                                   # the processional
room(10, 45, 45, 54)                                   # the lower hall (meets the pit and the processional)
room(40, 42, 43, 44)                                   # from the vault down to the lower hall
room(25, 55, 30, 58)                                   # the tomb's antechamber
room(16, 59, 39, 72)                                   # the Prior's tomb

# --- Flooding: the charnel pit and the lower hall's low end lie under the Rotwater (poison bog) ---
def flood(x0, y0, x1, y1, p=1.0):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if grid[y][x] in '.,' and rnd() < p:
                grid[y][x] = '~'
flood(5, 38, 20, 47, 0.8)
flood(10, 49, 17, 54, 0.6)
flood(18, 66, 22, 71, 0.5)

# --- Furniture ---
for x in range(8, 49, 3):                              # burial niches along both gallery walls
    put(x, 15, 'u')
    if x % 2 == 0:
        put(x, 22, 'u')
for x in (14, 22, 33, 41):                             # the gallery's pillars
    put(x, 18, 'I')
    put(x, 20, 'I')
for y in range(26, 43, 4):                             # statues along the processional
    put(25, y, 's')
    put(30, y, 's')
for (x, y) in [(38, 33), (51, 33), (38, 39), (51, 39), (24, 5), (31, 5), (12, 47), (43, 47), (19, 61), (36, 61)]:
    put(x, y, 'Z')                                     # braziers
for (x, y) in [(42, 36), (45, 36), (48, 36)]:
    put(x, y, 'R')                                     # runes before the vault's reliquary
for (x, y) in [(25, 66), (26, 66), (29, 66), (30, 66)]:
    put(x, y, 'R')                                     # and before the Prior's sarcophagus
put(27, 69, 'Y'); put(28, 69, 'X')                     # the Prior's sarcophagus
for (x, y) in [(18, 60), (37, 60), (18, 71), (37, 71), (34, 52), (21, 52)]:
    put(x, y, 'u')
for (x, y) in [(7, 37), (13, 41), (19, 39), (9, 46), (16, 36), (35, 47), (24, 50), (40, 52), (12, 27), (47, 25),
               (20, 70), (35, 68), (44, 20), (10, 19)]:
    put(x, y, 'q')                                     # bones
for (x, y) in [(52, 40), (37, 40), (32, 9)]:
    put(x, y, 'k')                                     # crates
shrine1, shrine2 = [30, 8], [28, 49]
for s in (shrine1, shrine2):
    put(*s, 'S')
lectern = [23, 68]                                     # the Prior's confession (an interactable)

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

# Dress the far walls: torches every few cells, banners, shelves, statues in alcoves, cracks.
for y in range(H):
    for x in range(W):
        c = grid[y][x]
        if c not in 'AB':
            continue
        r = rnd()
        torch = (x * 5 + y * 3) % 11 == 0
        if torch:
            grid[y][x] = 'a' if c == 'A' else 'b'
        elif r < 0.12:
            grid[y][x] = 'e' if c == 'A' else 'f'
        elif r < 0.20:
            grid[y][x] = 'g' if c == 'A' else 'h'
        elif r < 0.26:
            grid[y][x] = 'i' if c == 'A' else 'j'
        elif r < 0.34:
            grid[y][x] = 'm' if c == 'A' else 'n'

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
    cache("gallery", 7, 17),
    cache("charnel", 4, 36),
    cache("vault", 45, 34, kind="Reliquary", facing=1),
    cache("lower_hall", 44, 53, facing=1),
    cache("tomb", 33, 70, facing=1),
]

def pack(*members, at, radius=1.8, level=4, flag=None, value="cleared"):
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
N, M, C, E = "Normal", "Minion", "Champion", "Elite"
spawns = [
    # The bone gallery
    pack(("drowned_dead", N, 3), at=[14, 18]),
    pack(("bone_archer", N, 2), ("drowned_dead", N, 1), at=[27, 19]),
    pack(("grave_knight", C, 1, ["hasted"]), ("drowned_dead", N, 2), at=[40, 18]),
    # West: the charnel pit
    pack(("fen_zombie", N, 3), at=[8, 29]),
    pack(("plague_crawler", N, 4), at=[11, 40]),
    pack(("fen_zombie", C, 1, ["tainted"]), ("fen_zombie", N, 2), at=[17, 44]),
    # East: the reliquary vault
    pack(("cultist", N, 2), ("cult_chanter", N, 1), at=[47, 26]),                   # in the corridor, apart from the vault's own
    pack(("cultist", C, 1, ["shielded"]), ("cultist", N, 1), ("cult_chanter", N, 1), at=[48, 38]),
    # The processional
    pack(("bone_archer", N, 1), ("drowned_dead", N, 2), at=[27, 33]),
    # The lower hall
    pack(("bog_witch", N, 1), ("fen_zombie", M, 4), at=[19, 50]),
    pack(("grave_knight", N, 2), at=[36, 49]),
    pack(("cursed_grave", N, 1), at=[42, 52], radius=0),
    # The Prior's tomb
    # Level 3: an elite fights two levels up, so the Prior meets a level-4 hero as a level-5 foe.
    pack(("drowned_prior", E, 1, ["shielded"]), ("drowned_dead", N, 2), ("bone_archer", N, 1), at=[27, 64], level=3,
         flag="act1.prior", value="slain"),
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
    "id": "prior_ossuary", "name": "The Prior's Ossuary", "level": 4, "revision": REVISION, "tileset": "tileset_dungeon",
    "tagline": "The Order buried its shame with its dead.",
    "darkness": 0.95, "heroLight": 4.5, "xpScale": 0.6,
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
        "I": thing([110, 111]),
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
    "playerStart": [27, 7],
    "spawns": spawns,
    "shrines": [
        {"id": "ossuary_shrine", "name": "The Ossuary Stair", "at": shrine1},
        {"id": "lower_hall_shrine", "name": "The Lower Hall Shrine", "at": shrine2},
    ],
    "interactables": [
        {"id": "prior_confession", "name": "The Prior's lectern", "at": lectern, "tile": 148, "dialogue": "ossuary_confession"},
    ],
    "exits": [
        {"id": "chapel", "label": "The Drowned Chapel", "at": [27, 4], "radius": 1.5, "to": "drowned_chapel", "toExit": "ossuary",
         "arrive": [27, 7]},
    ],
    "caches": caches,
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
