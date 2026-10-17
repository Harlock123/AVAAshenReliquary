# Generates the first draft of zones/gallows_road.json. Deterministic: no randomness beyond a fixed LCG.
import json, math, sys

W, H = 44, 104
grid = [['.'] * W for _ in range(H)]
seed = 1234567
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

def path_x(y):  # winding road centre along the map's long axis
    return 21 + 7 * math.sin(y / 11.0) + 3 * math.sin(y / 4.3)

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

def clear(x0, y0, x1, y1, ch='.'):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if 2 <= x < W - 2 and 2 <= y < H - 2 and grid[y][x] not in '=-':
                grid[y][x] = ch

def put(x, y, ch):
    grid[y][x] = ch

# --- Gallows Hill and graveyard (start, south end: high y) ---
gx = int(path_x(96))
clear(gx - 9, 88, gx + 6, 100)
put(gx + 3, 97, 'g'); put(gx + 3, 98, 'X')           # the gallows post you wake beneath
start = [gx + 1, 98]  # beside the gallows post, not hidden behind it
for (x, y) in [(gx - 7, 90), (gx - 5, 90), (gx - 3, 90), (gx - 7, 92), (gx - 5, 92), (gx - 3, 92), (gx - 7, 94), (gx - 5, 94)]:
    put(x, y, '+')
put(gx - 4, 96, 'C')                                   # a cursed grave
for x in range(gx - 9, gx - 1, 2):
    put(x, 88, 'f')
shrine1 = [gx + 4, 89]
put(*shrine1, 'S')

# --- Ruined farmstead (middle) ---
fx = int(path_x(60)) + 6
clear(fx - 3, 55, fx + 6, 66)
put(fx + 2, 58, 'H')
for (x, y) in [(fx + 1, 57), (fx + 2, 57), (fx + 3, 57), (fx + 1, 58), (fx + 3, 58), (fx + 1, 59), (fx + 2, 59), (fx + 3, 59)]:
    put(x, y, 'X')
for (x, y) in [(fx - 1, 63), (fx, 64), (fx + 4, 63)]:
    put(x, y, 'x')
for x in range(fx - 3, fx + 6, 2):
    put(x, 66, 'f')
shrine2 = [int(path_x(52)) - 3, 52]
clear(shrine2[0] - 1, shrine2[1] - 1, shrine2[0] + 1, shrine2[1] + 1)
put(*shrine2, 'S')

# --- Bog pond (west of the road, upper middle) ---
px = int(path_x(38)) - 10
for y in range(33, 43):
    for x in range(px - 4, px + 5):
        if ((x - px) / 4.5) ** 2 + ((y - 38) / 5.0) ** 2 <= 1 and 2 <= x < W - 2:
            grid[y][x] = 'w'
        elif ((x - px) / 6.0) ** 2 + ((y - 38) / 6.5) ** 2 <= 1 and 2 <= x < W - 2 and grid[y][x] not in '=-':
            grid[y][x] = ',' if rnd() < 0.5 else '.'
for (x, y) in [(px + 5, 35), (px + 5, 41)]:
    if grid[y][x] not in '=-':
        grid[y][x] = 'D'

# --- Fort Gallowmere refugee camp (north end: low y) ---
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
exit_at = [cx, 9]

rows = [''.join(r) for r in grid]
def pack(*members, at, radius=2.0):
    return {"at": at, "radius": radius, "pack": [dict(zip(("creature", "rank", "count"), m)) for m in members]}
P = lambda y, dx=0: [int(path_x(y)) + dx, y]
spawns = [
    pack(("fen_zombie", "Normal", 2), at=P(84, -1)),
    pack(("cursed_grave", "Normal", 1), at=[gx - 4, 96], radius=0),
    pack(("drowned_dead", "Normal", 2), at=P(75, 2)),
    pack(("fen_zombie", "Normal", 3), at=P(68, -2)),
    pack(("bone_archer", "Normal", 1), ("drowned_dead", "Normal", 1), at=[fx + 4, 61]),
    pack(("fen_zombie", "Normal", 2), ("drowned_dead", "Normal", 1), at=P(47, 1)),
    pack(("bog_witch", "Normal", 1), ("fen_zombie", "Minion", 4), at=[px + 6, 38]),
    pack(("drowned_dead", "Normal", 3), at=P(30, -1)),
    pack(("bone_archer", "Normal", 2), at=P(24, 3)),
    pack(("grave_knight", "Champion", 1), ("drowned_dead", "Normal", 2), at=P(19, 0)),
]
zone = {
    "id": "gallows_road", "name": "Gallows Road", "level": 1, "tileset": "tileset_grassland",
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
    },
    "playerStart": start,
    "spawns": spawns,
    "shrines": [
        {"id": "gallows_shrine", "name": "The Gallows Shrine", "at": shrine1},
        {"id": "farmstead_shrine", "name": "The Farmstead Shrine", "at": shrine2},
    ],
    "exits": [{"id": "fort_gallowmere", "label": "Fort Gallowmere", "at": exit_at, "radius": 3.0}],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
