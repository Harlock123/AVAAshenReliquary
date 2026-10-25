# Generates zones/wyrms_nest.json: Act 4's third zone (levels 18-19), the caves inside Mount Cindral where Vorthrax the
# Cinderwyrm nests, above the Ember Gate. Deterministic: a fixed LCG.
#
# Caverns joined by tunnels climb north (low y) from the lava rivers: the Mouth (a shrine), the Bone Gallery (a fire ant
# hive among the wyrm's leavings), the Roost (cinder-drakes on ledges over a lava pool), the Inquisitors' Vigil (the
# Prophet's men keeping watch on the nest, a shrine beyond), the Clutch (the Wyrm's Egg, warm in its bed of embers: the
# Herald Remnant story of Act 4), and the Ember Gate's cavern at the top: the great door down into the Reliquary, where
# Vorthrax waits (M4 step 9). The door stays shut until he is dead.
#
# It is dark: the lava and a few braziers light it, and the hero's own light. The caves' walls are Flare's (recoloured),
# shaped by tools/cinder_common.wall_tiles.
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import math, sys
from cinder_common import Rng, legend, open_cell, pack, wall_tiles, write, LEADS

REVISION = 1
W, H = 66, 124
rnd = Rng(18130)
grid = [['w'] * W for _ in range(H)]

def inside(x, y):
    return 2 <= x < W - 2 and 2 <= y < H - 2

def put(x, y, ch):
    if inside(x, y):
        grid[y][x] = ch

def cavern(cx, cy, rx, ry):
    """A rough ellipse of floor, its rim broken by noise."""
    for y in range(cy - ry - 2, cy + ry + 3):
        for x in range(cx - rx - 2, cx + rx + 3):
            if not inside(x, y):
                continue
            r = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
            if r <= 1 + 0.25 * (rnd() - 0.5):
                grid[y][x] = ',' if rnd() < 0.1 else '.'

