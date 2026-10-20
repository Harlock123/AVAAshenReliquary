# Generates zones/saint_oriels_rest.json: Act 3's hub, the Rest of the Monastery of Saint Oriel, a walled cloister on a
# snowy ledge under frozen cliffs. The Choir of Whispers, the third Herald, sings in the Bell Tower above it.
#
# Its people: Abbess Ilse (the act's story), Brother Caddoc (the companion of the Penitent's story, M4 step 6; met here),
# Halvard the smith (arms, armour, rune work and crafting), Sister Mora (wares and the belt's supplies), the stash, the
# Cloister altar (respec) and the Cloister Shrine (a Rift Shrine). Pilgrims' tents stand outside the wall. The south road
# goes down to the Siege Walls (the Frostbitten Pass will lie between, step 6); the north door, up toward the Hollow
# Woods and the Bell Tower, is shut for now. The tileset is tileset_oriel: Flare's snowplains plus rubberduck's Old Ruins
# (ruins tile n is n + 1000).
#
# Deterministic. Bump REVISION whenever the layout changes.
import json, sys

REVISION = 1
W, H = 44, 42
grid = [['.'] * W for _ in range(H)]
seed = 31415
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

def put(x, y, ch):
    grid[y][x] = ch

# Frozen cliffs behind (north) and at the sides; dead trees and snow-rocks scattered on the ledge.
for y in range(H):
    for x in range(W):
        edge = min(x, W - 1 - x, y)
        if y < 4 or edge < 2:
            grid[y][x] = 'C' if (y < 4 or rnd() < 0.7) else 'T'
        elif rnd() < 0.04:
            grid[y][x] = 'T'
        elif rnd() < 0.05:
            grid[y][x] = ','

# The cloister: a ring of dressed stone with flagstones inside.
x0, y0, x1, y1 = 8, 6, W - 9, 26
for y in range(y0, y1 + 1):
    for x in range(x0, x1 + 1):
        grid[y][x] = 'W' if x in (x0, x1) or y in (y0, y1) else ('_' if rnd() < 0.12 else 'f')
for (x, y) in [(x0, y0), (x1, y0), (x0, y1), (x1, y1)]:
    grid[y][x] = 'R'
cx = W // 2
# Its south door, and the road down the ledge to the edge of the map.
for x in range(cx - 1, cx + 2):
    grid[y1][x] = '='
grid[y1][cx - 2] = 'Q'; grid[y1][cx + 2] = 'Q'
for y in range(y1 + 1, H):
    for x in range(cx - 1, cx + 2):
        grid[y][x] = '='
    for x in (cx - 2, cx + 2):
        if grid[y][x] in '.,T':
            grid[y][x] = '-'
# The north door, toward the cliff stair (shut for now).
for x in range(cx - 1, cx + 2):
    grid[y0][x] = 'G'
grid[y0][cx - 2] = 'Q'; grid[y0][cx + 2] = 'Q'
for y in range(1, y0):
    for x in range(cx - 1, cx + 2):
        grid[y][x] = '='
# The cloister walk: a flagstone path round the court.
for x in range(x0 + 2, x1 - 1):
    grid[y0 + 2][x] = '='
    grid[y1 - 2][x] = '='
for y in range(y0 + 2, y1 - 1):
    grid[y][x0 + 2] = '='
    grid[y][x1 - 2] = '='
for y in range(y0 + 1, y1):
    grid[y][cx] = '='

# The guest hall (Flare's timber lodge) in the north-west corner of the court; the bell tower's base in the north-east.
hall = [x0 + 5, y0 + 5]
for dy in range(-1, 2):
    for dx in range(-1, 2):
        put(hall[0] + dx, hall[1] + dy, 'X')
put(*hall, 'H')
for (x, y) in [(x1 - 6, y0 + 3), (x1 - 4, y0 + 3), (x1 - 6, y0 + 5), (x1 - 4, y0 + 5)]:
    put(x, y, 'P')                                     # the bell tower's corner piers
# Statues of the abbesses along the south walk; braziers at the doors.
for x in (cx - 8, cx - 5, cx + 5, cx + 8):
    put(x, y1 - 3, 'Z')
for (x, y) in [(cx - 3, y1 - 1), (cx + 3, y1 - 1), (cx - 3, y0 + 1), (cx + 3, y0 + 1)]:
    put(x, y, 'B')
# The cloister garth's well.
put(cx + 4, 16, 'w')
shrine = [cx - 4, 16]
put(*shrine, 'S')

# Outside the wall: pilgrims' tents and a fire on the ledge, a cart, crates.
for (x, y) in [(5, 30), (10, 33), (31, 30), (36, 33)]:
    put(x, y, 't'); put(x + 1, y, 'X'); put(x, y + 1, 'X'); put(x + 1, y + 1, 'X')
put(8, 31, 'c')
put(34, 31, 'c')
put(14, 36, 'k'); put(15, 36, 'k'); put(29, 36, 'a')

