# Generates zones/rotwater_fens.json: Act 1's second zone (levels 2-3), the poison fens south-west of Fort Gallowmere.
# Deterministic: no randomness beyond a fixed LCG.
#
# A causeway runs from the old ferry landing below the fort's west gate (the north-east corner) to the sealed doors of
# the Drowned Chapel (the south-west corner, zone 1.3), through six stretches: the ferry landing, the reed maze, the drowned
# village (a cult camp in its ruins), the fork (the Sunken Road west, all bog, or the Old Dyke east, dry and busier),
# the rot altar where the routes meet again, and the chapel approach. Poison bog ('~') fills the low ground: walkable,
# slow, and a Fortitude save every two seconds. Deep meres ('w') block. Packs are level 2 as far as the end of the fork,
# 3 from the rot altar on (a hero arrives there at about level 3); the champions (each Vengeful) are level 2.
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import json, math, sys

REVISION = 1
W, H = 96, 180
grid = [['~'] * W for _ in range(H)]
seed = 2468013
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

# Smooth value noise for the lie of the land: high ground is dry, low ground is bog, the lowest is deep water.
def lattice(ix, iy, salt):
    h = (ix * 374761393 + iy * 668265263 + salt * 2147483647) & 0xffffffff
    h = ((h ^ (h >> 13)) * 1274126177) & 0xffffffff
    return ((h ^ (h >> 16)) & 0xffff) / 0xffff
def noise(x, y, scale, salt):
    fx, fy = x / scale, y / scale
    ix, iy = int(math.floor(fx)), int(math.floor(fy))
    tx, ty = fx - ix, fy - iy
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    a, b = lattice(ix, iy, salt), lattice(ix + 1, iy, salt)
    c, d = lattice(ix, iy + 1, salt), lattice(ix + 1, iy + 1, salt)
    return (a + (b - a) * tx) * (1 - ty) + (c + (d - c) * tx) * ty
def height(x, y):
    return 0.65 * noise(x, y, 11, 1) + 0.35 * noise(x, y, 4.5, 2)

def inside(x, y):
    return 2 <= x < W - 2 and 2 <= y < H - 2

for y in range(H):
    for x in range(W):
        h = height(x, y)
        if not inside(x, y):
            grid[y][x] = 'T'
        elif h > 0.62:
            grid[y][x] = 'T' if rnd() < 0.35 else ('D' if rnd() < 0.3 else ('.' if rnd() < 0.7 else ','))
        elif h > 0.5:
            grid[y][x] = ',' if rnd() < 0.25 else ('D' if rnd() < 0.05 else '.')
        elif h < 0.24:
            grid[y][x] = 'w'
        else:
            grid[y][x] = '~' if rnd() > 0.04 else 'D'

# The causeway: a winding track along waypoints. The main route, then the fork's two branches.
def route(points, wobble=2.5):
    out = []
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        n = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
        for i in range(n):
            t = i / n
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            # Bend sideways to the segment's direction, so the track meanders.
            dx, dy = x1 - x0, y1 - y0
            ln = math.hypot(dx, dy) or 1
            bend = wobble * math.sin(t * math.pi) * math.sin((x + y) / 7.0)
            out.append((x - dy / ln * bend, y + dx / ln * bend))
    out.append(points[-1])
    return out
main_north = route([(86, 4), (84, 14), (72, 28), (56, 38), (40, 50), (30, 64), (32, 78), (44, 92)])
sunken_road = route([(44, 92), (30, 102), (20, 116), (22, 130), (36, 140), (46, 146)], wobble=3)
old_dyke = route([(44, 92), (60, 100), (72, 112), (70, 128), (58, 140), (46, 146)], wobble=2)
main_south = route([(46, 146), (36, 156), (24, 164), (14, 172)])
track = main_north + sunken_road + old_dyke + main_south

def lay(points, sunk=False):
    for (px, py) in points:
        for y in range(int(py) - 3, int(py) + 4):
            for x in range(int(px) - 3, int(px) + 4):
                if not inside(x, y):
                    continue
                d = math.hypot(x + 0.5 - px, y + 0.5 - py)
                if d <= 1.25:
                    # The Sunken Road is half drowned: every few steps the causeway dips under the bog.
                    grid[y][x] = '~' if sunk and math.sin((x + y) / 3.1) > 0.55 else '='
                elif d <= 2.2 and grid[y][x] != '=':
                    grid[y][x] = '-' if rnd() < 0.7 else '.'
                elif d <= 3.2 and grid[y][x] in 'TDw':
                    grid[y][x] = '.' if rnd() < 0.6 else '~'
lay(main_north); lay(old_dyke); lay(main_south); lay(sunken_road, sunk=True)

