# Generates zones/gallows_road.json. Deterministic: no randomness beyond a fixed LCG.
#
# The road runs north (low y) from the gallows at the south end to the barred gate of Fort Gallowmere, through
# seven stretches: Gallows Hill, the Hangman's Wood, the ruined farmstead, the drowned fields, the barrow field,
# the last mile and the refugee camp. Side glades off the road hold optional packs.
#
# Revision 2 (2026-10-17): lengthened from 44x104 with 10 packs to 56x220 with about 25, after the M1 exit test
# took 5 minutes against a 20-minute target. Bump REVISION whenever the layout changes: saves made on another
# revision keep the character but start again at the gallows.
import json, math, sys

REVISION = 2
W, H = 56, 220
grid = [['.'] * W for _ in range(H)]
seed = 1234567
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

def path_x(y):  # winding road centre along the map's long axis
    return 28 + 9 * math.sin(y / 14.0) + 3 * math.sin(y / 5.3)

# Forest border and scattered trees, thinning near the road.
for y in range(H):
    cx = path_x(y)
    for x in range(W):
        d = abs(x - cx)
        edge = min(x, W - 1 - x, y, H - 1 - y)
        if edge < 2:
            grid[y][x] = 'T'
        elif d > 4:
            p = 0.04 + 0.05 * max(0, d - 4)
            if rnd() < min(p, 0.55):
                grid[y][x] = 'T' if rnd() < 0.55 else ('O' if rnd() < 0.7 else 'D')
            elif rnd() < 0.10:
                grid[y][x] = ','
            elif rnd() < 0.015:
                grid[y][x] = 'r'

# The road itself.
for y in range(2, H - 2):
    cx = path_x(y)
    for x in range(W):
        d = abs(x - cx)
        if d <= 1.2:
            grid[y][x] = '='
        elif d <= 2.1 and grid[y][x] in '.,TODr':
            grid[y][x] = '-' if rnd() < 0.7 else '.'

def inside(x, y):
    return 2 <= x < W - 2 and 2 <= y < H - 2

def clear(x0, y0, x1, y1, ch='.'):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if inside(x, y) and grid[y][x] not in '=-':
                grid[y][x] = ch

def put(x, y, ch):
    grid[y][x] = ch

def glade(gx, gy, rx, ry):
    """An oval clearing, joined to the road by a cleared track so it can always be reached."""
    for y in range(gy - ry, gy + ry + 1):
        for x in range(gx - rx, gx + rx + 1):
            if inside(x, y) and ((x - gx) / (rx + 0.5)) ** 2 + ((y - gy) / (ry + 0.5)) ** 2 <= 1 and grid[y][x] not in '=-':
                grid[y][x] = ',' if rnd() < 0.12 else '.'
    rx0 = int(path_x(gy))
    step = 1 if gx > rx0 else -1
    for x in range(rx0, gx, step):
        for y in (gy, gy + 1):
            if inside(x, y) and grid[y][x] not in '=-':
                grid[y][x] = '.'

def pond(px, py, rx, ry):
    for y in range(py - ry - 2, py + ry + 3):
        for x in range(px - rx - 2, px + rx + 3):
            if not inside(x, y):
                continue
            if ((x - px) / rx) ** 2 + ((y - py) / ry) ** 2 <= 1:
                grid[y][x] = 'w'
            elif ((x - px) / (rx + 1.5)) ** 2 + ((y - py) / (ry + 1.5)) ** 2 <= 1 and grid[y][x] not in '=-':
                grid[y][x] = ',' if rnd() < 0.5 else '.'

P = lambda y, dx=0: [int(path_x(y)) + dx, y]

# --- Gallows Hill and its graveyard (the start, at the south end) ---
gx = int(path_x(206))
clear(gx - 12, 196, gx + 7, 213)
put(gx + 3, 207, 'g'); put(gx + 3, 208, 'X')         # the gallows post you wake beneath
start = [gx + 1, 208]                                # beside the gallows post, not hidden behind it
for (x, y) in [(gx - 10, 198), (gx - 8, 198), (gx - 6, 198), (gx - 10, 200), (gx - 8, 200), (gx - 6, 200),
               (gx - 10, 202), (gx - 8, 202), (gx - 6, 202), (gx - 10, 204), (gx - 8, 204)]:
    put(x, y, '+')
