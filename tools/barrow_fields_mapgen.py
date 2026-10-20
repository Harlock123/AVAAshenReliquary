# Generates zones/barrow_fields.json: Act 2's second zone (levels 7-8), the burial ground of the Iron Legion north of
# Ironhold, where the dead come up out of the mounds every night. Deterministic: no randomness beyond a fixed LCG.
#
# The road runs north (low y) from Ironhold's north gate at the south end, through five stretches: the graveward gate,
# the mound field (open barrows that send up the dead), the legion's field (graves in ranks), the barracks road (the
# walled compound of the Old Barracks to the east, zone 2.3) and the hound hill, to the siege road at the north end,
# barred until the Siege Walls (zone 2.4, step 4). The Widow's Yew, a quiet glade to the west, holds Edith Hadric's
# grave (Hadric's Oath); a broken shrine on the hound hill holds a part of the Clockwork Confessor.
#
# Act 2's mechanic here: the barrow dead, legionaries, hounds and knights rise again unless their bodies are destroyed
# (smashed, burned, hallowed or crushed; see GameWorld.Barrows). The tileset is tileset_ironhold (ruins tile n is n + 1000).
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import json, math, sys

REVISION = 1
W, H = 72, 170
grid = [['.'] * W for _ in range(H)]
seed = 8642097
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

def path_x(y):  # an old funeral road, winding between the mounds
    return 36 + 8 * math.sin(y / 17.0) + 3 * math.sin(y / 6.3)

# Dead trees and thorn at the edges; the fields themselves are open, grey grass.
for y in range(H):
    cx = path_x(y)
    for x in range(W):
        d = abs(x - cx)
        edge = min(x, W - 1 - x, y, H - 1 - y)
        if edge < 2:
            grid[y][x] = 'T'
        elif d > 7:
            p = 0.02 + 0.04 * max(0, d - 7)
            if rnd() < min(p, 0.45):
                grid[y][x] = 'D' if rnd() < 0.45 else ('T' if rnd() < 0.6 else 'O')
            elif rnd() < 0.10:
                grid[y][x] = ','
        elif rnd() < 0.06:
            grid[y][x] = ','

for y in range(2, H - 2):
    cx = path_x(y)
    for x in range(W):
        d = abs(x - cx)
        if d <= 1.2:
            grid[y][x] = '='
        elif d <= 2.1 and grid[y][x] in '.,TOD':
            grid[y][x] = '-' if rnd() < 0.7 else '.'

def inside(x, y):
    return 2 <= x < W - 2 and 2 <= y < H - 2

def put(x, y, ch):
    if inside(x, y):
        grid[y][x] = ch

def clear(x0, y0, x1, y1):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if inside(x, y) and grid[y][x] not in '=-':
                grid[y][x] = ',' if rnd() < 0.1 else '.'

def glade(gx, gy, rx, ry):
    """An oval clearing, joined to the road by a cleared track so it can always be reached."""
    for y in range(gy - ry, gy + ry + 1):
        for x in range(gx - rx, gx + rx + 1):
            if inside(x, y) and ((x - gx) / (rx + 0.5)) ** 2 + ((y - gy) / (ry + 0.5)) ** 2 <= 1 and grid[y][x] not in '=-':
                grid[y][x] = ',' if rnd() < 0.1 else '.'
    rx0 = int(path_x(gy))
    step = 1 if gx > rx0 else -1
    for x in range(rx0, gx, step):
        for y in (gy, gy + 1):
            if inside(x, y) and grid[y][x] not in '=-':
                grid[y][x] = '.'

def mound(mx, my):
    """A barrow: a ring of standing stones round a heaped mound."""
    for (dx, dy) in [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)]:
        put(mx + dx, my + dy, 'u')
    put(mx, my, 'X')
    for (dx, dy) in [(-2, -2), (2, -2), (-2, 2), (2, 2)]:
        if inside(mx + dx, my + dy) and grid[my + dy][mx + dx] not in '=-':
            put(mx + dx, my + dy, 'r')

P = lambda y, dx=0: [int(path_x(y)) + dx, y]

# --- The graveward gate (the south end): the way in from Ironhold ---
sx = int(path_x(160))
clear(sx - 7, 152, sx + 7, 166)
for y in range(160, H - 2):
    for x in range(sx - 1, sx + 2):
        grid[y][x] = '='
