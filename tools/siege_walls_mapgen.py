# Generates zones/siege_walls.json: Act 2's last zone (levels 9-10), the broken northern walls of the old city beyond
# the Barrow Fields, where Ser Hadric the Unburied, Herald of Iron, holds the breach. Deterministic: a fixed LCG.
#
# The way runs north (low y) from the Barrow Fields' siege road at the south end, through the legion's siege works
# (trenches of rubble and barricades, crossbow nests), past the ruined gatehouse to the west (Red Mirella's hideout: the
# Outlaw Queen, Act 2's faction story), to the breach in the city's wall, and up onto the wall-top: a paved plaza where
# Hadric waits (his arena, radius 11). Beyond him the Herald gate opens the road north to Saint Oriel's Rest (Act 3)
# once he falls (through the Frostbitten Pass, M4 step 6).
#
# Act 2's mechanics come together here: cover and kiting crossbowmen in the siege works, the rising dead in the mass
# graves, and Hadric's three phases on the boss framework (a shield wall and charges, a rallying standard, his armour
# cast off). The tileset is tileset_ironhold (ruins tile n is n + 1000).
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import json, math, sys

REVISION = 1
W, H = 64, 132
grid = [['.'] * W for _ in range(H)]
seed = 1357911
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

def path_x(y):
    return 32 + 6 * math.sin(y / 15.0)

for y in range(H):
    cx = path_x(y)
    for x in range(W):
        d = abs(x - cx)
        edge = min(x, W - 1 - x, y, H - 1 - y)
        if edge < 2:
            grid[y][x] = 'T'
        elif d > 9:
            p = 0.03 + 0.05 * max(0, d - 9)
            if rnd() < min(p, 0.5):
                grid[y][x] = 'D' if rnd() < 0.4 else ('T' if rnd() < 0.6 else 'O')
            elif rnd() < 0.1:
                grid[y][x] = ','
        elif rnd() < 0.05:
            grid[y][x] = ','

for y in range(46, H - 2):
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

def clear(x0, y0, x1, y1, ch='.'):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if inside(x, y) and grid[y][x] not in '=-':
                grid[y][x] = ',' if ch == '.' and rnd() < 0.08 else ch

P = lambda y, dx=0: [int(path_x(y)) + dx, y]

# --- The siege road (the south end): the way in from the Barrow Fields ---
sx = int(path_x(124))
clear(sx - 6, 116, sx + 6, 128)
for y in range(124, H - 2):
    for x in range(sx - 1, sx + 2):
        grid[y][x] = '='
start = [sx, 124]
exit_south = [sx, 128]
shrine1 = [sx + 4, 119]
put(*shrine1, 'S')

# --- The siege works: trenches of rubble and barricades across the way, crossbow nests behind them ---
for y in range(80, 116):
    cx = int(path_x(y))
    for x in range(cx - 18, cx + 19):
        if inside(x, y) and grid[y][x] in 'TOD' and rnd() < 0.75:
            grid[y][x] = ',' if rnd() < 0.12 else '.'
for y in (110, 100, 90):
    cx = int(path_x(y))
    g = int(rnd() * 4) - 2
    for x in range(cx - 16, cx + 17):
        if not inside(x, y) or grid[y][x] in '=-' or (cx - 11 + g <= x <= cx - 9 + g) or (cx + 9 + g <= x <= cx + 11 + g):
            continue
        grid[y][x] = 'k' if rnd() < 0.35 else 'u'
# The mass graves of the siege's dead, where the bodies rise.
for (gx, gy) in [(int(path_x(105)) + 12, 105), (int(path_x(95)) - 13, 95)]:
    for y in (gy - 2, gy, gy + 2):                     # rows of graves with lanes between (a checkerboard walled cells in)
        for x in range(gx - 3, gx + 4, 2):
            if inside(x, y) and grid[y][x] not in '=-':
                put(x, y, '+')

# --- The ruined gatehouse (west): Red Mirella's hideout ---
hx, hy = 10, 72
clear(hx - 6, hy - 6, hx + 7, hy + 6)
for x in range(hx - 4, hx + 5):
    put(x, hy - 4, 'W'); put(x, hy + 4, 'W')
for y in range(hy - 4, hy + 5):
    put(hx - 4, y, 'W'); put(hx + 4, y, 'W')
for y in (hy - 1, hy, hy + 1):
    put(hx + 4, y, '.')
put(hx + 4, hy - 2, 'Q'); put(hx + 4, hy + 2, 'Q')
put(hx - 2, hy - 2, 'c')                               # the outlaws' fire
for (x, y) in [(hx - 3, hy + 3), (hx + 2, hy + 3)]:
    put(x, y, 'k')
for x in range(hx + 5, int(path_x(hy)) - 1):           # a track from the road to the gatehouse door
    for y in (hy, hy + 1):
        if grid[y][x] not in '=-':
            grid[y][x] = '.'