cursed1 = [gx - 11, 196]                             # a cursed grave at the graveyard's far corner
put(*cursed1, 'C')
for x in range(gx - 12, gx - 4, 2):
    put(x, 206, 'f')
shrine1 = [gx + 4, 199]
put(*shrine1, 'S')

# --- The Hangman's Wood ---
glade(int(path_x(178)) + 13, 178, 4, 3)              # a poachers' glade to the east
for (x, y) in [(int(path_x(178)) + 15, 176), (int(path_x(178)) + 11, 180)]:
    put(x, y, 'x')
glade(int(path_x(164)) - 12, 164, 3, 3)              # a hanging tree to the west
put(int(path_x(164)) - 12, 164, 'D')

# --- The ruined farmstead (shrine 2 just north of it) ---
fx = int(path_x(142)) + 7
clear(fx - 3, 136, fx + 8, 149)
put(fx + 2, 139, 'H')
for (x, y) in [(fx + 1, 138), (fx + 2, 138), (fx + 3, 138), (fx + 1, 139), (fx + 3, 139), (fx + 1, 140), (fx + 2, 140), (fx + 3, 140)]:
    put(x, y, 'X')
put(fx + 6, 143, 'H')
for (x, y) in [(fx + 5, 142), (fx + 6, 142), (fx + 7, 142), (fx + 5, 143), (fx + 7, 143), (fx + 5, 144), (fx + 6, 144), (fx + 7, 144)]:
    put(x, y, 'X')
for (x, y) in [(fx - 1, 146), (fx, 147), (fx + 4, 146), (fx + 7, 147)]:
    put(x, y, 'x')
for x in range(fx - 3, fx + 9, 2):
    put(x, 149, 'f')
shrine2 = [int(path_x(128)) - 3, 128]
clear(shrine2[0] - 1, shrine2[1] - 1, shrine2[0] + 1, shrine2[1] + 1)
put(*shrine2, 'S')

# --- The drowned fields: two bog ponds either side of the road ---
west = [int(path_x(108)) - 11, 108]
east = [int(path_x(94)) + 11, 94]
pond(west[0], west[1], 4, 5)
pond(east[0], east[1], 4, 4)
for (x, y) in [(west[0] + 6, 104), (west[0] + 6, 112), (east[0] - 6, 91)]:
    if grid[y][x] not in '=-w':
        grid[y][x] = 'D'
glade(int(path_x(118)) + 12, 118, 3, 3)              # a drowned cottage's yard to the east
put(int(path_x(118)) + 13, 117, 'H')
for (dx, dy) in [(0, -1), (1, -1), (2, -1), (0, 0), (2, 0), (0, 1), (1, 1), (2, 1)]:
    put(int(path_x(118)) + 12 + dx, 117 + dy, 'X')
put(int(path_x(118)) + 13, 117, 'H')

# --- The barrow field: old graves, standing stones and a second cursed grave (shrine 3 before the champion) ---
bx = int(path_x(66))
clear(bx - 12, 58, bx - 3, 74)
for (x, y) in [(bx - 11, 60), (bx - 9, 60), (bx - 7, 60), (bx - 11, 63), (bx - 9, 63), (bx - 11, 66), (bx - 7, 66),
               (bx - 9, 69), (bx - 11, 72), (bx - 7, 72)]:
    put(x, y, '+')
for (x, y) in [(bx - 5, 59), (bx - 4, 64), (bx - 5, 70)]:
    put(x, y, 'r')
cursed2 = [bx - 9, 66]
put(*cursed2, 'C')
shrine3 = [int(path_x(56)) + 3, 56]
clear(shrine3[0] - 1, shrine3[1] - 1, shrine3[0] + 1, shrine3[1] + 1)
put(*shrine3, 'S')
glade(int(path_x(46)) + 12, 46, 4, 3)                # the grave knight's barrow, east of the road
for (dx, dy) in [(-3, -2), (3, -2), (-3, 2), (3, 2)]:
    put(int(path_x(46)) + 12 + dx, 46 + dy, 'r')

