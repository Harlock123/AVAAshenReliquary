# Generates zones/iron_march.json: Act 2's first zone (levels 6-7), the old war road from Sallow's Mire to the gate of
# Ironhold. Deterministic: no randomness beyond a fixed LCG.
#
# The road runs north (low y) from the Mire causeway at the south end to Ironhold's outer wall, through six stretches:
# the causeway, the old battlefield (lines of broken low walls with crossbow squads behind them), the watchtower ruin,
# the legion's siege camp (barricades and tents), the hollow road (outlaw ambushes) and the siege line before the gate,
# held by Centurion Varro. Side glades: the outlaws' lookout (Red Mirella's wanted poster) and a drowned shrine.
#
# Act 2's mechanics: cover (the legend's low walls, rubble, crates and barricades: a shot across one at someone close
# behind it is against +2 AC) and kiting (crossbowmen and bowmen fall back when the hero closes). The tileset is
# tileset_ironhold: Flare's grassland plus rubberduck's Old Ruins (ruins tile n is n + 1000).
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import json, math, sys

REVISION = 1
W, H = 64, 200
grid = [['.'] * W for _ in range(H)]
seed = 2468013
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

def path_x(y):  # the war road's centre: straighter than Gallows Road, a built road
    return 32 + 7 * math.sin(y / 19.0) + 2 * math.sin(y / 7.1)

# Forest border, thinning to open ground near the road (this was farmland before the war).
for y in range(H):
    cx = path_x(y)
    for x in range(W):
        d = abs(x - cx)
        edge = min(x, W - 1 - x, y, H - 1 - y)
        if edge < 2:
            grid[y][x] = 'T'
        elif d > 6:
            p = 0.03 + 0.045 * max(0, d - 6)
            if rnd() < min(p, 0.5):
                grid[y][x] = 'T' if rnd() < 0.5 else ('O' if rnd() < 0.7 else 'D')
            elif rnd() < 0.09:
                grid[y][x] = ','
        elif rnd() < 0.05:
            grid[y][x] = ','

# The road: paved down the middle (old legion flagstones), worn at the edges.
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

def clear(x0, y0, x1, y1, ch='.'):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if inside(x, y) and grid[y][x] not in '=-':
                grid[y][x] = ',' if ch == '.' and rnd() < 0.08 else ch

def put(x, y, ch):
    if inside(x, y):
        grid[y][x] = ch

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

def low_wall(x0, x1, y, gaps=()):
    """A broken low wall across the field (cover), with gaps to walk through; the road always passes."""
    for x in range(x0, x1 + 1):
        if not inside(x, y) or grid[y][x] in '=-' or any(g0 <= x <= g1 for (g0, g1) in gaps):
            continue
        grid[y][x] = 'v' if rnd() < 0.25 else 'u'          # mostly rubble heaps: the flat low walls barely show

def rubble(x, y):
    put(x, y, 'u')

P = lambda y, dx=0: [int(path_x(y)) + dx, y]

# --- The Mire causeway (the south end): the way in from Sallow's Mire ---
mx = int(path_x(192))
clear(mx - 6, 184, mx + 6, 197)
for y in range(193, H - 2):
    for x in range(mx - 1, mx + 2):
        grid[y][x] = '='
start = [mx, 192]
exit_south = [mx, 196]
shrine1 = [mx + 4, 187]
put(*shrine1, 'S')
put(mx - 4, 186, 'M')                                  # a legion milestone: IRONHOLD XII MILES

# --- The old battlefield: open ground, three broken low walls across it, rubble and fallen statues ---
for y in range(140, 182):
    cx = int(path_x(y))
    for x in range(cx - 18, cx + 19):
        if inside(x, y) and grid[y][x] in 'TOD':
            grid[y][x] = ',' if rnd() < 0.15 else '.'
for y in (174, 161, 148):
    cx = int(path_x(y))
    g = int(rnd() * 6) - 3
    low_wall(cx - 16, cx + 16, y, gaps=[(cx - 12 + g, cx - 10 + g), (cx + 9 + g, cx + 11 + g)])
for (dx, y) in [(-11, 168), (8, 166), (-6, 155), (13, 153), (-14, 144), (5, 142)]:
    rubble(int(path_x(y)) + dx, y)
