# Generates zones/sallows_mire.json: Act 1's last zone (levels 4-5), Mother Sallow's mire beyond the Drowned Chapel.
# Deterministic: no randomness beyond a fixed LCG.
#
# The flooded tunnel from the chapel's west crypt comes up at the north end. A causeway winds south through the dead
# wood, past the witch-lights, the drowned orchard and the hanged men's oak, to Mother Sallow's clearing: her hut, her
# cauldron and a ring of dry ground in the black water (the arena: 10 tiles round the cauldron, which floods in her
# second phase). East of the clearing the road to Ironhold (Act 2) is a Herald gate: sealed while she lives.
# Packs are level 4 to the oak and 5 after it; the champions are level 4.
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import json, math, sys

REVISION = 1
W, H = 80, 150
grid = [['~'] * W for _ in range(H)]
seed = 97531
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

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
    return 0.6 * noise(x, y, 10, 5) + 0.4 * noise(x, y, 4, 6)
def inside(x, y):
    return 2 <= x < W - 2 and 2 <= y < H - 2

# The Mire is wetter and more wooded than the Fens: dead trees everywhere, deep water in the hollows.
for y in range(H):
    for x in range(W):
        h = height(x, y)
        if not inside(x, y):
            grid[y][x] = 'T'
        elif h > 0.6:
            grid[y][x] = 'D' if rnd() < 0.35 else ('T' if rnd() < 0.3 else '.')
        elif h > 0.5:
            grid[y][x] = ',' if rnd() < 0.3 else ('D' if rnd() < 0.08 else '.')
        elif h < 0.27:
            grid[y][x] = 'w'
        else:
            grid[y][x] = '~' if rnd() > 0.05 else 'D'

def route(points, wobble=2.5):
    out = []
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        n = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
        for i in range(n):
            t = i / n
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            dx, dy = x1 - x0, y1 - y0
            ln = math.hypot(dx, dy) or 1
            bend = wobble * math.sin(t * math.pi) * math.sin((x + y) / 6.0)
            out.append((x - dy / ln * bend, y + dx / ln * bend))
    out.append(points[-1])
    return out
cx, cy = 40, 128                                       # the cauldron: the arena's heart
path = route([(38, 3), (36, 14), (48, 28), (58, 42), (50, 58), (32, 70), (24, 86), (34, 100), (44, 112), (cx, cy - 10)])
east_road = route([(cx + 10, cy), (60, cy - 2), (70, cy - 6), (77, cy - 8)], wobble=1)
track = path + east_road
def lay(points):
    for (px, py) in points:
        for y in range(int(py) - 3, int(py) + 4):
            for x in range(int(px) - 3, int(px) + 4):
                if not inside(x, y) and not (x >= W - 2 and points is east_road):
                    continue
                if not (0 <= x < W and 0 <= y < H):
                    continue
                d = math.hypot(x + 0.5 - px, y + 0.5 - py)
                if d <= 1.25:
                    grid[y][x] = '='
                elif d <= 2.2 and grid[y][x] != '=':
                    grid[y][x] = '-' if rnd() < 0.7 else '.'
                elif d <= 3.2 and grid[y][x] in 'TDw':
                    grid[y][x] = '.' if rnd() < 0.6 else '~'
lay(path); lay(east_road)

def clear(x0, y0, x1, y1, ch='.'):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if inside(x, y) and grid[y][x] not in '=-':
                grid[y][x] = ch
def island(gx, gy, rx, ry, ch='.'):
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
def mere(px, py, rx, ry):
    for y in range(py - ry - 2, py + ry + 3):
        for x in range(px - rx - 2, px + rx + 3):
            if not inside(x, y) or grid[y][x] in '=-':
                continue
            if ((x - px) / rx) ** 2 + ((y - py) / ry) ** 2 <= 1:
                grid[y][x] = 'w'
            elif ((x - px) / (rx + 1.5)) ** 2 + ((y - py) / (ry + 1.5)) ** 2 <= 1:
                grid[y][x] = ',' if rnd() < 0.4 else '.'