# --- Fort Gallowmere's refugee camp and gate (the north end) ---
cx = int(path_x(8))
clear(cx - 8, 3, cx + 8, 14)
put(cx, 8, 'c')
for (x, y) in [(cx - 5, 5), (cx + 4, 5), (cx - 6, 11), (cx + 5, 11)]:
    put(x, y, 't'); put(x + 1, y, 'X'); put(x, y + 1, 'X'); put(x + 1, y + 1, 'X')
for (x, y) in [(cx - 2, 12), (cx + 2, 4), (cx - 3, 4)]:
    put(x, y, 'x')
for x in range(cx - 8, cx + 9, 2):
    if grid[15][x] not in '=-':
        put(x, 15, 'f')
put(cx + 2, 16, 's')
# The gate: braced rails across the road on row 2 between stone posts, a fence out to the trees either side.
road2 = [x for x in range(W) if grid[2][x] in '=-']
g0, g1 = min(road2), max(road2)
for x in range(2, W - 2):
    grid[2][x] = 'T'
for x in range(cx - 11, cx + 12):
    grid[2][x] = 'F'
for x in range(g0, g1 + 1):
    grid[2][x] = 'G'
grid[2][g0 - 1] = 'P'
grid[2][g1 + 1] = 'P'
exit_at = [(g0 + g1) // 2, 4]

rows = [''.join(r) for r in grid]

# Chests, placed after the rows are drawn (they sit on open ground and don't change the map), mostly in the side
# glades so exploring pays. Each goes on the plain-grass cell nearest its spot that has open ground beside it.
def open_cell(x, y):
    best = None
    for dy in range(-4, 5):
        for dx in range(-4, 5):
            cx_, cy_ = x + dx, y + dy
            if not inside(cx_, cy_) or rows[cy_][cx_] != '.':
                continue
            if not any(rows[cy_ + ny][cx_ + nx] in '.,=-' for nx, ny in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                continue
            d = dx * dx + dy * dy
            if best is None or d < best[0]:
                best = (d, [cx_, cy_])
    return best[1]
def cache(id, x, y, kind="Cache", facing=0):
    return {"id": id, "at": open_cell(x, y), "kind": kind, "facing": facing}
caches = [
    cache("graveyard", gx - 12, 203),
    cache("poachers", int(path_x(178)) + 15, 179, facing=1),
    cache("hanging_tree", int(path_x(164)) - 13, 163),
    cache("farmstead", fx + 4, 137, facing=1),
    cache("drowned_cottage", int(path_x(118)) + 10, 116),
    cache("barrows", bx - 11, 69, facing=1),
    cache("knights_barrow", int(path_x(46)) + 14, 45, kind="Reliquary"),
    cache("camp", cx + 6, 8, facing=1),
]

def pack(*members, at, radius=2.0):
    return {"at": at, "radius": radius, "pack": [dict(zip(("creature", "rank", "count"), m)) for m in members]}
N, M, C = "Normal", "Minion", "Champion"
spawns = [
    # Gallows Hill
    pack(("cursed_grave", N, 1), at=cursed1, radius=0),
    pack(("fen_zombie", N, 2), at=P(192, -1)),
    # The Hangman's Wood
    pack(("drowned_dead", N, 2), at=P(184, 2)),
    pack(("fen_zombie", N, 3), at=[int(path_x(178)) + 13, 178]),
    pack(("bone_archer", N, 1), ("fen_zombie", N, 2), at=P(170, -1)),
    pack(("drowned_dead", N, 2), ("fen_zombie", N, 1), at=[int(path_x(164)) - 11, 165]),
    pack(("fen_zombie", N, 3), at=P(156, 1)),
    # The ruined farmstead
    pack(("bone_archer", N, 1), ("drowned_dead", N, 1), at=[fx + 4, 141]),
    pack(("drowned_dead", N, 2), at=[fx + 1, 147]),
    pack(("fen_zombie", N, 2), ("drowned_dead", N, 1), at=P(133, -1)),
    # The drowned fields
    pack(("fen_zombie", N, 3), at=P(122, 1)),
    pack(("drowned_dead", N, 2), ("bone_archer", N, 1), at=[int(path_x(118)) + 11, 120]),
    pack(("bog_witch", N, 1), ("fen_zombie", M, 4), at=[west[0] + 7, west[1]]),
    pack(("drowned_dead", N, 3), at=P(100, -1)),
    pack(("bog_witch", N, 1), ("fen_zombie", M, 3), at=[east[0] - 7, east[1]]),
    pack(("fen_zombie", N, 2), ("bone_archer", N, 1), at=P(84, 1)),
    # The barrow field
    pack(("bone_archer", N, 2), at=P(76, 2)),
    pack(("cursed_grave", N, 1), at=cursed2, radius=0),
    pack(("drowned_dead", N, 3), at=[bx - 6, 62]),
    pack(("grave_knight", N, 1), ("drowned_dead", N, 1), at=P(64, 1)),
    pack(("grave_knight", C, 1), ("drowned_dead", N, 2), at=[int(path_x(46)) + 12, 46]),
    # The last mile
    pack(("drowned_dead", N, 3), at=P(38, -1)),
    pack(("bone_archer", N, 2), ("fen_zombie", N, 1), at=P(29, 2)),
    pack(("grave_knight", N, 1), ("drowned_dead", N, 2), at=P(21, 0)),
]
zone = {
    "id": "gallows_road", "name": "Gallows Road", "level": 1, "revision": REVISION, "tileset": "tileset_grassland",
    "rows": rows,
    "legend": {
        ".": {"ground": "grass"},
        ",": {"ground": "grass", "objects": [112, 113, 114, 115, 116, 117, 118, 120, 121, 122, 123, 124, 125, 126, 127]},
        "=": {"ground": "path"},
        "-": {"ground": "path_edge"},
        "T": {"ground": "grass", "objects": [248, 249, 250, 251], "blocks": True, "blocksSight": True},
        "O": {"ground": "grass", "objects": [252, 253, 254, 255], "blocks": True, "blocksSight": True},
        "D": {"ground": "grass", "objects": [244, 245, 246, 247], "blocks": True},
        "r": {"ground": "grass", "objects": [128, 129, 130, 131], "blocks": True},
        "+": {"ground": "grass", "objects": [140, 141, 142, 143], "blocks": True},
        "f": {"ground": "grass", "objects": [105, 106, 109, 110], "blocks": True},
        "w": {"ground": "water"},
        "c": {"ground": "path", "objects": [102], "blocks": True},
        "t": {"ground": "grass", "objects": [72, 75], "blocks": True, "blocksSight": True},
        "x": {"ground": "grass", "objects": [96, 97, 98, 99, 100, 101], "blocks": True},
        "s": {"ground": "grass", "objects": [138, 139], "blocks": True},
        "g": {"ground": "grass", "objects": [138], "blocks": True},
        "S": {"ground": "grass", "objects": [265]},
        "H": {"ground": "grass", "objects": [296], "blocks": True, "blocksSight": True},
        "C": {"ground": "grass"},
        "X": {"ground": "grass", "blocks": True},
        "G": {"ground": "path", "objects": [104, 107], "blocks": True, "stack": True},
        "P": {"ground": "grass", "objects": [132], "blocks": True, "blocksSight": True},
        "F": {"ground": "grass", "objects": [107], "blocks": True},
    },
    "playerStart": start,
    "spawns": spawns,
    "shrines": [
        {"id": "gallows_shrine", "name": "The Gallows Shrine", "at": shrine1},
        {"id": "farmstead_shrine", "name": "The Farmstead Shrine", "at": shrine2},
        {"id": "barrow_shrine", "name": "The Barrow Shrine", "at": shrine3},
    ],
    "exits": [{"id": "fort_gallowmere", "label": "Fort Gallowmere", "at": exit_at, "radius": 2.0}],
    "caches": caches,
    "vendors": [
        {
            "id": "wenna", "name": "Wenna Marsh", "title": "Peddler", "at": [cx - 8, 12], "facing": 5, "stockSize": 12,
            "greeting": "Coin for kit, kit for coin. The fort pays worse.",
            "look": {"head": "leather_hood", "chest": "cloth_shirt", "hands": "cloth_gloves", "legs": "cloth_pants",
                     "feet": "cloth_sandals", "main": "dagger"},
        }
    ],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