rows = [''.join(r) for r in grid]
FLAG = [1024, 1025, 1026, 1027, 1028, 1029, 1030, 1031]
WORN = [1074, 1075, 1076, 1077, 1078]
gate = [cx, H - 2]
zone = {
    "id": "saint_oriels_rest", "name": "Saint Oriel's Rest", "level": 11, "act": 3, "revision": REVISION, "tileset": "tileset_oriel",
    "town": True, "tagline": "The bells have not rung since the Choir began to sing.",
    "rows": rows,
    "legend": {
        ".": {"ground": "grass"},
        ",": {"ground": "grass", "objects": [112, 113, 116, 117, 120, 121, 124, 125]},
        "=": {"ground": "path", "tiles": FLAG},
        "-": {"ground": "path_edge"},
        "f": {"ground": "grass", "tiles": FLAG},
        "_": {"ground": "grass", "tiles": WORN},
        "C": {"ground": "grass", "objects": [48, 49, 52, 53, 56, 60, 64, 68], "blocks": True, "blocksSight": True},
        "T": {"ground": "grass", "objects": [244, 245, 246, 247], "blocks": True, "blocksSight": True},
        "W": {"ground": "grass", "objects": [1223, 1224, 1225, 1226, 1227, 1228, 1229], "blocks": True, "blocksSight": True},
        "R": {"ground": "grass", "objects": [1185, 1186], "blocks": True, "blocksSight": True},
        "Q": {"ground": "grass", "objects": [1230], "blocks": True, "blocksSight": True},
        "P": {"ground": "grass", "tiles": FLAG, "objects": [1223, 1226, 1228], "blocks": True, "blocksSight": True},
        "G": {"ground": "path", "tiles": FLAG, "objects": [1362, 1365], "blocks": True},
        "H": {"ground": "grass", "tiles": FLAG, "objects": [744], "blocks": True, "blocksSight": True},
        "X": {"ground": "grass", "tiles": FLAG, "blocks": True},
        "Z": {"ground": "grass", "tiles": FLAG, "objects": [1241, 1242, 1243, 1244], "blocks": True},
        "B": {"ground": "grass", "tiles": FLAG, "objects": [102], "blocks": True, "light": 4.0},
        "w": {"ground": "grass", "tiles": FLAG, "objects": [1289], "blocks": True},
        "S": {"ground": "grass", "tiles": FLAG, "objects": [265]},
        "t": {"ground": "grass", "objects": [72, 75], "blocks": True, "blocksSight": True},
        "c": {"ground": "grass", "objects": [102], "blocks": True, "light": 3.5},
        "k": {"ground": "grass", "objects": [96, 97, 98, 99], "blocks": True},
        "a": {"ground": "grass", "objects": [540, 541], "blocks": True},
    },
    "playerStart": [cx, H - 6],
    "spawns": [],
    "shrines": [{"id": "cloister_shrine", "name": "The Cloister Shrine", "at": shrine}],
    "npcs": [
        {"id": "ilse", "name": "Abbess Ilse", "title": "Abbess of Saint Oriel", "at": [cx + 2, y0 + 4], "facing": 2,
         "portrait": "flare/female09", "dialogue": "oriel_ilse",
         "look": {"head": "mage_hood", "chest": "mage_vest", "hands": "mage_sleeves", "legs": "mage_skirt", "feet": "mage_boots", "main": "greatstaff"}},
        {"id": "caddoc", "name": "Brother Caddoc", "title": "Penitent", "at": [cx - 6, y1 - 5], "facing": 3,
         "portrait": "flare/male10", "dialogue": "oriel_caddoc",
         "look": {"head": "leather_hood", "chest": "mage_vest", "hands": "cloth_gloves", "legs": "cloth_pants", "feet": "cloth_sandals", "main": "staff"}},
    ],
    "interactables": [
        {"id": "stash", "name": "Stash", "at": [cx - 3, 19], "tile": 1264, "action": "stash"},
        {"id": "altar", "name": "Cloister altar", "at": [cx + 4, y0 + 2 + 1], "tile": 1293, "dialogue": "oriel_altar"},
    ],
    "vendors": [
        {"id": "halvard", "name": "Halvard", "title": "Smith", "at": [x1 - 4, y1 - 5], "facing": 5, "stockSize": 12,
         "sells": ["weapons", "armour"], "runework": True,
         "greeting": "Cold steel's the only kind up here. I can etch it, and I can strip the runes off your old one.",
         "look": {"head": "chain_coif", "chest": "leather_chest", "hands": "leather_gloves", "legs": "leather_pants", "feet": "leather_boots",
                  "main": "war_hammer"}},
        {"id": "mora", "name": "Sister Mora", "title": "Cellarer", "at": [x0 + 4, y1 - 5], "facing": 6, "stockSize": 12,
         "greeting": "Bread, salt, bombs and blessings. The pass takes all four.",
         "supplies": ["alchemists_fire", "frost_vial", "bottled_lightning", "acid_flask", "antidote"],
         "look": {"head": "mage_hood", "chest": "mage_vest", "hands": "cloth_gloves", "legs": "mage_skirt", "feet": "cloth_sandals", "main": "rod"}},
    ],
    "exits": [
        {"id": "south_road", "label": "The Siege Walls", "at": gate, "radius": 1.5, "to": "siege_walls", "toExit": "heights",
         "arrive": [cx, H - 6]},
        {"id": "north_door", "label": "The Hollow Woods", "at": [cx, y0 + 1], "radius": 1.5,
         "requires": {"act3.north_door": "open"},
         "sealedText": "The north door to the cliff stair is barred from within. (The Hollow Woods are still being written.)"},
    ],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