# --- The tunnel mouth (the entrance) ---
island(38, 6, 5, 4)
start = [38, 7]
shrine1 = [42, 9]
put(*shrine1, 'S')
# --- The dead wood and the witch-lights ---
island(62, 24, 4, 3)                                   # witch-lights over a cultist's camp, east
put(63, 23, 't'); put(64, 23, 'X'); put(63, 24, 'X'); put(64, 24, 'X')
put(60, 26, 'c')
island(30, 30, 4, 3)                                   # a drowned shrine of the Lantern, west
for (dx, dy) in [(-2, -2), (2, -2), (-2, 2), (2, 2)]:
    put(30 + dx, 30 + dy, 'r')
mere(70, 50, 5, 7)
# --- The drowned orchard (shrine 2) ---
island(40, 62, 6, 4)
for (x, y) in [(36, 60), (39, 59), (43, 60), (45, 63), (37, 64), (41, 65)]:
    put(x, y, 'D')
shrine2 = [46, 66]
clear(shrine2[0] - 1, shrine2[1] - 1, shrine2[0] + 1, shrine2[1] + 1)
put(*shrine2, 'S')
island(12, 72, 4, 4)                                   # a sunken barn, far west
put(12, 72, 'H')
for (dx, dy) in [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)]:
    put(12 + dx, 72 + dy, 'X')
mere(48, 80, 6, 5)
# --- The hanged men's oak ---
island(16, 94, 4, 3)
put(16, 94, 'g'); put(14, 93, 'g'); put(18, 95, 'g')
shrine3 = [38, 106]                                    # the last shrine before her clearing
clear(shrine3[0] - 1, shrine3[1] - 1, shrine3[0] + 1, shrine3[1] + 1)
put(*shrine3, 'S')
island(62, 102, 4, 3)                                  # a peat-cutter's hut, east
put(63, 101, 't'); put(64, 101, 'X'); put(63, 102, 'X'); put(64, 102, 'X')
# --- Mother Sallow's clearing: a ring of dry ground round her cauldron, her hut behind ---
for y in range(cy - 13, cy + 14):
    for x in range(cx - 13, cx + 14):
        if not inside(x, y):
            continue
        d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
        if d <= 10.5:
            grid[y][x] = ',' if rnd() < 0.1 else '.'
        elif d <= 12.5 and grid[y][x] not in '=-':
            grid[y][x] = 'w' if rnd() < 0.5 else '~'
lay(route([(44, 112), (cx, cy - 10)]))                 # the causeway into it
lay(east_road)                                         # and the road out east
put(cx, cy - 1, 'K')                                   # the cauldron
put(cx - 5, cy - 7, 'H')                               # her hut
for (dx, dy) in [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)]:
    put(cx - 5 + dx, cy - 7 + dy, 'X')
for (x, y) in [(cx - 8, cy + 4), (cx + 7, cy + 6), (cx + 8, cy - 5), (cx - 3, cy + 8)]:
    put(x, y, 'q')                                     # bones of the drowned
# The Herald gate on the east road: braced rails between stone posts.
gx = W - 6
for y in range(cy - 11, cy - 4):
    if grid[y][gx] in '=-.,~':
        grid[y][gx] = 'G'
gate_at = [gx - 2, int(east_road[-1][1])]                # in front of the rails: they are the water's wall

# Reeds in the bog.
for y in range(H):
    for x in range(W):
        if grid[y][x] == '~' and rnd() < 0.14:
            grid[y][x] = '%'
rows = [''.join(r) for r in grid]

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
    cache("tunnel_mouth", 34, 5),
    cache("witch_lights", 65, 25, facing=1),
    cache("lantern_shrine", 31, 32),
    cache("orchard", 38, 61),
    cache("sunken_barn", 14, 74, facing=1),
    cache("hanged_oak", 17, 96),
    cache("peat_hut", 61, 103, facing=1),
    cache("sallows_hut", cx - 2, cy - 8, kind="Reliquary"),
]

def pack(*members, at, radius=2.0, level=4):
    out = []
    for m in members:
        d = dict(zip(("creature", "rank", "count"), m[:3]))
        if len(m) > 3:
            d["affixes"] = m[3]
        out.append(d)
    return {"at": at, "radius": radius, "pack": out, "level": level}
def on(i, dx=0, dy=0):
    x, y = path[min(i, len(path) - 1)]
    return [int(x) + dx, int(y) + dy]
