# Generates zones/lava_rivers.json: Act 4's second zone (levels 17-18), where Mount Cindral's fire runs down to the ash
# in three rivers. Deterministic: a fixed LCG.
#
# The road climbs north (low y) from the Ash Wastes: the first river (the Order's bridge still stands; fire ants in the
# shallows), the slag fields (an Inquisitor captain and his zealots), the second river (its bridge burned: a ford of
# embers, or the long way round to a crossing upstream), the drakes' ledges (cinder-drakes nesting on warm rock), the old
# foundry of the Order (ruined walls round a cooling pour), and the third river below the mountain's gate, where the road
# goes on up to the Wyrm's Nest.
#
# Act 4's second mechanic: hazardous ground. Lava can't be crossed but by a bridge; embers can, and burn (a Fortitude
# save each second against fire; fire resistance counts). Lava glows: some of its cells are lights.
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import math, sys
from cinder_common import Rng, legend, open_cell, pack, write, LEADS

REVISION = 1
W, H = 70, 150
rnd = Rng(17320)
grid = [['.'] * W for _ in range(H)]

def path_x(y):
    return 35 + 10 * math.sin(y / 21.0) + 3 * math.sin(y / 8.0)

def inside(x, y):
    return 2 <= x < W - 2 and 2 <= y < H - 2

def put(x, y, ch):
    if inside(x, y):
        grid[y][x] = ch

for y in range(H):
    cx = path_x(y)
    width = 20 + 5 * math.sin(y / 13.0)
    for x in range(W):
        d = abs(x - cx)
        edge = min(x, W - 1 - x, y, H - 1 - y)
        if edge < 2 or d > width + 3:
            grid[y][x] = 'C'
        elif d > width:
            grid[y][x] = 'C' if rnd() < 0.5 else ('o' if rnd() < 0.4 else ',')
        elif d > 4 and rnd() < 0.025:
            grid[y][x] = 'C' if rnd() < 0.6 else 'o'
        elif rnd() < 0.08:
            grid[y][x] = ','

def river(y_mid, amp, width, phase):
    """A river of lava from edge to edge across the zone, winding; returns its middle row at each x."""
    mids = {}
    for x in range(W):
        mid = y_mid + amp * math.sin(x / 7.0 + phase) + 1.5 * math.sin(x / 3.1 + phase * 2)
        mids[x] = mid
        for y in range(int(mid - width / 2), int(mid + width / 2) + 1):
            if 2 <= x < W - 2 and 1 <= y < H - 1:
                grid[y][x] = '~'
    return mids

rivers = [river(122, 3, 5, 0.3), river(80, 4, 6, 1.7), river(30, 3, 5, 3.1)]

# The road, over everything but the rivers.
for y in range(3, H - 2):
    cx = path_x(y)
    for x in range(W):
        if abs(x - cx) <= 1.2 and grid[y][x] != '~':
            grid[y][x] = '=' if rnd() < 0.75 else ','

def P(y, off=0):
    return [int(round(path_x(y))) + off, y]

def bridge(r, x):
    """A walkway across a river at column x, three wide, banks to banks."""
    for dx in (-1, 0, 1):
        for y in range(H):
            if grid[y][x + dx] == '~' and abs(y - r[x]) < 6:
                grid[y][x + dx] = '#'