start = [sx, 161]
exit_south = [sx, 166]
shrine1 = [sx + 4, 156]
put(*shrine1, 'S')
for x in range(sx - 7, sx + 8):                        # the old cemetery wall, broken
    if grid[150][x] not in '=-' and rnd() < 0.7:
        put(x, 150, 'v')

# --- The mound field: barrows either side of the road, some of them open ---
for y in range(112, 150):
    cx = int(path_x(y))
    for x in range(cx - 20, cx + 21):
        if inside(x, y) and grid[y][x] in 'TOD' and rnd() < 0.7:
            grid[y][x] = ',' if rnd() < 0.15 else '.'
mounds = [P(144, -11), P(140, 12), P(132, -14), P(126, 10), P(118, -9), P(114, 15)]
for (mx, my) in mounds:
    mound(mx, my)
open_barrows = [[mounds[1][0] - 3, mounds[1][1] + 2], [mounds[3][0] + 3, mounds[3][1] - 2], [mounds[4][0] - 3, mounds[4][1] + 2]]

# --- The Widow's Yew (a glade west of the mound field): Edith Hadric's grave ---
wx, wy = int(path_x(128)) - 24, 128
glade(wx, wy, 4, 4)
put(wx - 1, wy - 2, 'Y')                               # the yew
grave = [wx + 1, wy]                                   # her headstone (an interactable)

# --- The legion's field: graves in ranks, the legion lying where it was put ---
for y in range(80, 112):
    cx = int(path_x(y))
    for x in range(cx - 18, cx + 19):
        if inside(x, y) and grid[y][x] in 'TOD' and rnd() < 0.75:
            grid[y][x] = ',' if rnd() < 0.15 else '.'
for row in range(84, 110, 4):
    cx = int(path_x(row))
    for x in list(range(cx - 16, cx - 4, 2)) + list(range(cx + 5, cx + 17, 2)):
        if inside(x, row) and grid[row][x] in '.,':
            put(x, row, '+')
for (dx, y) in [(-12, 96), (13, 92)]:
    put(int(path_x(y)) + dx, y, 'Z')                   # broken legion statues

# --- The barracks road: the Old Barracks' walled compound to the east ---
bx, by = int(path_x(68)) + 16, 68
clear(int(path_x(68)) + 2, by - 6, bx + 8, by + 6)
for x in range(bx - 5, bx + 6):
    put(x, by - 5, 'W'); put(x, by + 5, 'W')
for y in range(by - 5, by + 6):
    put(bx - 5, y, 'W'); put(bx + 5, y, 'W')
for y in (by - 1, by, by + 1):                         # the compound's gate, toward the road
    put(bx - 5, y, '.')
put(bx - 5, by - 2, 'Q'); put(bx - 5, by + 2, 'Q')
for (dx, dy) in [(-3, -3), (3, -3), (-3, 3), (3, 3)]:
    put(bx + dx, by + dy, 'k')
put(bx + 3, by, 'H')                                   # the barracks stair down (the exit sits before it)
barracks_exit = [bx + 2, by]
for x in range(int(path_x(by)) + 2, bx - 5):           # a track from the road to the gate
    for y in (by, by + 1):
        if grid[y][x] not in '=-':
            grid[y][x] = '.'
shrine2 = P(74, -4)
clear(shrine2[0] - 1, shrine2[1] - 1, shrine2[0] + 1, shrine2[1] + 1)
put(*shrine2, 'S')

# --- The hound hill: kennels of the dead legion's hounds, and a broken Lantern shrine ---
glade(int(path_x(44)) - 15, 44, 5, 4)
hx, hy = int(path_x(44)) - 15, 44
for (dx, dy) in [(-3, -2), (3, -2), (-3, 2), (3, 2), (0, 3)]:
    put(hx + dx, hy + dy, 'u')
put(hx, hy - 3, 'Y')
cog = [hx + 1, hy - 1]                                 # the governor cog among the bones (an interactable)

# --- The siege road (the north end): barred until the Siege Walls ---
for y in range(4, 32):
    cx = int(path_x(y))
    for x in range(cx - 12, cx + 13):
        if inside(x, y) and grid[y][x] in 'TOD' and rnd() < 0.7:
            grid[y][x] = ',' if rnd() < 0.1 else '.'
