# Generates zones/hollow_woods.json: Act 3's second zone (levels 12-13), the dead forest on the shelf above Saint
# Oriel's Rest, between the cliff stair and the old catacombs. Deterministic: a fixed LCG.
#
# The way runs north (low y) from the cliff stair (the Rest's north door) at the south end, through the hollow trees:
# the woodcutters' clearing (a shrine), the stone ring where phantoms walk, the mirror pool (the Choir's cultists keep
# it), Caddoc's hermitage (a second shrine; Brother Caddoc waits there once met at the Rest), the hanging grove where
# the stalkers den, and the catacombs' door at the north end (a third shrine). Two burned pages of the Library of Ash
# lie in the woods.
#
# Act 3's second mechanic: things that are not what they seem. Hollow stalkers are unseen until the hero's Perception
# finds them, they strike, or a burst catches them; phantoms are illusions a Will save sees through. The whisperers
# are the crowd-control casters. It is dusk under the trees. The tileset is tileset_oriel.
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import json, math, sys

REVISION = 1
W, H = 70, 130
grid = [['T'] * W for _ in range(H)]
seed = 1618033
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

def path_x(y):
    return 35 + 14 * math.sin(y / 21.0) + 4 * math.sin(y / 7.0 + 1)

def inside(x, y):
    return 2 <= x < W - 2 and 2 <= y < H - 2

def put(x, y, ch):
    if inside(x, y):
        grid[y][x] = ch

def P(y, off=0):
    return [int(round(path_x(y))) + off, y]

# The forest: dense trees, thinning to a walkable band either side of the track.
for y in range(H):
    cx = path_x(y)
    width = 7 + 3 * math.sin(y / 11.0 + 2)
    for x in range(W):
        d = abs(x - cx)
        if not inside(x, y):
            grid[y][x] = 'C'
        elif d <= width:
            r = rnd()
            grid[y][x] = 'T' if (d > 3 and r < 0.06) else ('O' if (d > 3 and r < 0.08) else (',' if r < 0.16 else '.'))
        elif d <= width + 2 and rnd() < 0.35:
            grid[y][x] = ','
        else:
            grid[y][x] = 'T' if rnd() < 0.8 else 'O'

def clear(cx, cy, rx, ry, ch='.'):
    for y in range(cy - ry, cy + ry + 1):
        for x in range(cx - rx, cx + rx + 1):
            if inside(x, y) and ((x - cx) / (rx + 0.5)) ** 2 + ((y - cy) / (ry + 0.5)) ** 2 <= 1:
                grid[y][x] = ',' if (ch == '.' and rnd() < 0.15) else ch

# The track.
for y in range(3, H - 2):
    cx = path_x(y)
    for x in range(W):
        d = abs(x - cx)
        if d <= 0.9:
            grid[y][x] = '='
        elif d <= 1.8 and grid[y][x] in '.,TO':
            grid[y][x] = '-'

# The clearings.
woodcut = P(112, -9)
clear(woodcut[0], woodcut[1], 6, 4)
for (dx, dy) in [(-3, -1), (2, 2), (4, -2)]:
    put(woodcut[0] + dx, woodcut[1] + dy, 'k')         # stumps and the woodcutters' sledge
ring = P(92, 11)
clear(ring[0], ring[1], 7, 6)
for i in range(8):
    a = i * math.pi / 4
    put(ring[0] + int(round(5 * math.cos(a))), ring[1] + int(round(4 * math.sin(a))), 'Z')   # the standing stones
pool = P(72, -12)
clear(pool[0], pool[1], 8, 5)
for y in range(pool[1] - 2, pool[1] + 3):
    for x in range(pool[0] - 4, pool[0] + 5):
        if ((x - pool[0]) / 4.5) ** 2 + ((y - pool[1]) / 2.5) ** 2 <= 1:
            grid[y][x] = 'v'                           # the frozen mirror pool
hermitage = P(52, 10)
clear(hermitage[0], hermitage[1], 7, 5, 'f')
hx, hy = hermitage
for dx in range(-4, 5):
    put(hx + dx, hy - 3, 'W')
for dy in range(-3, 3):
    put(hx - 4, hy + dy, 'W'); put(hx + 4, hy + dy, 'W')