def clear(x0, y0, x1, y1, ch='.'):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if inside(x, y) and grid[y][x] not in '=-':
                grid[y][x] = ch
def island(gx, gy, rx, ry, ch='.'):
    """A dry oval (a glade, a camp) with a dry spit back to the causeway's nearest point, so it is always reachable."""
    for y in range(gy - ry, gy + ry + 1):
        for x in range(gx - rx, gx + rx + 1):
            if inside(x, y) and ((x - gx) / (rx + 0.5)) ** 2 + ((y - gy) / (ry + 0.5)) ** 2 <= 1 and grid[y][x] not in '=-':
                grid[y][x] = ',' if rnd() < 0.12 else ch
    px, py = min(track, key=lambda p: (p[0] - gx) ** 2 + (p[1] - gy) ** 2)
    n = int(math.hypot(px - gx, py - gy) * 2) + 1
    for i in range(n + 1):
        x, y = int(gx + (px - gx) * i / n), int(gy + (py - gy) * i / n)
        for (ax, ay) in ((x, y), (x + 1, y)):
            if inside(ax, ay) and grid[ay][ax] not in '=-':
                grid[ay][ax] = '.'
def put(x, y, ch):
    grid[y][x] = ch
def house(x, y):
    """A ruined house (one big tile) on its 3x3 footprint."""
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            put(x + dx, y + dy, 'X')
    put(x, y, 'H')
def mere(px, py, rx, ry):
    """Deep water with a dry rim, so its banks read."""
    for y in range(py - ry - 2, py + ry + 3):
        for x in range(px - rx - 2, px + rx + 3):
            if not inside(x, y) or grid[y][x] in '=-':
                continue
            if ((x - px) / rx) ** 2 + ((y - py) / ry) ** 2 <= 1:
                grid[y][x] = 'w'
            elif ((x - px) / (rx + 1.5)) ** 2 + ((y - py) / (ry + 1.5)) ** 2 <= 1:
                grid[y][x] = ',' if rnd() < 0.4 else '.'

# --- The ferry landing (the entrance, below the fort's west gate) ---
island(84, 8, 6, 5)
start = [84, 6]
shrine1 = [79, 9]
put(*shrine1, 'S')
for (x, y) in [(90, 12), (91, 13)]:                    # Coll's old ferry, sunk at its moorings
    put(x, y, 'b')
for (x, y) in [(88, 3), (89, 3), (90, 3)]:
    put(x, y, 'f')
mere(66, 12, 5, 3)                                     # the landing's black mere

# --- The reed maze ---
island(58, 24, 3, 3)                                   # a reed-cutter's hut
house(58, 24)
island(78, 34, 3, 3)                                   # a drowned shrine of the Lantern, east
for (dx, dy) in [(-2, -2), (2, -2), (-2, 2), (2, 2)]:
    put(78 + dx, 34 + dy, 'r')
island(42, 36, 3, 2)                                   # an eel-trap islet, west

# --- The drowned village, the cult's camp in its ruins (shrine 2) ---
clear(20, 56, 40, 74)
for (x, y) in [(24, 59), (34, 58), (23, 69)]:
    house(x, y)
for (x, y) in [(36, 68), (38, 64)]:                    # the cult's tents
    put(x, y, 't'); put(x + 1, y, 'X'); put(x, y + 1, 'X'); put(x + 1, y + 1, 'X')
put(30, 66, 'c')                                       # their fire
for (x, y) in [(27, 64), (33, 72), (21, 63)]:
    put(x, y, 'x')
shrine2 = [36, 76]
clear(shrine2[0] - 1, shrine2[1] - 1, shrine2[0] + 1, shrine2[1] + 1)
put(*shrine2, 'S')
mere(14, 84, 4, 6)

# --- The fork: the Sunken Road (west) and the Old Dyke (east) ---
island(10, 108, 4, 3)                                  # a gibbet on a hummock, off the Sunken Road
put(10, 108, 'g')
island(84, 104, 4, 4)                                  # a smugglers' cache on the Old Dyke's far side
for (x, y) in [(83, 102), (86, 105), (82, 106)]:
    put(x, y, 'x')
island(84, 124, 4, 3)                                  # a peat-cutters' camp, east
put(85, 123, 't'); put(86, 123, 'X'); put(85, 124, 'X'); put(86, 124, 'X')
mere(46, 118, 7, 9)                                    # the black mere between the two routes
for (x, y) in [(36, 118), (56, 116), (46, 106)]:
    if grid[y][x] in '.,~':
        put(x, y, 'D')
shrine3 = [62, 106]
clear(shrine3[0] - 1, shrine3[1] - 1, shrine3[0] + 1, shrine3[1] + 1)
put(*shrine3, 'S')