def ford(r, x, width=3):
    """A crust of embers across a river: walkable, and it burns."""
    for dx in range(-(width // 2), width // 2 + 1):
        for y in range(H):
            if grid[y][x + dx] == '~' and abs(y - r[x]) < 6:
                grid[y][x + dx] = 'e'

# The first river: the Order's bridge on the road.
bridge(rivers[0], int(round(path_x(122))))
ford(rivers[0], int(round(path_x(122))) - 14)
# The second: the bridge burned (embers on the road), a stone crossing far upstream to the east.
ford(rivers[1], int(round(path_x(80))))
upstream = int(round(path_x(80))) + 14
bridge(rivers[1], upstream)
# The third: the bridge to the mountain, and a ford west.
bridge(rivers[2], int(round(path_x(30))))
ford(rivers[2], int(round(path_x(30))) - 15, 2)

def clear(x0, y0, x1, y1, ch='.'):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if inside(x, y) and grid[y][x] in 'Co,':
                grid[y][x] = ch

# The slag fields: a lava pool among ember crust, east of the road.
slag = P(104, 12)
clear(slag[0] - 7, slag[1] - 6, slag[0] + 7, slag[1] + 6)
for y in range(slag[1] - 3, slag[1] + 4):
    for x in range(slag[0] - 4, slag[0] + 5):
        r = ((x - slag[0]) / 4.0) ** 2 + ((y - slag[1]) / 3.0) ** 2
        if r <= 0.55:
            grid[y][x] = '~'
        elif r <= 1.1 and rnd() < 0.7:
            grid[y][x] = 'e'
# The drakes' ledges: warm rock shelves west of the road, embers between.
ledges = P(62, -13)
clear(ledges[0] - 7, ledges[1] - 7, ledges[0] + 7, ledges[1] + 7)
for (dx, dy) in [(-4, -4), (3, -5), (-5, 3), (4, 4), (0, 0)]:
    put(ledges[0] + dx, ledges[1] + dy, 'o')
for (dx, dy) in [(-2, -2), (2, 1), (-1, 3), (2, -3)]:
    put(ledges[0] + dx, ledges[1] + dy, 'e')
# The Order's old foundry: ruined walls round a cooling pour, east of the road.
foundry = P(46, 12)
clear(foundry[0] - 8, foundry[1] - 6, foundry[0] + 8, foundry[1] + 6, '=')
for dx in range(-7, 8):
    for dy in (-5, 5):
        if dx not in (-1, 0, 1) and rnd() < 0.85:
            put(foundry[0] + dx, foundry[1] + dy, 'W')
for dy in range(-5, 6):
    if dy not in (-1, 0, 1):
        put(foundry[0] - 7, foundry[1] + dy, 'W')
        put(foundry[0] + 7, foundry[1] + dy, 'W')
for y in range(foundry[1] - 1, foundry[1] + 2):
    for x in range(foundry[0] - 2, foundry[0] + 3):
        grid[y][x] = '~'
for (dx, dy) in [(-5, -3), (5, -3), (-5, 3), (5, 3)]:
    put(foundry[0] + dx, foundry[1] + dy, 'R')

# Some lava glows (lights, a few to a river).
for r in rivers:
    for x in range(4, W - 4, 7):
        y = int(round(r[x]))
        if grid[y][x] == '~':
            grid[y][x] = '!'
for (x, y) in [slag, [foundry[0], foundry[1]]]:
    grid[y][x] = '!'

shrine1, shrine2, shrine3 = P(138, -4), P(92, 4), P(14, -4)
for s in (shrine1, shrine2, shrine3):
    put(*s, 'S')

start = P(H - 6)
exit_south = [start[0], H - 3]
for y in range(H - 5, H - 1):
    for x in range(exit_south[0] - 1, exit_south[0] + 2):
        grid[y][x] = '='
exit_north = [int(round(path_x(4))), 3]
for y in range(1, 6):
    for x in range(exit_north[0] - 1, exit_north[0] + 2):
        grid[y][x] = '='

rows = [''.join(r) for r in grid]
def cache(id, x, y, kind="Cache", facing=0):
    return {"id": id, "at": open_cell(rows, x, y), "kind": kind, "facing": facing}
caches = [
    cache("first_bank", *P(128, -8)),
    cache("slag_fields", slag[0] + 6, slag[1] + 4, facing=1),
    cache("upstream_crossing", upstream, int(rivers[1][upstream]) - 6),
    cache("drakes_ledges", ledges[0] - 5, ledges[1] - 1, kind="Reliquary"),
    cache("foundry", foundry[0] + 4, foundry[1] - 3, facing=1),
    cache("third_bank", *P(22, 7)),
]
note = open_cell(rows, foundry[0] - 4, foundry[1] + 2)

N, C, E = "Normal", "Champion", "Elite"
HUSK, WRAITH, ANT, LION, INQ, ZEAL, CAPT, DRAKE = ("ash_husk", "cinder_wraith", "fire_ant", "tainted_antlion", "inquisitor",
                                                   "inquisitor_zealot", "inquisitor_captain", "cinder_drake")
spawns = [
    # Before the first river (level 17)
    pack((ANT, N, 5), at=P(134, 4)),
    pack((ANT, C, 1, ["hasted"], LEADS), (ANT, N, 4), at=P(116, -3)),
    # The slag fields
    pack((CAPT, C, 1, ["shielded"], LEADS), (ZEAL, N, 2), (INQ, N, 1), at=[slag[0] - 5, slag[1] - 4]),
    pack((WRAITH, N, 3), at=[slag[0] + 5, slag[1] + 2]),
    pack((HUSK, N, 3), (WRAITH, N, 1), at=P(96, -4)),
    # The second river's ford (level 18 from here)
    pack((DRAKE, N, 2), at=P(72, 4), level=18),
    # The drakes' ledges
    pack((DRAKE, C, 1, ["vengeful"], LEADS), (DRAKE, N, 2), at=[ledges[0], ledges[1] - 2], radius=2.5, level=18),
    pack((DRAKE, N, 1), (ANT, N, 4), at=[ledges[0] + 3, ledges[1] + 3], level=18),
    pack((LION, N, 2), (ANT, N, 2), at=P(58, 4), level=18),
    # The foundry
    pack((CAPT, E, 1, ["tainted"], LEADS), (INQ, N, 2), (ZEAL, N, 1), at=[foundry[0], foundry[1] + 3], radius=2.5, level=18),
    pack((ZEAL, N, 2), (INQ, N, 1), at=[foundry[0] - 4, foundry[1] - 3], level=18),
    # The third river and the climb
    pack((WRAITH, C, 1, ["mirrored"], LEADS), (WRAITH, N, 2), at=P(38, -4), level=18),
    pack((DRAKE, N, 2), (ANT, N, 2), at=P(22, 4), level=18),
    pack((INQ, C, 1, ["hasted"], LEADS), (INQ, N, 2), (ZEAL, N, 1), at=P(10, 3), level=18),
]
zone = {
    "id": "lava_rivers", "name": "The Lava Rivers", "level": 17, "act": 4, "revision": REVISION, "xpScale": 0.8,
    "tileset": "tileset_cinder",
    "tagline": "The mountain bleeds fire, and the fire runs to the sea.",
    "darkness": 0.72,
    "rows": rows,
    "legend": legend({"!": {"ground": "lava", "light": 3.0, "lightColor": "#ff8a3a"}}),
    "playerStart": start,
    "spawns": spawns,
    "shrines": [
        {"id": "rivers_shrine", "name": "The First Bank", "at": shrine1},
        {"id": "ford_shrine", "name": "The Burned Bridge", "at": shrine2},
        {"id": "gate_road_shrine", "name": "The Gate Road", "at": shrine3},
    ],
    "exits": [
        {"id": "wastes", "label": "The Ash Wastes", "at": exit_south, "radius": 1.5, "to": "ash_wastes", "toExit": "rivers",
         "arrive": start},
        {"id": "nest", "label": "The Wyrm's Nest", "at": exit_north, "radius": 1.5, "to": "wyrms_nest", "toExit": "rivers",
         "arrive": [exit_north[0], 7]},
    ],
    "caches": caches,
    "interactables": [
        {"id": "foundry_ledger", "name": "The foundry's ledger", "at": note, "tile": 1276, "dialogue": "rivers_foundry_ledger"},
    ],
}
write(zone, sys.argv[1])