N, M, C, B = "Normal", "Minion", "Champion", "Herald"
V = ["vengeful"]
swarm = lambda n=2, m=3: (("plague_crawler", N, n), ("plague_crawler", M, m))
spawns = [
    # The dead wood (level 4)
    pack(("drowned_dead", N, 2), ("fen_zombie", N, 1), at=on(20, 1)),
    pack(*swarm(), at=on(45, -2)),
    pack(("cultist", N, 2), ("cult_chanter", N, 1), at=[62, 27]),
    pack(("bog_witch", N, 1), ("fen_zombie", M, 3), at=on(70, 2)),
    pack(("bone_archer", N, 1), ("drowned_dead", N, 2), at=[30, 32]),
    pack(("fen_zombie", N, 3), at=on(95)),
    pack(*swarm(3, 3), at=on(115, 2)),
    # The drowned orchard (level 4)
    pack(("cultist", C, 1, V), ("cultist", N, 2), at=[40, 61]),
    pack(("drowned_dead", N, 3), at=on(155, -2)),
    pack(("bog_witch", N, 1), ("drowned_dead", N, 1), at=[13, 75]),
    pack(*swarm(), at=on(175, 2)),
    pack(("cultist", N, 2), ("cult_chanter", N, 1), at=on(195, -2)),
    # The hanged men's oak (level 4)
    pack(("grave_knight", N, 1), ("drowned_dead", N, 2), at=[17, 92]),
    pack(("bone_archer", N, 2), ("fen_zombie", N, 1), at=on(225, 2)),
    # The last stretch (level 5)
    pack(*swarm(3, 3), at=on(250, -2), level=5),
    pack(("cultist", N, 2), ("cult_chanter", N, 1), at=[61, 104], level=5),
    pack(("grave_knight", C, 1, V), ("drowned_dead", N, 1), at=on(270, 2), level=4),
    # Mother Sallow, Herald of Rot, at her cauldron (level 4: a Herald fights two levels up, so as a level-6 creature)
    pack(("mother_sallow", B, 1), at=[cx, cy], radius=0, level=4),
]
zone = {
    "id": "sallows_mire", "name": "Sallow's Mire", "level": 4, "revision": REVISION, "tileset": "tileset_grassland",
    "tagline": "Her cauldron smokes beyond the dead wood.",
    "darkness": 0.78, "heroLight": 6.0,
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
        "c": {"ground": "grass", "objects": [102], "blocks": True},
        "K": {"ground": "grass", "objects": [102], "blocks": True, "light": 6.0, "lightColor": "#9ee06a"},
        "t": {"ground": "grass", "objects": [72, 75], "blocks": True, "blocksSight": True},
        "g": {"ground": "grass", "objects": [138], "blocks": True},
        "q": {"ground": "grass", "objects": [140, 141, 142, 143], "blocks": True},
        "S": {"ground": "grass", "objects": [265]},
        "H": {"ground": "grass", "objects": [296], "blocks": True, "blocksSight": True},
        "X": {"ground": "grass", "blocks": True},
        "G": {"ground": "path", "objects": [104, 107], "blocks": True, "stack": True},
    },
    "playerStart": start,
    "spawns": spawns,
    "shrines": [
        {"id": "tunnel_shrine", "name": "The Tunnel Shrine", "at": shrine1},
        {"id": "orchard_shrine", "name": "The Orchard Shrine", "at": shrine2},
        {"id": "oak_shrine", "name": "The Hanged Oak Shrine", "at": shrine3},
    ],
    "exits": [
        {"id": "chapel", "label": "The Drowned Chapel", "at": [38, 3], "radius": 1.5, "to": "drowned_chapel", "toExit": "mire",
         "arrive": [38, 8]},
        {"id": "iron_march", "label": "The Iron March", "at": gate_at, "radius": 2.0,
         "requires": {"act1.sallow": "slain"},
         "sealedText": "Black water stands across the road east. It will not fall while Mother Sallow lives.",
         "openText": "The road east to Ironhold lies open. (Act 2 is still being written.)"},
    ],
    "caches": caches,
    # The Well-Keeper's Daughter (M2 step 9): the hearth by her hut, where her Cinder Relic lies.
    "interactables": [{"id": "sallows_hearth", "name": "Sallow's hearth", "at": [cx - 7, cy - 4], "tile": 102, "dialogue": "mire_hearth"}],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