mirella = [hx, hy]

# --- The breach: the city's broken wall across the way, rubble in the gap; a shrine before it ---
shrine2 = P(64, 4)
clear(shrine2[0] - 1, shrine2[1] - 1, shrine2[0] + 1, shrine2[1] + 1)
put(*shrine2, 'S')
wy = 52
for x in range(2, W - 2):
    grid[wy][x] = 'W'
    grid[wy + 1][x] = 'W' if rnd() < 0.5 else grid[wy + 1][x]
bx = int(path_x(wy))
for x in range(bx - 3, bx + 4):
    grid[wy][x] = '='
    if grid[wy + 1][x] == 'W':
        grid[wy + 1][x] = '='
for x in (bx - 4, bx + 4):
    grid[wy][x] = 'R'
for (dx, dy) in [(-6, 2), (6, 1), (-8, -2), (7, -3)]:
    put(bx + dx, wy + dy, 'u')

# --- The wall-top: Hadric's arena, a paved plaza ringed by broken merlons ---
ax, ay, ar = bx, 26, 11
for y in range(4, wy):
    for x in range(2, W - 2):
        d = math.hypot(x - ax, y - ay)
        if d <= ar + 0.5:
            grid[y][x] = 'f' if rnd() < 0.85 else 'g'
        elif y >= 38 and abs(x - bx) <= 2:
            grid[y][x] = 'f'                              # the ramp up from the breach
        elif d <= ar + 1.5:
            grid[y][x] = 'R' if rnd() < 0.55 else 'u'     # broken merlons round the plaza
        else:
            grid[y][x] = 'T' if rnd() < 0.6 else 'O'
for (dx, dy) in [(-6, -6), (6, -6), (-6, 6), (6, 6)]:
    put(ax + dx, ay + dy, 'I')                         # standing pillars of the old wall-walk (cover from his charge)
shrine3 = [bx + 2, 44]
put(*shrine3, 'S')
# The Herald gate north, behind him.
for y in range(3, ay - ar):
    for x in range(ax - 1, ax + 2):
        grid[y][x] = 'f'
for x in range(W):
    grid[2][x] = 'T'
    if grid[3][x] != 'f':
        grid[3][x] = 'T'
grid[4][ax - 2] = 'Q'; grid[4][ax + 2] = 'Q'
exit_north = [ax, 5]
for (dx, dy) in [(-9, 0), (9, 0)]:
    put(ax + dx, ay + dy, 'B')

rows = [''.join(r) for r in grid]

