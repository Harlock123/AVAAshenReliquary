# Generates zones/ash_wastes.json: Act 4's first zone (levels 16-17), the ash plain between the Ember Gate camp and the
# lava rivers under Mount Cindral. Deterministic: a fixed LCG.
#
# The road runs north (low y) from the camp at the south end: the cairn field (a shrine), the burned village of
# Harrowmere (ash husks among its walls), the antlion pits (Tainted antlions in the soft ash), the Order's last patrol
# (fallen: a shrine and a note), the Inquisitors' muster (a captain drilling zealots), the glass flats where the ash
# melted, and the mountain's foot (a third shrine) where the road drops toward the lava rivers.
#
# Act 4's first mechanic: ash storms. Every so often the ash comes down and nothing more than a few paces off can be
# seen, by the hero or by the monsters. Its AI behaviour: packs a leader drives (each leader's "leads").
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import math, sys
from cinder_common import Rng, legend, open_cell, pack, write, LEADS

REVISION = 1
W, H = 66, 156
rnd = Rng(1604)
grid = [['.'] * W for _ in range(H)]

def path_x(y):
    return 33 + 11 * math.sin(y / 19.0) + 3 * math.sin(y / 7.0)

def inside(x, y):
    return 2 <= x < W - 2 and 2 <= y < H - 2

def put(x, y, ch):
    if inside(x, y):
        grid[y][x] = ch

# The plain: wide, its edges broken by spires; drifts of pebbles and bones.
for y in range(H):
    cx = path_x(y)
    width = 18 + 5 * math.sin(y / 11.0)
    for x in range(W):
        d = abs(x - cx)
        edge = min(x, W - 1 - x, y, H - 1 - y)
        if edge < 2 or d > width + 3:
            grid[y][x] = 'C'
        elif d > width:
            grid[y][x] = 'C' if rnd() < 0.5 else ('o' if rnd() < 0.4 else ',')
        elif d > 4 and rnd() < 0.025:
            grid[y][x] = 'C' if rnd() < 0.5 else 'o'
        elif rnd() < 0.07:
            grid[y][x] = ','

# The road: the Order's old flagstones, half buried.
for y in range(3, H - 2):
    cx = path_x(y)
    for x in range(W):
        d = abs(x - cx)
        if d <= 1.2 and grid[y][x] != 'C':
            grid[y][x] = '=' if rnd() < 0.7 else ','
        elif d <= 1.2:
            grid[y][x] = '='

def P(y, off=0):
    return [int(round(path_x(y))) + off, y]

def clear(x0, y0, x1, y1, ch='.'):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if inside(x, y) and grid[y][x] in 'Co,':
                grid[y][x] = ch

# Harrowmere: the burned village, west of the road.
village = P(118, -12)
clear(village[0] - 7, village[1] - 6, village[0] + 7, village[1] + 6)
for (hx, hy) in [(-5, -4), (2, -4), (-5, 2), (3, 2)]:
    for dx in range(4):
        for dy in range(3):
            if dx in (0, 3) or dy in (0, 2):
                if rnd() < 0.75:
                    put(village[0] + hx + dx, village[1] + hy + dy, 'W')
            else:
                put(village[0] + hx + dx, village[1] + hy + dy, '=')
put(village[0], village[1], 'B')
# The antlion pits, east: soft ash with spires round, and embers where the pits go deep.
pits = P(94, 12)
clear(pits[0] - 7, pits[1] - 6, pits[0] + 7, pits[1] + 6)
for (dx, dy) in [(-3, -2), (3, 1), (-1, 3)]:
    for ex in range(-1, 2):
        for ey in range(-1, 2):
            if abs(ex) + abs(ey) < 2:
                put(pits[0] + dx + ex, pits[1] + dy + ey, 'e')
for (dx, dy) in [(-6, -5), (6, -4), (-6, 5), (5, 5)]:
    put(pits[0] + dx, pits[1] + dy, 'C')
# The fallen patrol: a ring of boulders on the road's west side.
patrol = P(74, -9)
clear(patrol[0] - 4, patrol[1] - 4, patrol[0] + 4, patrol[1] + 4)
for (dx, dy) in [(-3, -2), (3, -2), (-3, 3), (3, 3)]:
    put(patrol[0] + dx, patrol[1] + dy, 'o')
# The Inquisitors' muster: a walled yard east of the road, braziers burning.
muster = P(52, 11)
clear(muster[0] - 8, muster[1] - 6, muster[0] + 8, muster[1] + 6, '=')
for dx in range(-7, 8):
    for dy in (-5, 5):
        if dx not in (-1, 0, 1) or dy == -5:
            put(muster[0] + dx, muster[1] + dy, 'W')
for dy in range(-5, 6):
    put(muster[0] + 7, muster[1] + dy, 'W')
for dy in range(-5, 6):
    if dy not in (-1, 0, 1):
        put(muster[0] - 7, muster[1] + dy, 'W')
for (dx, dy) in [(-4, -3), (4, -3), (-4, 3), (4, 3)]:
    put(muster[0] + dx, muster[1] + dy, 'B')