for (dx, y) in [(-9, 170), (11, 158), (-15, 152)]:
    put(int(path_x(y)) + dx, y, 'Z')                  # fallen legion statues

# --- The outlaws' lookout (a glade to the west of the battlefield): Red Mirella's men watch the road ---
ox, oy = int(path_x(160)) - 19, 160
glade(ox, oy, 5, 4)
put(ox, oy - 1, 'c')                                   # their fire
for (x, y) in [(ox - 4, oy - 3), (ox + 2, oy - 3)]:
    put(x, y, 't'); put(x + 1, y, 'X'); put(x, y + 1, 'X'); put(x + 1, y + 1, 'X')
for (x, y) in [(ox + 4, oy + 1), (ox + 4, oy + 2)]:
    put(x, y, 'k')
poster = [int(path_x(168)) + 3, 168]                  # a wanted poster nailed to a post by the road

# --- The watchtower ruin (east of the road): crossbowmen behind its broken walls ---
tx, ty = int(path_x(126)) + 10, 126
clear(tx - 5, ty - 6, tx + 6, ty + 6)
for x in range(tx - 3, tx + 4):
    for y in (ty - 4, ty + 3):
        put(x, y, 'W')
for y in range(ty - 4, ty + 4):
    put(tx - 3, y, 'W'); put(tx + 3, y, 'W')
for y in (ty - 1, ty):                                 # its door, facing the road
    put(tx - 3, y, '.')
put(tx + 3, ty + 1, 'u')                               # a breach in the far wall, rubble across it
put(tx + 3, ty + 2, '.')
for (x, y) in [(tx - 2, ty - 3), (tx + 2, ty + 2)]:
    put(x, y, 'k')
shrine2 = [int(path_x(118)) - 4, 118]
clear(shrine2[0] - 1, shrine2[1] - 1, shrine2[0] + 1, shrine2[1] + 1)
put(*shrine2, 'S')

# --- The siege camp: the legion's abandoned camp, barricades across the road, tents behind ---
for y in range(80, 112):
    cx = int(path_x(y))
    for x in range(cx - 14, cx + 15):
        if inside(x, y) and grid[y][x] in 'TOD':
            grid[y][x] = ',' if rnd() < 0.12 else '.'
for (y, dx0, dx1) in [(106, -10, -3), (106, 3, 10), (94, -12, -4), (94, 4, 12), (84, -9, -3), (84, 3, 9)]:
    cx = int(path_x(y))
    for x in range(cx + dx0, cx + dx1 + 1):
        put(x, y, 'k' if rnd() < 0.5 else 'u')
for (dx, y) in [(-11, 100), (8, 99), (-12, 88), (9, 89)]:
    x = int(path_x(y)) + dx
    put(x, y, 't'); put(x + 1, y, 'X'); put(x, y + 1, 'X'); put(x + 1, y + 1, 'X')
put(int(path_x(97)) - 7, 97, 'c')
pond_c = [int(path_x(92)) + 17, 92]                    # a flooded ditch east of the camp
for y in range(pond_c[1] - 3, pond_c[1] + 4):
    for x in range(pond_c[0] - 3, pond_c[0] + 4):
        if inside(x, y) and ((x - pond_c[0]) / 3.2) ** 2 + ((y - pond_c[1]) / 3.2) ** 2 <= 1:
            grid[y][x] = 'w'

# --- The hollow road: forest closes in, outlaws in ambush; a drowned shrine in a glade to the east ---
glade(int(path_x(62)) + 15, 62, 4, 3)
put(int(path_x(62)) + 15, 61, 'Y')                     # an old shrine to the Lantern, broken
shrine3 = [int(path_x(50)) - 3, 50]
clear(shrine3[0] - 1, shrine3[1] - 1, shrine3[0] + 1, shrine3[1] + 1)
put(*shrine3, 'S')
for (dx, y) in [(-5, 70), (5, 66), (-6, 56), (6, 44)]:
    put(int(path_x(y)) + dx, y, 'u')

# --- The siege line before Ironhold's outer wall (the north end) ---
for y in range(4, 34):
    cx = int(path_x(y))
    for x in range(cx - 16, cx + 17):
        if inside(x, y) and grid[y][x] in 'TOD,':
            grid[y][x] = ',' if rnd() < 0.08 else '.'