shrine3 = P(30, 4)
clear(shrine3[0] - 1, shrine3[1] - 1, shrine3[0] + 1, shrine3[1] + 1)
put(*shrine3, 'S')
nx = int(path_x(3))
for x in range(2, W - 2):
    grid[3][x] = 'T'
for x in range(nx - 6, nx + 7):
    grid[3][x] = 'v'
for x in range(nx - 1, nx + 2):
    grid[3][x] = 'G'
exit_north = [nx, 5]
for (dx, y) in [(-6, 10), (6, 10)]:
    put(nx + dx, y, 'B')

rows = [''.join(r) for r in grid]

def open_cell(x, y):
    best = None
    for dy in range(-4, 5):
        for dx in range(-4, 5):
            cx_, cy_ = x + dx, y + dy
            if not inside(cx_, cy_) or rows[cy_][cx_] != '.':
                continue
            if not any(rows[cy_ + ny][cx_ + nx_] in '.,=-' for nx_, ny in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                continue
            d = dx * dx + dy * dy
            if best is None or d < best[0]:
                best = (d, [cx_, cy_])
    return best[1]
def cache(id, x, y, kind="Cache", facing=0):
    return {"id": id, "at": open_cell(x, y), "kind": kind, "facing": facing}
caches = [
    cache("graveward", sx - 6, 158),
    cache("first_mound", mounds[0][0] + 3, mounds[0][1], facing=1),
    cache("widows_yew", wx - 3, wy + 2),
    cache("deep_mound", mounds[5][0] - 3, mounds[5][1]),
    cache("legion_field", int(path_x(100)) - 15, 101, facing=1),
    cache("barracks_yard", bx + 3, by - 3, facing=1),
    cache("hound_hill", hx - 3, hy, facing=1),
    cache("kings_barrow", int(path_x(20)) + 10, 20, kind="Reliquary"),
]

def pack(*members, at, radius=2.0, level=None):
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
N, C = "Normal", "Champion"
DEAD, LEG, HOUND, WIGHT, KNIGHT, BARROW = "barrow_dead", "barrow_legionary", "grave_hound", "barrow_wight", "barrow_knight", "open_barrow"
spawns = [
    # The graveward gate (level 7)
    pack((DEAD, N, 3), at=P(146, 2)),
    # The mound field: open barrows send up the dead
    pack((BARROW, N, 1), at=open_barrows[0], radius=0),
    pack((DEAD, N, 2), (LEG, N, 1), at=P(138, -3)),
    pack((HOUND, N, 3), at=P(131, 5)),
    pack((BARROW, N, 1), at=open_barrows[1], radius=0),
    pack((LEG, N, 2), (WIGHT, N, 1), at=P(122, -4)),
    pack((BARROW, N, 1), at=open_barrows[2], radius=0),
    pack((KNIGHT, C, 1, ["shielded"]), (DEAD, N, 2), at=P(116, 2)),
    # Near the Widow's Yew (the grave itself is left in peace)
    pack((HOUND, N, 2), (DEAD, N, 1), at=[wx + 7, wy - 4]),
    # The legion's field (level 8 from here)
    pack((LEG, N, 3), at=P(108, -8), level=8),
    pack((LEG, N, 2), (WIGHT, N, 1), at=P(102, 9), level=8),
    pack((DEAD, N, 3), (WIGHT, N, 1), at=P(96, -2), level=8),
    pack((KNIGHT, N, 1), (LEG, N, 2), at=P(90, 7), level=8),
    pack((HOUND, N, 3), at=P(84, -9), level=8),
    # The barracks road
    pack((LEG, N, 2), (KNIGHT, N, 1), at=[bx - 8, by + 2], level=8),
    pack((WIGHT, N, 2), at=[bx, by - 2], radius=1.5, level=8),
    pack((DEAD, N, 3), at=P(60, 3), level=8),
    # The hound hill
    pack((HOUND, C, 1, ["hasted"]), (HOUND, N, 3), at=[hx + 2, hy + 1], level=8),
    pack((HOUND, N, 2), (LEG, N, 1), at=P(50, 6), level=8),
    pack((WIGHT, N, 1), (DEAD, N, 2), at=P(38, -5), level=8),
    # The siege road
    pack((LEG, N, 3), at=P(26, -6), level=8),
    pack((KNIGHT, C, 1, ["vengeful"]), (LEG, N, 2), at=P(20, 3), level=8),
    pack((WIGHT, N, 2), (HOUND, N, 2), at=P(13, -3), level=8),
    pack((KNIGHT, N, 1), (WIGHT, N, 1), (DEAD, N, 2), at=P(8, 5), level=8),
]
zone = {
    "id": "barrow_fields", "name": "The Barrow Fields", "level": 7, "act": 2, "revision": REVISION, "tileset": "tileset_ironhold",
    "tagline": "The Iron Legion was buried with honours. It did not stay buried.",
    "darkness": 0.74,
    "rows": rows,
    "legend": {
        ".": {"ground": "grass"},
        ",": {"ground": "grass", "objects": [112, 113, 114, 115, 116, 117, 118, 120, 121, 122, 123, 124, 125, 126, 127]},
        "=": {"ground": "path"},
        "-": {"ground": "path_edge"},
        "T": {"ground": "grass", "objects": [248, 249, 250, 251], "blocks": True, "blocksSight": True},
        "O": {"ground": "grass", "objects": [252, 253, 254, 255], "blocks": True, "blocksSight": True},
        "D": {"ground": "grass", "objects": [244, 245, 246, 247], "blocks": True},
        "Y": {"ground": "grass", "objects": [244], "blocks": True},
        "r": {"ground": "grass", "objects": [128, 129, 130, 131], "blocks": True},
        "+": {"ground": "grass", "objects": [140, 141, 142, 143], "blocks": True},
        "X": {"ground": "grass", "objects": [1187, 1193], "blocks": True, "blocksSight": True},
        "u": {"ground": "grass", "objects": [1189, 1190, 1192, 1194], "blocks": True, "cover": True},
        "v": {"ground": "grass", "objects": [1187, 1189, 1190, 1192, 1193], "blocks": True, "cover": True},
        "k": {"ground": "grass", "objects": [1285, 1286, 1287, 1288], "blocks": True, "cover": True},
        "W": {"ground": "grass", "objects": [1223, 1224, 1225, 1226, 1227, 1228, 1229], "blocks": True, "blocksSight": True},
        "Q": {"ground": "grass", "objects": [1230], "blocks": True, "blocksSight": True},
        "Z": {"ground": "grass", "objects": [1241, 1242, 1243, 1244], "blocks": True},
        "H": {"ground": "grass", "objects": [1351], "blocks": True},
        "G": {"ground": "path", "objects": [1362, 1365], "blocks": True},
        "B": {"ground": "grass", "objects": [102], "blocks": True, "light": 4.0},
        "S": {"ground": "grass", "objects": [265]},
    },
    "playerStart": start,
    "spawns": spawns,
    "shrines": [
        {"id": "graveward_shrine", "name": "The Graveward Shrine", "at": shrine1},
        {"id": "barracks_road_shrine", "name": "The Barracks Road Shrine", "at": shrine2},
        {"id": "siege_road_shrine", "name": "The Siege Road Shrine", "at": shrine3},
    ],
    "exits": [
        {"id": "ironhold", "label": "Ironhold", "at": exit_south, "radius": 1.5, "to": "ironhold", "toExit": "north_gate",
         "arrive": [sx, 160]},
        {"id": "barracks", "label": "The Old Barracks", "at": barracks_exit, "radius": 1.2, "to": "old_barracks", "toExit": "stair",
         "arrive": [bx - 1, by]},
        {"id": "siege_walls", "label": "The Siege Walls", "at": exit_north, "radius": 1.5,
         "requires": {"act2.siege_road": "open"},
         "sealedText": "Hadric's banners stand on the broken walls beyond. (The Siege Walls are still being written.)"},
    ],
    "caches": caches,
    "interactables": [
        {"id": "ediths_grave", "name": "Edith Hadric's grave", "at": grave, "tile": 142, "dialogue": "barrow_edith"},
        {"id": "governor_cog", "name": "Brass among the bones", "at": cog, "tile": 1271, "blocks": False, "dialogue": "barrow_cog"},
    ],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