put(hx - 4, hy - 3, 'R'); put(hx + 4, hy - 3, 'R')
put(hx + 2, hy - 2, 'B')                               # the hermitage's brazier
grove = P(30, -10)
clear(grove[0], grove[1], 7, 5)
for (dx, dy) in [(-4, -2), (3, -3), (5, 1), (-2, 3), (1, 0)]:
    put(grove[0] + dx, grove[1] + dy, 'T')             # the hanging trees
door = P(10)
clear(door[0], door[1], 6, 4, 'f')
dx0, dy0 = door[0], 5
for x in range(dx0 - 4, dx0 + 5):
    for y in range(2, dy0):
        grid[y][x] = 'W' if y == dy0 - 1 else 'C'
for x in range(dx0 - 1, dx0 + 2):
    for y in range(2, dy0 + 1):
        grid[y][x] = 'f'
put(dx0 - 2, dy0 - 1, 'R'); put(dx0 + 2, dy0 - 1, 'R')
put(dx0, 2, 'H')                                       # the stair down into the catacombs
for (x, y) in [(dx0 - 3, dy0 + 1), (dx0 + 3, dy0 + 1)]:
    put(x, y, 'B')

shrine1, shrine2, shrine3 = [woodcut[0] + 1, woodcut[1] - 2], [hx - 2, hy], [dx0 - 3, dy0 + 4]
for s in (shrine1, shrine2, shrine3):
    put(*s, 'S')

# The south end: the cliff stair down to the Rest.
start = P(H - 6)
exit_south = [start[0], H - 3]
for y in range(H - 4, H - 1):
    for x in range(exit_south[0] - 1, exit_south[0] + 2):
        grid[y][x] = '='

rows = [''.join(r) for r in grid]

def open_cell(x, y):
    best = None
    for dy in range(-5, 6):
        for dx in range(-5, 6):
            cx_, cy_ = x + dx, y + dy
            if not inside(cx_, cy_) or rows[cy_][cx_] not in '.f':
                continue
            if sum(rows[cy_ + ny][cx_ + nx_] in '.,=-fv' for nx_, ny in ((1, 0), (-1, 0), (0, 1), (0, -1))) < 3:
                continue
            d = dx * dx + dy * dy
            if best is None or d < best[0]:
                best = (d, [cx_, cy_])
    return best[1]
def cache(id, x, y, kind="Cache", facing=0):
    return {"id": id, "at": open_cell(x, y), "kind": kind, "facing": facing}
