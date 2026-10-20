# Generates zones/frostbitten_pass.json: Act 3's first zone (levels 11-12), the pass up the frozen cliffs from the Siege
# Walls' north gate to Saint Oriel's Rest. Deterministic: a fixed LCG.
#
# The way climbs north (low y) from the Siege Walls at the south end: the foot of the pass (a shrine, the first
# campfire), the ice-falls where rime crawlers nest, the Horned Stair (a ruined waystation of the Order, the minotaurs'
# lair), the frozen tarn (frost wraiths drift over the ice), the hermit's ledge (a second shrine), the high saddle where
# the Choir's mirror cultists watch the road, and the last climb to the Rest's south road (a third shrine). Two burned
# pages of the Library of Ash lie in the pass.
#
# Act 3's first mechanic: cold. Away from a fire the hero's exposure rises; blizzards blow every so often and triple
# it. Campfires (lights without a colour) warm, and they stand along the road, about one a fight apart. The tileset is
# tileset_oriel: Flare's snowplains with rubberduck's Old Ruins (ruins tile n is n + 1000).
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import json, math, sys

REVISION = 1
W, H = 60, 150
grid = [['.'] * W for _ in range(H)]
seed = 2718281
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

def path_x(y):
    return 30 + 9 * math.sin(y / 17.0) + 3 * math.sin(y / 6.5)

def inside(x, y):
    return 2 <= x < W - 2 and 2 <= y < H - 2

def put(x, y, ch):
    if inside(x, y):
        grid[y][x] = ch

# The cliffs: the pass is a gully, narrow in places; rocks and dead trees on its flanks.
for y in range(H):
    cx = path_x(y)
    width = 11 + 4 * math.sin(y / 9.0)
    for x in range(W):
        d = abs(x - cx)
        edge = min(x, W - 1 - x, y, H - 1 - y)
        if edge < 2 or d > width + 3:
            grid[y][x] = 'C'
        elif d > width:
            grid[y][x] = 'C' if rnd() < 0.55 else ('T' if rnd() < 0.5 else 'O')
        elif d > 4 and rnd() < 0.04:
            grid[y][x] = 'T' if rnd() < 0.5 else 'O'
        elif rnd() < 0.06:
            grid[y][x] = ','

# The road.
for y in range(3, H - 2):
    cx = path_x(y)
    for x in range(W):
        d = abs(x - cx)
        if d <= 1.2:
            grid[y][x] = '='
        elif d <= 2.1 and grid[y][x] in '.,TO':
            grid[y][x] = '-' if rnd() < 0.7 else '.'

def P(y, off=0):
    return [int(round(path_x(y))) + off, y]

def clear(x0, y0, x1, y1, ch='.'):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if inside(x, y) and grid[y][x] in 'CTO,':
                grid[y][x] = ch

# Clearings off the road: the ice-falls (east), the Horned Stair (west), the tarn (east), the hermit's ledge (west).
falls = P(112, 11)
clear(falls[0] - 5, falls[1] - 5, falls[0] + 5, falls[1] + 5)
for (dx, dy) in [(-3, -3), (2, -4), (4, 1), (-4, 3), (0, 4)]:
    put(falls[0] + dx, falls[1] + dy, 'i')             # ice spurs
stair = P(88, -12)
clear(stair[0] - 6, stair[1] - 5, stair[0] + 6, stair[1] + 5, 'f')
for (dx, dy) in [(-6, -5), (6, -5), (-6, 5), (6, 5)]:
    put(stair[0] + dx, stair[1] + dy, 'R')
for dx in range(-6, 7):
    if dx not in (-1, 0, 1):
        put(stair[0] + dx, stair[1] - 5, 'W')
for dy in range(-4, 5):
    put(stair[0] - 6, stair[1] + dy, 'W')
for (dx, dy) in [(-3, -2), (3, -2), (-3, 2), (3, 2)]:
    put(stair[0] + dx, stair[1] + dy, 'I')             # the waystation's broken piers (cover)