for (y, dx0, dx1) in [(24, -13, -3), (24, 3, 13), (16, -11, -4), (16, 4, 11)]:
    cx = int(path_x(y))
    for x in range(cx + dx0, cx + dx1 + 1):
        put(x, y, 'k' if rnd() < 0.4 else 'u')
# The wall: dressed stone across the map, a gatehouse over the road with its gate open a crack.
gx = int(path_x(3))
for x in range(2, W - 2):
    grid[3][x] = 'W'
    grid[2][x] = 'T'
for x in range(gx - 1, gx + 2):
    grid[3][x] = '='
grid[3][gx - 2] = 'Q'
grid[3][gx + 2] = 'Q'
exit_north = [gx, 4]
for (dx, y) in [(-8, 8), (8, 8), (-14, 11), (14, 11)]:
    put(gx + dx, y, 'B')                               # Ironhold's braziers along the wall

rows = [''.join(r) for r in grid]

# Chests, placed after the rows are drawn, mostly off the road so exploring pays.
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
    cache("causeway", mx - 5, 190),
    cache("battlefield", int(path_x(171)) + 14, 171, facing=1),
    cache("lookout", ox - 3, oy + 2),
    cache("watchtower", tx, ty, facing=1),
    cache("siege_camp", int(path_x(100)) - 13, 102),
    cache("drowned_shrine", int(path_x(62)) + 17, 63, facing=1),
    cache("hollow_road", int(path_x(76)) - 7, 76),
    cache("siege_line", gx + 12, 20, kind="Reliquary", facing=1),
]

def pack(*members, at, radius=2.0, level=None, flag=None, value="cleared"):
    out = []
    for m in members:
        d = dict(zip(("creature", "rank", "count"), m[:3]))
        if len(m) > 3:
            d["affixes"] = m[3]
        out.append(d)
    spawn = {"at": at, "radius": radius, "pack": out}
    if level:
        spawn["level"] = level
    if flag:
        spawn["flag"], spawn["value"] = flag, value
    return spawn