# --- The rot altar, where the routes meet ---
clear(38, 140, 54, 152)
for (dx, dy) in [(-5, -4), (5, -4), (-6, 1), (6, 1), (-4, 5), (4, 5)]:  # a ring of standing stones
    put(46 + dx, 146 + dy, 'r')
put(46, 146, 'A')                                      # the altar stone
for (x, y) in [(40, 142), (52, 142), (41, 151), (51, 151)]:
    put(x, y, '+')

# --- The chapel approach and the sealed doors (the south-west corner) ---
shrine4 = [32, 158]
clear(shrine4[0] - 1, shrine4[1] - 1, shrine4[0] + 1, shrine4[1] + 1)
put(*shrine4, 'S')
island(8, 150, 3, 3)                                   # a drowned graveyard, west of the approach
for (x, y) in [(7, 148), (9, 148), (7, 150), (9, 152)]:
    put(x, y, '+')
clear(6, 166, 20, 176)
for x in range(4, 22):
    put(x, 177, 'F')
for (x, y) in [(10, 177), (11, 177), (12, 177)]:
    put(x, y, 'G')
put(9, 177, 'P'); put(13, 177, 'P')
chapel_at = [11, 175]

# The fort's side: the track runs off the top of the map toward the west gate.
for y in range(0, 4):
    for x in range(84, 89):
        grid[y][x] = '=' if 85 <= x <= 87 else '-'
fort_exit = [86, 1]

# Reeds in the bog, now the layout is fixed (they don't block).
for y in range(H):
    for x in range(W):
        if grid[y][x] == '~' and rnd() < 0.14:
            grid[y][x] = '%'

rows = [''.join(r) for r in grid]

# Chests sit on the nearest plain-grass cell with open ground beside it.
def open_cell(x, y):
    best = None
    for dy in range(-5, 6):
        for dx in range(-5, 6):
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
    cache("landing", 88, 9, facing=1),
    cache("reed_hut", 60, 22),
    cache("lantern_islet", 79, 36, facing=1),
    cache("eel_traps", 43, 37),
    cache("cult_camp", 38, 66, facing=1),
    cache("gibbet", 11, 110),
    cache("smugglers", 85, 104, facing=1),
    cache("peat_camp", 83, 125),
    cache("rot_altar", 46, 149, kind="Reliquary"),
    cache("drowned_graves", 8, 151, facing=1),
]

def pack(*members, at, radius=2.0, level=2):
    out = []
    for m in members:
        d = dict(zip(("creature", "rank", "count"), m[:3]))
        if len(m) > 3:
            d["affixes"] = m[3]
        out.append(d)
    return {"at": at, "radius": radius, "pack": out, "level": level}
def on(path, i, dx=0, dy=0):
    x, y = path[min(i, len(path) - 1)]
    return [int(x) + dx, int(y) + dy]