# The glass flats: the ash run to glass (flagstones' grey) and ember seams through it.
flats = P(32, -6)
clear(flats[0] - 9, flats[1] - 5, flats[0] + 9, flats[1] + 5)
for x in range(flats[0] - 8, flats[0] + 9):
    y = flats[1] + int(round(2 * math.sin(x / 2.5)))
    if abs(x - path_x(y)) > 2:
        put(x, y, 'e')

shrine1, shrine2, shrine3 = P(140, -4), [patrol[0] + 1, patrol[1] + 1], P(12, 4)
for s in (shrine1, shrine2, shrine3):
    put(*s, 'S')
# Braziers the Order kept along the road: lights in the storms.
for y in (132, 104, 84, 64, 42, 22):
    put(*P(y, 3 if y % 2 else -3), 'B')

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
    cache("cairn_field", *P(146, 6)),
    cache("harrowmere", village[0] - 3, village[1] - 3, facing=1),
    cache("antlion_pits", pits[0] + 5, pits[1] - 3),
    cache("fallen_patrol", patrol[0] - 2, patrol[1] + 2, facing=1),
    cache("muster_yard", muster[0] + 5, muster[1] + 1, kind="Reliquary"),
    cache("glass_flats", flats[0] - 7, flats[1] + 3),
]
note = open_cell(rows, patrol[0] - 1, patrol[1] - 1)

N, C, E = "Normal", "Champion", "Elite"
HUSK, WRAITH, ANT, LION, INQ, ZEAL, CAPT = ("ash_husk", "cinder_wraith", "fire_ant", "tainted_antlion", "inquisitor",
                                            "inquisitor_zealot", "inquisitor_captain")
spawns = [
    # The cairn field (level 16)
    pack((HUSK, N, 3), at=P(134, 4)),
    pack((ANT, N, 5), at=P(126, -5)),
    # Harrowmere
    pack((HUSK, C, 1, ["vengeful"], LEADS), (HUSK, N, 3), at=[village[0] - 1, village[1] + 1], radius=2.5),
    pack((WRAITH, N, 2), (HUSK, N, 1), at=[village[0] + 4, village[1] - 3]),
    pack((WRAITH, N, 2), at=P(108, 4)),
    # The antlion pits
    pack((LION, N, 2), (ANT, N, 3), at=[pits[0] - 2, pits[1] + 1], radius=2.5),
    pack((LION, C, 1, ["tainted"], LEADS), (ANT, N, 4), at=[pits[0] + 3, pits[1] - 2]),
    pack((ANT, N, 4), (WRAITH, N, 1), at=P(84, -4)),
    # Past the fallen patrol (level 17 from here)
    pack((INQ, N, 2), (ZEAL, N, 1), at=P(66, 3), level=17),
    # The muster
    pack((CAPT, E, 1, ["shielded"], LEADS), (INQ, N, 2), (ZEAL, N, 1), at=[muster[0], muster[1]], radius=2.5, level=17),
    pack((ZEAL, N, 2), at=[muster[0] + 5, muster[1] + 4], level=17),
    pack((INQ, C, 1, ["hasted"], LEADS), (INQ, N, 2), at=P(46, -4), level=17),
    # The glass flats
    pack((WRAITH, N, 3), at=[flats[0] - 3, flats[1] - 2], level=17),
    pack((LION, C, 1, ["mirrored"], LEADS), (LION, N, 1), (ANT, N, 3), at=[flats[0] + 5, flats[1] + 1], level=17),
    pack((HUSK, N, 3), (WRAITH, N, 1), at=P(24, 4), level=17),
    # The mountain's foot
    pack((CAPT, C, 1, ["vengeful"], LEADS), (INQ, N, 2), (ZEAL, N, 1), at=P(16, -3), level=17),
    pack((ANT, N, 5), at=P(8, 5), level=17),
]
zone = {
    "id": "ash_wastes", "name": "The Ash Wastes", "level": 16, "act": 4, "revision": REVISION, "xpScale": 0.8,
    "tileset": "tileset_cinder", "ashStorms": True,
    "tagline": "Where the Prophet's fire fell, nothing grows but ash.",
    "darkness": 0.7,
    "rows": rows,
    "legend": legend(),
    "playerStart": start,
    "spawns": spawns,
    "shrines": [
        {"id": "cairn_shrine", "name": "The Cairn Field", "at": shrine1},
        {"id": "patrol_shrine", "name": "The Fallen Patrol", "at": shrine2},
        {"id": "mountain_foot_shrine", "name": "The Mountain's Foot", "at": shrine3},
    ],
    "exits": [
        {"id": "camp", "label": "The Ember Gate Camp", "at": exit_south, "radius": 1.5, "to": "ember_gate", "toExit": "wastes",
         "arrive": start},
        {"id": "rivers", "label": "The Lava Rivers", "at": exit_north, "radius": 1.5, "to": "lava_rivers", "toExit": "wastes",
         "arrive": [exit_north[0], 7]},
    ],
    "caches": caches,
    "interactables": [
        {"id": "patrol_note", "name": "A knight's last letter", "at": note, "tile": 1276, "dialogue": "wastes_patrol_note"},
    ],
}
write(zone, sys.argv[1])