tarn = P(66, 10)
clear(tarn[0] - 7, tarn[1] - 5, tarn[0] + 7, tarn[1] + 5)
for y in range(tarn[1] - 3, tarn[1] + 4):
    for x in range(tarn[0] - 5, tarn[0] + 6):
        if ((x - tarn[0]) / 5.5) ** 2 + ((y - tarn[1]) / 3.5) ** 2 <= 1:
            grid[y][x] = 'v'                           # the frozen tarn (walkable ice)
ledge = P(46, -11)
clear(ledge[0] - 5, ledge[1] - 4, ledge[0] + 4, ledge[1] + 4)
saddle = P(26, 0)
clear(saddle[0] - 9, saddle[1] - 4, saddle[0] + 9, saddle[1] + 4)
for (dx, dy) in [(-7, -2), (7, -2), (-7, 3), (7, 3)]:
    put(saddle[0] + dx, saddle[1] + dy, 'u')           # cairns of the Choir's watchers (cover)

# Campfires along the road (warmth), shrines, the pilgrims' cairns.
fires = [P(136, 4), P(118, -4), P(100, 4), P(84, 5), P(70, -4), P(52, 4), P(36, -4), P(16, 4)]
for f in fires:
    put(*f, 'c')
put(stair[0] + 1, stair[1] + 1, 'c')
put(ledge[0] + 1, ledge[1] - 1, 'c')
shrine1, shrine2, shrine3 = P(132, -3), [ledge[0] - 2, ledge[1] + 1], P(10, -3)
for s in (shrine1, shrine2, shrine3):
    put(*s, 'S')

# The ends: the Siege Walls to the south, the Rest's south road to the north.
start = P(H - 6)
exit_south = [start[0], H - 3]
for y in range(H - 4, H - 1):
    for x in range(exit_south[0] - 1, exit_south[0] + 2):
        grid[y][x] = '='