N, M, C = "Normal", "Minion", "Champion"
LEG, XB, RAID, BOW, VARRO = "legionary", "legion_crossbowman", "outlaw_raider", "outlaw_bowman", "centurion_varro"
spawns = [
    # The Mire causeway (level 6)
    pack((LEG, N, 2), at=P(182, 1)),
    # The old battlefield: squads behind the walls (crossbowmen behind, legionaries holding the gaps)
    pack((XB, N, 2), at=P(176, -6), radius=1.5),
    pack((LEG, N, 2), (XB, N, 1), at=P(176, 8)),
    pack((LEG, N, 3), at=P(167, -3)),
    pack((XB, N, 2), (LEG, N, 1), at=P(163, 9), radius=1.5),
    pack((LEG, C, 1, ["shielded"]), (LEG, N, 1), at=P(156, -2)),
    pack((XB, N, 3), at=P(150, -8), radius=1.5),
    pack((LEG, N, 2), (XB, N, 1), at=P(145, 6)),
    # The outlaws' lookout
    pack((RAID, N, 2), (BOW, N, 1), at=[ox + 1, oy + 1]),
    pack((BOW, N, 2), at=[ox - 2, oy - 2], radius=1.2),
    # The watchtower ruin (level 7 from here)
    pack((XB, N, 2), at=[tx, ty - 1], radius=1.2, level=7),
    pack((LEG, N, 2), at=[tx - 6, ty + 1], level=7),
    pack((LEG, N, 2), (XB, N, 1), at=P(132, -2), level=7),
    # The siege camp
    pack((XB, N, 2), (LEG, N, 1), at=P(108, -6), level=7),
    pack((LEG, N, 3), at=P(103, 3), level=7),
    pack((XB, C, 1, ["hasted"]), (XB, N, 2), at=P(96, 8), radius=1.5, level=7),
    pack((LEG, N, 2), (XB, N, 1), at=P(90, -7), level=7),
    pack((LEG, C, 1, ["vengeful"]), (LEG, N, 2), at=P(86, 1), level=7),
    # The hollow road: outlaw ambushes
    pack((RAID, N, 3), at=P(74, -3), level=7),
    pack((BOW, N, 2), (RAID, N, 1), at=P(66, 5), level=7),
    pack((RAID, N, 2), (BOW, N, 1), at=[int(path_x(62)) + 13, 63], level=7),
    pack((RAID, C, 1, ["hasted"]), (RAID, N, 2), at=P(54, -2), level=7),
    pack((BOW, N, 2), (RAID, N, 2), at=P(40, 3), level=7),
    # The siege line: Centurion Varro and his crossbows before the gate
    pack((XB, N, 3), at=P(27, -8), radius=1.5, level=7),
    pack((LEG, N, 3), at=P(20, 6), level=7),
    pack((XB, N, 2), (LEG, N, 1), at=P(13, -7), level=7),
    pack((VARRO, C, 1), (LEG, N, 2), (XB, N, 1), at=P(19, 0), level=7, flag="act2.varro", value="slain"),
]
FLAG = [1024, 1025, 1026, 1027, 1028, 1029, 1030, 1031]
zone = {
    "id": "iron_march", "name": "The Iron March", "level": 6, "act": 2, "revision": REVISION, "tileset": "tileset_ironhold",
    "tagline": "The dead of a forgotten war still march.",
    "rows": rows,
    "legend": {
        ".": {"ground": "grass"},
        ",": {"ground": "grass", "objects": [112, 113, 114, 115, 116, 117, 118, 120, 121, 122, 123, 124, 125, 126, 127]},
        "=": {"ground": "path", "tiles": FLAG},
        "-": {"ground": "path_edge"},
        "T": {"ground": "grass", "objects": [248, 249, 250, 251], "blocks": True, "blocksSight": True},
        "O": {"ground": "grass", "objects": [252, 253, 254, 255], "blocks": True, "blocksSight": True},
        "D": {"ground": "grass", "objects": [244, 245, 246, 247], "blocks": True},
        "w": {"ground": "water"},
        "c": {"ground": "grass", "objects": [102], "blocks": True},
        "t": {"ground": "grass", "objects": [72, 75], "blocks": True, "blocksSight": True},
        "X": {"ground": "grass", "blocks": True},
        "S": {"ground": "grass", "objects": [265]},
        # Cover: low broken walls, rubble, crates and barrels (seen over, blocking, and a shelter from shots).
        "v": {"ground": "grass", "objects": [1048, 1049, 1050, 1051, 1052, 1053], "blocks": True, "cover": True},
        "u": {"ground": "grass", "objects": [1187, 1189, 1190, 1192, 1193, 1194], "blocks": True, "cover": True},
        "k": {"ground": "grass", "objects": [1285, 1286, 1287, 1288], "blocks": True, "cover": True},
        # Ruins: walls, gate posts, statues, a milestone, a broken shrine, braziers.
        "W": {"ground": "grass", "objects": [1223, 1224, 1225, 1226, 1227, 1228, 1229], "blocks": True, "blocksSight": True},
        "Q": {"ground": "grass", "objects": [1230], "blocks": True, "blocksSight": True},
        "Z": {"ground": "grass", "objects": [1241, 1242, 1243, 1244], "blocks": True},
        "M": {"ground": "grass", "objects": [1276], "blocks": True},
        "Y": {"ground": "grass", "objects": [1291], "blocks": True},
        "B": {"ground": "grass", "objects": [102], "blocks": True, "light": 4.0},
    },
    "playerStart": start,
    "spawns": spawns,
    "shrines": [
        {"id": "causeway_shrine", "name": "The Causeway Shrine", "at": shrine1},
        {"id": "watchtower_shrine", "name": "The Watchtower Shrine", "at": shrine2},
        {"id": "hollow_shrine", "name": "The Hollow Road Shrine", "at": shrine3},
    ],
    "exits": [
        {"id": "mire", "label": "Sallow's Mire", "at": exit_south, "radius": 1.5, "to": "sallows_mire", "toExit": "iron_march",
         "arrive": [mx, 191]},
        {"id": "ironhold", "label": "Ironhold", "at": exit_north, "radius": 2.0, "to": "ironhold", "toExit": "march_gate",
         "arrive": [gx, 7]},
    ],
    "caches": caches,
    # The Outlaw Queen (Act 2's faction story, step 4) begins with her wanted poster by the road.
    "interactables": [{"id": "wanted_poster", "name": "Wanted poster", "at": poster, "tile": 139, "dialogue": "march_poster"}],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