def open_cell(x, y):
    best = None
    for dy in range(-4, 5):
        for dx in range(-4, 5):
            cx_, cy_ = x + dx, y + dy
            if not inside(cx_, cy_) or rows[cy_][cx_] not in '.f':
                continue
            if not any(rows[cy_ + ny][cx_ + nx_] in '.,=-fg' for nx_, ny in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                continue
            d = dx * dx + dy * dy
            if best is None or d < best[0]:
                best = (d, [cx_, cy_])
    return best[1]
def cache(id, x, y, kind="Cache", facing=0):
    return {"id": id, "at": open_cell(x, y), "kind": kind, "facing": facing}
caches = [
    cache("siege_road", sx - 5, 120),
    cache("first_trench", int(path_x(112)) + 14, 112, facing=1),
    cache("mass_grave", int(path_x(95)) - 15, 98),
    cache("gatehouse", hx + 2, hy - 2, facing=1),
    cache("nest", int(path_x(86)) + 13, 86, facing=1),
    cache("before_the_breach", bx - 8, 58),
    cache("hadrics_hoard", ax + 9, ay - 5, kind="Reliquary", facing=1),
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
N, C, B = "Normal", "Champion", "Herald"
LEG, XB, BLEG, DEAD, HOUND, WIGHT, KNIGHT = ("legionary", "legion_crossbowman", "barrow_legionary", "barrow_dead", "grave_hound",
                                             "barrow_wight", "barrow_knight")
spawns = [
    # The siege works (level 9)
    pack((LEG, N, 3), at=P(114, -3)),
    pack((XB, N, 2), at=P(112, 8), radius=1.5),
    pack((XB, N, 2), (LEG, N, 1), at=P(102, -7)),
    pack((DEAD, N, 3), (WIGHT, N, 1), at=[int(path_x(105)) + 12, 107]),
    pack((LEG, C, 1, ["shielded"]), (LEG, N, 2), at=P(97, 3)),
    pack((DEAD, N, 3), at=[int(path_x(95)) - 13, 93]),
    pack((XB, N, 3), at=P(92, 9), radius=1.5),
    pack((HOUND, N, 3), at=P(86, -5)),
    pack((BLEG, N, 2), (XB, N, 1), at=P(80, 4)),
    # Near the gatehouse (the outlaws keep their own door; the dead keep the road)
    pack((HOUND, N, 2), (BLEG, N, 1), at=P(74, -4)),
    # Before the breach (level 10 from here)
    pack((KNIGHT, N, 1), (BLEG, N, 2), at=P(68, -3), level=10),
    pack((XB, C, 1, ["hasted"]), (XB, N, 2), at=P(60, 7), radius=1.5, level=10),
    pack((WIGHT, N, 2), (DEAD, N, 2), at=P(57, -6), level=10),
    # The ramp up onto the wall
    pack((KNIGHT, C, 1, ["vengeful"]), (BLEG, N, 2), at=[bx, 47], level=10),
    pack((XB, N, 2), at=[bx + 3, 41], radius=1.2, level=10),
    # Ser Hadric the Unburied
    pack(("ser_hadric", B, 1), at=[ax, ay], radius=0, level=9),              # a level under the hero who meets him, as Sallow is
]
FLAG = [1024, 1025, 1026, 1027, 1028, 1029, 1030, 1031]
WORN = [1074, 1075, 1076, 1077, 1078]
zone = {
    "id": "siege_walls", "name": "The Siege Walls", "level": 9, "act": 2, "revision": REVISION, "xpScale": 0.9, "tileset": "tileset_ironhold",
    "tagline": "The Iron Legion holds the breach it died for.",
    "darkness": 0.72,
    "rows": rows,
    "legend": {
        ".": {"ground": "grass"},
        ",": {"ground": "grass", "objects": [112, 113, 114, 115, 116, 117, 118, 120, 121, 122, 123, 124, 125, 126, 127]},
        "=": {"ground": "path", "tiles": FLAG},
        "-": {"ground": "path_edge"},
        "f": {"ground": "grass", "tiles": FLAG},
        "g": {"ground": "grass", "tiles": WORN},
        "T": {"ground": "grass", "objects": [248, 249, 250, 251], "blocks": True, "blocksSight": True},
        "O": {"ground": "grass", "objects": [252, 253, 254, 255], "blocks": True, "blocksSight": True},
        "D": {"ground": "grass", "objects": [244, 245, 246, 247], "blocks": True},
        "+": {"ground": "grass", "objects": [140, 141, 142, 143], "blocks": True},
        "u": {"ground": "grass", "objects": [1187, 1189, 1190, 1192, 1193, 1194], "blocks": True, "cover": True},
        "k": {"ground": "grass", "objects": [1285, 1286, 1287, 1288], "blocks": True, "cover": True},
        "W": {"ground": "grass", "objects": [1223, 1224, 1225, 1226, 1227, 1228, 1229], "blocks": True, "blocksSight": True},
        "R": {"ground": "grass", "objects": [1230, 1231, 1232, 1233], "blocks": True, "blocksSight": True},
        "Q": {"ground": "grass", "objects": [1230], "blocks": True, "blocksSight": True},
        "I": {"ground": "grass", "tiles": FLAG, "objects": [1223, 1226, 1228], "blocks": True, "blocksSight": True},
        "c": {"ground": "grass", "objects": [102], "blocks": True},
        "B": {"ground": "grass", "tiles": FLAG, "objects": [102], "blocks": True, "light": 4.0},
        "S": {"ground": "grass", "objects": [265]},
    },
    "playerStart": start,
    "spawns": spawns,
    "shrines": [
        {"id": "siege_works_shrine", "name": "The Siege Works Shrine", "at": shrine1},
        {"id": "breach_shrine", "name": "The Breach Shrine", "at": shrine2},
        {"id": "wall_walk_shrine", "name": "The Wall-Walk Shrine", "at": shrine3},
    ],
    "exits": [
        {"id": "barrow_fields", "label": "The Barrow Fields", "at": exit_south, "radius": 1.5, "to": "barrow_fields",
         "toExit": "siege_walls", "arrive": [sx, 123]},
        {"id": "heights", "label": "The Frostbitten Pass", "at": exit_north, "radius": 1.5,
         "requires": {"act2.hadric": "slain"},
         "sealedText": "The Iron Legion still holds the north gate. It will not open while Ser Hadric stands.",
         "to": "frostbitten_pass", "toExit": "siege_walls", "arrive": [ax, ay - ar + 2]},
    ],
    "caches": caches,
    # The Outlaw Queen (Act 2's faction story): Red Mirella in the ruined gatehouse, until her choice is made.
    "npcs": [
        {"id": "mirella", "name": "Red Mirella", "title": "The Outlaw Queen", "at": mirella, "facing": 2,
         "portrait": "flare/female04", "dialogue": "siege_mirella", "requires": {"act2.faction": ""},
         "look": {"head": "leather_hood", "chest": "leather_chest", "hands": "leather_gloves", "legs": "leather_pants",
                  "feet": "leather_boots", "main": "longbow"}},
    ],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