exit_north = [int(round(path_x(4))), 3]
for y in range(1, 5):
    for x in range(exit_north[0] - 1, exit_north[0] + 2):
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
    cache("foot_of_the_pass", *P(140, -6)),
    cache("ice_falls", falls[0] + 3, falls[1] - 2, facing=1),
    cache("horned_stair", stair[0] - 4, stair[1] + 3, kind="Reliquary"),
    cache("tarn_shore", tarn[0] + 6, tarn[1] + 4, facing=1),
    cache("hermits_ledge", ledge[0] - 3, ledge[1] - 2),
    cache("saddle", saddle[0] + 8, saddle[1] - 1, facing=1),
]
pages = [open_cell(stair[0] + 3, stair[1] - 3), open_cell(ledge[0] + 2, ledge[1] + 2)]
hermit = open_cell(ledge[0] - 1, ledge[1] - 2)

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
ANT, WRAITH, HEX, HORN, MIRROR = "rime_crawler", "frost_wraith", "frost_hexer", "horned_guardian", "mirror_cultist"
spawns = [
    # The foot of the pass (level 11)
    pack((ANT, N, 4), at=P(128, 3)),
    pack((WRAITH, N, 2), at=P(122, -6)),
    # The ice-falls: the crawlers' nest
    pack((ANT, N, 4), at=[falls[0] - 1, falls[1]]),
    pack((ANT, C, 1, ["hasted"]), (ANT, N, 3), at=[falls[0] + 2, falls[1] - 3], radius=1.5),
    pack((HEX, N, 1), (WRAITH, N, 2), at=P(106, 2)),
    # The Horned Stair
    pack((HORN, N, 1), (ANT, N, 2), at=P(95, -3)),
    pack((HORN, N, 2), at=[stair[0] + 2, stair[1]], radius=1.5),
    pack((HORN, C, 1, ["vengeful"]), (HEX, N, 1), at=[stair[0] - 2, stair[1] - 2], radius=1.2),
    pack((WRAITH, N, 2), (HEX, N, 1), at=P(78, 3)),
    # The frozen tarn (level 12 from here)
    pack((WRAITH, N, 3), at=[tarn[0] - 2, tarn[1]], level=12),
    pack((WRAITH, C, 1, ["shielded"]), (WRAITH, N, 2), at=[tarn[0] + 3, tarn[1] + 1], level=12),
    pack((ANT, N, 3), (HEX, N, 1), at=P(58, -3), level=12),
    # Past the hermit's ledge
    pack((HORN, N, 1), (WRAITH, N, 2), at=P(42, 3), level=12),
    pack((HEX, N, 2), (ANT, N, 2), at=P(34, -4), level=12),
    # The high saddle: the Choir's watchers
    pack((MIRROR, C, 1, ["mirrored"]), (MIRROR, N, 2), at=[saddle[0] - 3, saddle[1]], level=12),
    pack((MIRROR, N, 2), (HEX, N, 1), at=[saddle[0] + 4, saddle[1] + 1], level=12),
    pack((HORN, C, 1, ["mirrored", "hasted"]), (ANT, N, 2), at=P(18, -3), level=12),
]
FLAG = [1024, 1025, 1026, 1027, 1028, 1029, 1030, 1031]
ICE = [344, 345, 348, 349, 352, 353, 356, 357]
zone = {
    "id": "frostbitten_pass", "name": "The Frostbitten Pass", "level": 11, "act": 3, "revision": REVISION, "xpScale": 0.8,
    "tileset": "tileset_oriel", "cold": True,
    "tagline": "The road up the mountain freezes the faithless first.",
    "darkness": 0.6,
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
        "i": {"ground": "grass", "objects": [440, 444], "blocks": True, "blocksSight": True},
        "W": {"ground": "grass", "tiles": FLAG, "objects": [1223, 1224, 1225, 1226, 1227, 1228, 1229], "blocks": True, "blocksSight": True},
        "R": {"ground": "grass", "objects": [1185, 1186], "blocks": True, "blocksSight": True},
        "I": {"ground": "grass", "tiles": FLAG, "objects": [1223, 1226, 1228], "blocks": True, "cover": True},
        "u": {"ground": "grass", "objects": [1187, 1189, 1190, 1192, 1193, 1194], "blocks": True, "cover": True},
        "c": {"ground": "grass", "objects": [102], "blocks": True, "light": 3.5},
        "S": {"ground": "grass", "objects": [265]},
    },
    "playerStart": start,
    "spawns": spawns,
    "shrines": [
        {"id": "pass_foot_shrine", "name": "The Foot of the Pass", "at": shrine1},
        {"id": "hermits_shrine", "name": "The Hermit's Shrine", "at": shrine2},
        {"id": "high_road_shrine", "name": "The High Road Shrine", "at": shrine3},
    ],
    "exits": [
        {"id": "siege_walls", "label": "The Siege Walls", "at": exit_south, "radius": 1.5, "to": "siege_walls", "toExit": "heights",
         "arrive": start},
        {"id": "rest", "label": "Saint Oriel's Rest", "at": exit_north, "radius": 1.5, "to": "saint_oriels_rest", "toExit": "south_road",
         "arrive": [exit_north[0], 7]},
    ],
    "caches": caches,
    "npcs": [
        {"id": "hermit", "name": "Old Teodor", "title": "Hermit of the Pass", "at": hermit, "facing": 3,
         "portrait": "flare/male07", "dialogue": "pass_hermit",
         "look": {"head": "leather_hood", "chest": "leather_chest", "hands": "cloth_gloves", "legs": "cloth_pants", "feet": "leather_boots",
                  "main": "staff"}},
    ],
    "interactables": [
        {"id": "page_1", "name": "A burned page", "at": pages[0], "tile": 1276, "dialogue": "library_page_1"},
        {"id": "page_2", "name": "A burned page", "at": pages[1], "tile": 1276, "dialogue": "library_page_2"},
    ],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