N, M, C = "Normal", "Minion", "Champion"
V = ["vengeful"]
swarm = lambda n=2, m=3: (("plague_crawler", N, n), ("plague_crawler", M, m))
spawns = [
    # The ferry landing and the reed maze (level 2)
    pack(*swarm(2, 3), at=on(main_north, 30)),
    pack(("fen_zombie", N, 3), at=[70, 16]),
    pack(("drowned_dead", N, 2), ("fen_zombie", N, 1), at=on(main_north, 55, 2)),
    pack(("cultist", N, 2), at=[58, 27]),
    pack(*swarm(), at=on(main_north, 80, -2)),
    pack(("bog_witch", N, 1), ("fen_zombie", M, 3), at=[78, 31]),
    pack(("drowned_dead", N, 3), at=on(main_north, 105, 1)),
    pack(*swarm(3, 2), at=[44, 38]),
    pack(("cultist", N, 2), ("cult_chanter", N, 1), at=on(main_north, 118, -1)),
    # The drowned village (level 2): three packs in the ruins, far enough apart to be fought one at a time
    pack(("cultist", N, 3), at=[26, 62]),
    pack(("cultist", C, 1, V), ("cultist", N, 2), at=[34, 67]),
    pack(*swarm(), at=[24, 72]),
    pack(("drowned_dead", N, 2), ("bone_archer", N, 1), at=on(main_north, 190, 2)),
    pack(*swarm(3, 3), at=on(main_north, 210, -2)),
    # The Sunken Road (level 2)
    pack(("bog_witch", N, 1), ("fen_zombie", M, 4), at=on(sunken_road, 15), level=2),
    pack(*swarm(), at=on(sunken_road, 30, 2), level=2),
    pack(("drowned_dead", N, 3), at=[11, 106], level=2),
    pack(("fen_zombie", N, 3), at=on(sunken_road, 55), level=2),
    pack(*swarm(3, 3), at=on(sunken_road, 75, -2), level=2),
    pack(("bog_witch", N, 1), ("drowned_dead", N, 1), at=on(sunken_road, 95, 2), level=2),
    # The Old Dyke (level 2)
    pack(("cultist", N, 2), ("cult_chanter", N, 1), at=on(old_dyke, 15), level=2),
    pack(("bone_archer", N, 2), ("drowned_dead", N, 1), at=[64, 104], level=2),
    pack(("cultist", N, 3), at=[84, 106], level=2),
    pack(*swarm(), at=on(old_dyke, 50, -2), level=2),
    pack(("cultist", C, 1, V), ("cultist", N, 1), at=[83, 126], level=2),
    pack(("cult_chanter", N, 2), ("fen_zombie", M, 3), at=on(old_dyke, 85, 2), level=2),
    pack(("grave_knight", N, 1), ("drowned_dead", N, 2), at=on(old_dyke, 105), level=2),
    # The rot altar (level 3)
    # (Packs here stand far enough apart to be fought one at a time: a clearing holds at most two.)
    pack(("cultist", N, 2), ("cult_chanter", N, 1), at=[54, 139], level=3),
    pack(("cultist", C, 1, V), ("cultist", N, 1), ("cult_chanter", N, 1), at=[46, 148], radius=2.5, level=2),
    # The chapel approach (level 3)
    pack(*swarm(3, 3), at=on(main_south, 14), level=3),
    pack(("drowned_dead", N, 3), at=[9, 149], level=3),
    pack(("grave_knight", C, 1, V), ("drowned_dead", N, 1), at=on(main_south, 40, -1), level=2),
    pack(("cultist", N, 2), ("cult_chanter", N, 1), at=[11, 171], level=3),
]
zone = {
    "id": "rotwater_fens", "name": "The Rotwater Fens", "level": 2, "revision": REVISION, "tileset": "tileset_grassland",
    "tagline": "Black water, and the reek of rot.",
    "rows": rows,
    "legend": {
        ".": {"ground": "grass"},
        ",": {"ground": "grass", "objects": [112, 113, 114, 115, 116, 117, 118, 120, 121, 122, 123, 124, 125, 126, 127]},
        "=": {"ground": "path"},
        "-": {"ground": "path_edge"},
        "~": {"ground": "bog"},
        "%": {"ground": "bog", "objects": [112, 113, 114, 115, 116, 117, 118, 120, 121, 122, 123, 124, 125, 126, 127]},
        "w": {"ground": "water"},
        "T": {"ground": "grass", "objects": [248, 249, 250, 251], "blocks": True, "blocksSight": True},
        "D": {"ground": "grass", "objects": [244, 245, 246, 247], "blocks": True},
        "r": {"ground": "grass", "objects": [128, 129, 130, 131], "blocks": True},
        "+": {"ground": "grass", "objects": [140, 141, 142, 143], "blocks": True},
        "f": {"ground": "grass", "objects": [105, 106, 109, 110], "blocks": True},
        "b": {"ground": "bog", "objects": [170, 171], "blocks": True},
        "c": {"ground": "grass", "objects": [102], "blocks": True},
        "t": {"ground": "grass", "objects": [72, 75], "blocks": True, "blocksSight": True},
        "x": {"ground": "grass", "objects": [96, 97, 98, 99, 100, 101], "blocks": True},
        "g": {"ground": "grass", "objects": [138], "blocks": True},
        "A": {"ground": "grass", "objects": [142], "blocks": True},
        "S": {"ground": "grass", "objects": [265]},
        "H": {"ground": "grass", "objects": [296], "blocks": True, "blocksSight": True},
        "X": {"ground": "grass", "blocks": True},
        "G": {"ground": "path", "objects": [104, 107], "blocks": True, "stack": True},
        "P": {"ground": "grass", "objects": [132], "blocks": True, "blocksSight": True},
        "F": {"ground": "grass", "objects": [107], "blocks": True},
    },
    "playerStart": start,
    "spawns": spawns,
    "shrines": [
        {"id": "landing_shrine", "name": "The Landing Shrine", "at": shrine1},
        {"id": "village_shrine", "name": "The Drowned Village Shrine", "at": shrine2},
        {"id": "dyke_shrine", "name": "The Old Dyke Shrine", "at": shrine3},
        {"id": "chapel_shrine", "name": "The Chapel Road Shrine", "at": shrine4},
    ],
    "exits": [
        {"id": "fort", "label": "Fort Gallowmere", "at": fort_exit, "radius": 1.5, "to": "fort_gallowmere", "toExit": "west_gate",
         "arrive": [86, 5]},
        {"id": "chapel", "label": "The Drowned Chapel", "at": chapel_at, "radius": 2.0, "to": "drowned_chapel", "toExit": "fens",
         "arrive": [chapel_at[0] + 3, chapel_at[1] - 4]},
    ],
    "caches": caches,
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