caches = [
    cache("woodcutters", woodcut[0] - 4, woodcut[1] + 2),
    cache("stone_ring", ring[0], ring[1], kind="Reliquary"),
    cache("mirror_pool", pool[0] + 6, pool[1] - 2, facing=1),
    cache("hermitage", hx + 2, hy + 1, facing=1),
    cache("hanging_grove", grove[0] - 5, grove[1]),
    cache("catacomb_door", dx0 + 4, dy0 + 2, facing=1),
]
pages = [open_cell(ring[0] + 2, ring[1] - 1), open_cell(grove[0] + 3, grove[1] + 2)]
caddoc = open_cell(hx + 1, hy - 1)

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
STALK, PHANT, WHISP, MIRROR, WRAITH = "hollow_stalker", "phantom", "whisperer", "mirror_cultist", "frost_wraith"
spawns = [
    # Below the woodcutters' clearing (level 12)
    pack((STALK, N, 2), at=P(118, 2)),
    pack((MIRROR, N, 2), (WHISP, N, 1), at=[woodcut[0] + 2, woodcut[1] + 1]),
    pack((PHANT, N, 3), (WRAITH, N, 1), at=P(102, -3)),
    # The stone ring: phantoms walk
    pack((PHANT, N, 4), at=[ring[0] - 2, ring[1]], radius=2.5),
    pack((WHISP, N, 1), (PHANT, N, 2), (STALK, N, 1), at=[ring[0] + 2, ring[1] + 1]),
    pack((STALK, N, 3), at=P(84, -3)),
    # The mirror pool: the Choir's cultists
    pack((MIRROR, C, 1, ["mirrored"]), (MIRROR, N, 2), at=[pool[0] + 2, pool[1] + 3]),
    pack((WHISP, N, 2), (PHANT, N, 2), at=[pool[0] - 5, pool[1] - 1]),
    pack((WRAITH, N, 2), (STALK, N, 1), at=P(62, 3)),
    # Past the hermitage (level 13 from here)
    pack((STALK, N, 2), (WHISP, N, 1), at=P(44, -3), level=13),
    pack((MIRROR, N, 3), at=P(38, 3), level=13),
    # The hanging grove: the stalkers' den
    pack((STALK, C, 1, ["hasted"]), (STALK, N, 3), at=[grove[0], grove[1] - 1], level=13),
    pack((PHANT, N, 3), (WHISP, N, 1), at=P(22, 4), level=13),
    # Before the catacombs' door
    pack((MIRROR, C, 1, ["mirrored", "shielded"]), (WHISP, N, 2), at=[dx0, dy0 + 6], level=13),
]
FLAG = [1024, 1025, 1026, 1027, 1028, 1029, 1030, 1031]
ICE = [344, 345, 348, 349, 352, 353, 356, 357]
zone = {
    "id": "hollow_woods", "name": "The Hollow Woods", "level": 12, "act": 3, "revision": REVISION, "xpScale": 0.8,
    "tileset": "tileset_oriel",
    "tagline": "Not everything between the trees is there.",
    "darkness": 0.78, "heroLight": 5.5,
    "rows": rows,
    "legend": {
        ".": {"ground": "grass"},
        ",": {"ground": "grass", "objects": [112, 113, 116, 117, 120, 121, 124, 125]},
        "=": {"ground": "path", "tiles": FLAG},
        "-": {"ground": "path_edge"},
        "f": {"ground": "grass", "tiles": FLAG},
        "v": {"ground": "grass", "tiles": ICE},
        "C": {"ground": "grass", "objects": [48, 49, 52, 53, 56, 60, 64, 68], "blocks": True, "blocksSight": True},
        "T": {"ground": "grass", "objects": [244, 245, 246, 247], "blocks": True, "blocksSight": True},
        "O": {"ground": "grass", "objects": [552, 553, 554, 555, 556, 557], "blocks": True, "cover": True},
        "k": {"ground": "grass", "objects": [535, 538, 540], "blocks": True},
        "Z": {"ground": "grass", "objects": [1291, 1292], "blocks": True, "cover": True},
        "W": {"ground": "grass", "tiles": FLAG, "objects": [1223, 1224, 1225, 1226, 1227, 1228, 1229], "blocks": True, "blocksSight": True},
        "R": {"ground": "grass", "objects": [1185, 1186], "blocks": True, "blocksSight": True},
        "H": {"ground": "path", "tiles": FLAG, "objects": [1346], "blocks": True},
        "B": {"ground": "grass", "tiles": FLAG, "objects": [102], "blocks": True, "light": 4.0},
        "S": {"ground": "grass", "objects": [265]},
    },
    "playerStart": start,
    "spawns": spawns,
    "shrines": [
        {"id": "woodcutters_shrine", "name": "The Woodcutters' Shrine", "at": shrine1},
        {"id": "hermitage_shrine", "name": "The Hermitage Shrine", "at": shrine2},
        {"id": "catacomb_door_shrine", "name": "The Catacomb Door", "at": shrine3},
    ],
    "exits": [
        {"id": "rest", "label": "Saint Oriel's Rest", "at": exit_south, "radius": 1.5, "to": "saint_oriels_rest", "toExit": "north_door",
         "arrive": start},
        {"id": "catacombs", "label": "The Catacombs of Oriel", "at": [dx0, 3], "radius": 1.5, "to": "catacombs_of_oriel",
         "toExit": "woods", "arrive": [dx0, dy0 + 2]},
    ],
    "caches": caches,
    "npcs": [
        {"id": "caddoc", "name": "Brother Caddoc", "title": "Penitent", "at": caddoc, "facing": 3,
         "portrait": "flare/male10", "dialogue": "woods_caddoc", "requires": {"act3.caddoc": "met", "act3.penitent": ""},
         "look": {"head": "leather_hood", "chest": "mage_vest", "hands": "cloth_gloves", "legs": "cloth_pants", "feet": "cloth_sandals",
                  "main": "staff"}},
    ],
    "interactables": [
        {"id": "page_3", "name": "A burned page", "at": pages[0], "tile": 1276, "dialogue": "library_page_3"},
        {"id": "page_4", "name": "A burned page", "at": pages[1], "tile": 1276, "dialogue": "library_page_4"},
    ],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