def tunnel(a, b, width=3):
    """A winding tunnel between two points."""
    (x0, y0), (x1, y1) = a, b
    steps = int(max(abs(x1 - x0), abs(y1 - y0)) * 1.5) + 1
    wobble = rnd() * 6
    for i in range(steps + 1):
        t = i / steps
        x = x0 + (x1 - x0) * t + 2.5 * math.sin(t * math.pi * 2 + wobble) * math.sin(t * math.pi)
        y = y0 + (y1 - y0) * t
        for dx in range(-(width // 2), width // 2 + 1):
            for dy in range(-(width // 2), width // 2 + 1):
                xx, yy = int(round(x)) + dx, int(round(y)) + dy
                if inside(xx, yy) and grid[yy][xx] == 'w':
                    grid[yy][xx] = '.'

# The chambers, south to north.
mouth = (33, 112)
gallery = (18, 92)
roost = (46, 78)
vigil = (24, 56)
clutch = (48, 36)
gate = (32, 14)
cavern(*mouth, 8, 6)
cavern(*gallery, 10, 7)
cavern(*roost, 11, 8)
cavern(*vigil, 10, 7)
cavern(*clutch, 9, 7)
cavern(*gate, 16, 9)
for a, b in [(mouth, gallery), (mouth, roost), (gallery, vigil), (roost, vigil), (vigil, clutch), (clutch, gate), (vigil, gate)]:
    tunnel(a, b, 3 if a != vigil or b != gate else 2)

# The way in from the rivers, south.
start = [mouth[0], H - 8]
for y in range(mouth[1], H - 2):
    for x in range(mouth[0] - 1, mouth[0] + 2):
        grid[y][x] = '.'

# Lava: a pool in the Roost under the drakes' ledges, a channel through the Gallery, lava round the Ember Gate's floor
# (Vorthrax's platforms in step 9).
for y in range(roost[1] - 3, roost[1] + 4):
    for x in range(roost[0] - 1, roost[0] + 8):
        if ((x - roost[0] - 3) / 4.5) ** 2 + ((y - roost[1]) / 3.0) ** 2 <= 1 and grid[y][x] in '.,':
            grid[y][x] = '~'
for x in range(gallery[0] - 9, gallery[0] - 2):
    for y in range(gallery[1] - 1, gallery[1] + 2):
        if grid[y][x] in '.,':
            grid[y][x] = '~'
for y in range(gate[1] - 9, gate[1] + 10):
    for x in range(gate[0] - 16, gate[0] + 17):
        r = ((x - gate[0]) / 16) ** 2 + ((y - gate[1]) / 9) ** 2
        if 0.62 < r <= 1.0 and grid[y][x] in '.,':
            grid[y][x] = '~'
# Islands of rock in the gate's lava, and the causeway in from the south.
for y in range(gate[1] + 4, gate[1] + 11):
    for x in range(gate[0] - 1, gate[0] + 2):
        if grid[y][x] == '~':
            grid[y][x] = '.'
# The Clutch: a bed of embers round the egg.
egg = [clutch[0] + 2, clutch[1] - 1]
for y in range(egg[1] - 2, egg[1] + 3):
    for x in range(egg[0] - 3, egg[0] + 4):
        if abs(x - egg[0]) + abs(y - egg[1]) <= 3 and grid[y][x] in '.,' and [x, y] != egg:
            grid[y][x] = 'e'
put(egg[0], egg[1], 'X')

# Glowing lava, braziers the Inquisitors set, the shrines.
for (x, y) in [(roost[0] + 3, roost[1]), (gallery[0] - 6, gallery[1]), (gate[0] - 13, gate[1]), (gate[0] + 13, gate[1]),
               (gate[0], gate[1] - 8), (gate[0] - 7, gate[1] + 7), (gate[0] + 7, gate[1] + 7)]:
    if grid[y][x] == '~':
        grid[y][x] = '!'
for (x, y) in [(vigil[0] - 4, vigil[1] - 3), (vigil[0] + 4, vigil[1] - 3), (vigil[0], vigil[1] + 4), (mouth[0] + 4, mouth[1] - 2),
               (clutch[0] - 5, clutch[1] + 3)]:
    if grid[y][x] in '.,':
        grid[y][x] = 'B'
shrine1 = [mouth[0] - 3, mouth[1] + 2]
shrine2 = [vigil[0] + 7, vigil[1] + 1]
for s in (shrine1, shrine2):
    put(*s, 'S')
# The Ember Gate: the door in the cavern's north wall, down into the Reliquary.
door = [gate[0], gate[1] - 6]
for y in range(2, door[1] + 1):
    for x in range(door[0] - 1, door[0] + 2):
        grid[y][x] = '='

extra = wall_tiles(grid, rnd)
rows = [''.join(r) for r in grid]
OK = ".,=X"
def cache(id, x, y, kind="Cache", facing=0):
    return {"id": id, "at": open_cell(rows, x, y, OK), "kind": kind, "facing": facing}
caches = [
    cache("the_mouth", mouth[0] + 5, mouth[1] + 2),
    cache("bone_gallery", gallery[0] + 6, gallery[1] - 4, facing=1),
    cache("the_roost", roost[0] - 7, roost[1] + 4),
    cache("the_vigil", vigil[0] - 6, vigil[1] + 2, facing=1),
    cache("the_clutch", clutch[0] + 6, clutch[1] + 3, kind="Reliquary"),
]
journal = open_cell(rows, vigil[0] + 2, vigil[1] - 4, OK)

N, C, E = "Normal", "Champion", "Elite"
HUSK, WRAITH, ANT, LION, INQ, ZEAL, CAPT, DRAKE = ("ash_husk", "cinder_wraith", "fire_ant", "tainted_antlion", "inquisitor",
                                                   "inquisitor_zealot", "inquisitor_captain", "cinder_drake")
spawns = [
    # The Mouth (level 18)
    pack((ANT, N, 5), at=[mouth[0], mouth[1] - 5]),
    # The Bone Gallery: the hive
    pack((ANT, C, 1, ["hasted"], LEADS), (ANT, N, 5), at=[gallery[0] + 2, gallery[1] + 2], radius=3),
    pack((LION, N, 2), (ANT, N, 2), at=[gallery[0] + 5, gallery[1] - 4]),
    pack((HUSK, N, 3), (WRAITH, N, 1), at=[(gallery[0] + vigil[0]) // 2, (gallery[1] + vigil[1]) // 2]),
    # The Roost
    pack((DRAKE, N, 3), at=[roost[0] - 5, roost[1] - 3], radius=2.5),
    pack((DRAKE, C, 1, ["vengeful"], LEADS), (DRAKE, N, 2), at=[roost[0] + 2, roost[1] + 5]),
    # The Vigil (level 19 from here)
    pack((CAPT, E, 1, ["shielded"], LEADS), (INQ, N, 2), (ZEAL, N, 2), at=[vigil[0], vigil[1]], radius=3, level=19),
    pack((ZEAL, N, 2), (WRAITH, N, 1), at=[vigil[0] - 6, vigil[1] - 4], level=19),
    # The Clutch
    pack((DRAKE, E, 1, ["tainted"], LEADS), (DRAKE, N, 2), at=[clutch[0] - 3, clutch[1] + 2], radius=2.5, level=19),
    pack((LION, C, 1, ["mirrored"], LEADS), (LION, N, 1), (ANT, N, 3), at=[(clutch[0] + gate[0]) // 2, (clutch[1] + gate[1]) // 2 + 4],
         level=19),
]
lg = legend({"!": {"ground": "lava", "light": 3.0, "lightColor": "#ff8a3a"}})
lg.update(extra)
zone = {
    "id": "wyrms_nest", "name": "The Wyrm's Nest", "level": 18, "act": 4, "revision": REVISION, "xpScale": 0.8,
    "tileset": "tileset_cinder",
    "tagline": "The mountain's heart is hollow, and something sleeps in it.",
    "darkness": 0.9, "heroLight": 5.5,
    "rows": rows,
    "legend": lg,
    "playerStart": start,
    "spawns": spawns,
    "shrines": [
        {"id": "nest_mouth_shrine", "name": "The Mouth of the Nest", "at": shrine1},
        {"id": "vigil_shrine", "name": "The Vigil", "at": shrine2},
    ],
    "exits": [
        {"id": "rivers", "label": "The Lava Rivers", "at": [mouth[0], H - 3], "radius": 1.5, "to": "lava_rivers", "toExit": "nest",
         "arrive": start},
        {"id": "reliquary", "label": "The Ember Gate", "at": [door[0], 3], "radius": 1.5, "to": "reliquary", "toExit": "gate",
         "arrive": [door[0], 6], "requires": {"act4.vorthrax": "slain"},
         "sealedText": "The Ember Gate is shut, and the heat behind it breathes. Something guards it that has not shown itself yet."},
    ],
    "caches": caches,
    "interactables": [
        {"id": "egg", "name": "A warm egg", "at": egg, "action": "egg", "dialogue": "nest_egg"},
        {"id": "vigil_orders", "name": "The Vigil's orders", "at": journal, "tile": 1276, "dialogue": "nest_vigil_orders"},
    ],
}
write(zone, sys.argv[1])
