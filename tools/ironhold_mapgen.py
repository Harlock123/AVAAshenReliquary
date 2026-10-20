# Generates zones/ironhold.json: Act 2's hub, the inner ward of Ironhold behind its outer wall, at the north end of the
# Iron March. Ser Hadric's dead besiege the city; its gates are shut but for the south one, to the March.
#
# Its people: Captain Oswin Thale of the Ironhold Guard by the south gate, Hilde the smith (arms, armour, rune work
# and crafting), Sable the Brothers of the Ferry's fence (whose prices and stock follow Act 1's ferry choice, the
# "Brothers of the Ferry" payoff) and Archivist Pell by the founders' statues; the stash, the Founders' altar (respec)
# and the Ward Shrine (a Rift Shrine). Refugee tents fill the west yard. The north gate, to the Barrow Fields, is barred
# for now (M4 step 3). The tileset is tileset_ironhold: Flare's grassland plus rubberduck's Old Ruins (ruins tile n is
# n + 1000).
#
# Deterministic. Bump REVISION whenever the layout changes.
import json, sys

REVISION = 1
W, H = 40, 38
grid = [['T'] * W for _ in range(H)]
seed = 97531
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

def put(x, y, ch):
    grid[y][x] = ch

# The ward: flagstones inside a ring of dressed stone, towers at the corners.
x0, y0, x1, y1 = 4, 3, W - 5, H - 5
for y in range(y0, y1 + 1):
    for x in range(x0, x1 + 1):
        if x in (x0, x1) or y in (y0, y1):
            grid[y][x] = 'W'
        else:
            grid[y][x] = ',' if rnd() < 0.12 else '.'
for (x, y) in [(x0, y0), (x1, y0), (x0, y1), (x1, y1)]:
    grid[y][x] = 'R'
cx = W // 2

# The south gate (to the Iron March) and the road in, out through the wall to the edge of the map.
for y in range(y1, H):
    for x in range(cx - 1, cx + 2):
        grid[y][x] = '='
grid[y1][cx - 2] = 'Q'
grid[y1][cx + 2] = 'Q'
for y in range(y1 + 1, H):
    grid[y][cx - 2] = 'T'
    grid[y][cx + 2] = 'T'
# The main street north through the ward to the barred north gate.
for y in range(y0, y1):
    for x in range(cx - 1, cx + 2):
        grid[y][x] = '='
grid[y0][cx - 1] = 'G'; grid[y0][cx] = 'G'; grid[y0][cx + 1] = 'G'
grid[y0][cx - 2] = 'Q'
grid[y0][cx + 2] = 'Q'
# A cross street.
my = 17
for x in range(x0 + 1, x1):
    grid[my][x] = '='

# The market square (east of the crossing): stalls and crates.
for (x, y) in [(cx + 5, my - 4), (cx + 9, my - 4)]:
    put(x, y, 'm')
for (x, y) in [(cx + 12, my - 2), (cx + 12, my - 1), (cx + 4, my + 3)]:
    put(x, y, 'k')
# The founders' statues, north-west of the crossing, with the archivist below them.
for (x, y) in [(cx - 9, my - 6), (cx - 6, my - 6), (cx - 3, my - 6)]:
    put(x, y, 'Z')
# The forge (south-east): Hilde's anvil and fire.
put(cx + 9, my + 6, 'a')
put(cx + 11, my + 6, 'c')
# The barracks yard (north-east): a walled yard of the Guard, its door on the main street.
for x in range(cx + 4, x1):
    put(x, y0 + 5, 'W')
for y in range(y0 + 1, y0 + 5):
    put(cx + 4, y, 'W')
put(cx + 4, y0 + 3, '.')                                # the barracks door
for (x, y) in [(cx + 7, y0 + 2), (cx + 10, y0 + 2), (cx + 13, y0 + 2)]:
    put(x, y, 'r')                                      # weapon racks (Flare's ruins lecterns stand in)
# The refugee yard (south-west): tents and a fire.
for (x, y) in [(x0 + 2, my + 4), (x0 + 6, my + 4), (x0 + 2, my + 9), (x0 + 6, my + 10)]:
    put(x, y, 't'); put(x + 1, y, 'X'); put(x, y + 1, 'X'); put(x + 1, y + 1, 'X')
put(x0 + 5, my + 7, 'c')
# The well, in the refugee yard.
put(x0 + 9, my + 6, 'w')
# Braziers along the main street.
for (x, y) in [(cx - 3, y0 + 4), (cx + 3, y0 + 8), (cx - 3, y1 - 4), (cx + 3, y1 - 3)]:
    put(x, y, 'B')
shrine = [cx - 6, my + 1]
put(*shrine, 'S')

rows = [''.join(r) for r in grid]
FLAG = [1024, 1025, 1026, 1027, 1028, 1029, 1030, 1031]
WORN = [1074, 1075, 1076, 1077, 1078]                 # grass in the cracks (ruins 153-166 are wall tops, drawn raised)
gate = [cx, H - 2]
zone = {
    "id": "ironhold", "name": "Ironhold", "level": 6, "act": 2, "revision": REVISION, "tileset": "tileset_ironhold",
    "town": True, "tagline": "The last city of the Iron March holds.",
    "rows": rows,
    "legend": {
        ".": {"ground": "grass", "tiles": FLAG},
        ",": {"ground": "grass", "tiles": WORN},
        "=": {"ground": "path", "tiles": FLAG},
        "T": {"ground": "grass", "objects": [248, 249, 250, 251], "blocks": True, "blocksSight": True},
        "W": {"ground": "grass", "objects": [1223, 1224, 1225, 1226, 1227, 1228, 1229], "blocks": True, "blocksSight": True},
        "R": {"ground": "grass", "objects": [1185, 1186], "blocks": True, "blocksSight": True},
        "Q": {"ground": "grass", "objects": [1230], "blocks": True, "blocksSight": True},
        "G": {"ground": "path", "tiles": FLAG, "objects": [1362, 1365], "blocks": True},
        "m": {"ground": "grass", "tiles": FLAG, "objects": [1281, 1282, 1283, 1284], "blocks": True},
        "k": {"ground": "grass", "tiles": FLAG, "objects": [1285, 1286, 1287, 1288], "blocks": True},
        "Z": {"ground": "grass", "tiles": FLAG, "objects": [1241, 1242, 1243, 1244], "blocks": True},
        "a": {"ground": "grass", "tiles": FLAG, "objects": [103], "blocks": True},
        "c": {"ground": "grass", "tiles": FLAG, "objects": [102], "blocks": True},
        "r": {"ground": "grass", "tiles": FLAG, "objects": [1274, 1275], "blocks": True},
        "t": {"ground": "grass", "objects": [72, 75], "blocks": True, "blocksSight": True},
        "X": {"ground": "grass", "blocks": True},
        "w": {"ground": "grass", "tiles": FLAG, "objects": [1289], "blocks": True},
        "B": {"ground": "grass", "tiles": FLAG, "objects": [102], "blocks": True, "light": 4.0},
        "S": {"ground": "grass", "tiles": FLAG, "objects": [265]},
    },
    "playerStart": [cx, H - 7],
    "spawns": [],
    "shrines": [{"id": "ward_shrine", "name": "The Ward Shrine", "at": shrine}],
    "npcs": [
        {"id": "thale", "name": "Captain Oswin Thale", "title": "Ironhold Guard", "at": [cx + 3, H - 8], "facing": 7,
         "portrait": "flare/male07", "dialogue": "ironhold_thale",
         "look": {"head": "plate_helm", "chest": "plate_cuirass", "hands": "plate_gauntlets", "legs": "plate_greaves",
                  "feet": "plate_boots", "main": "longsword", "off": "kite_shield"}},
        {"id": "pell", "name": "Archivist Pell", "title": "Keeper of the founders' rolls", "at": [cx - 6, my - 4], "facing": 3,
         "portrait": "flare/male03", "dialogue": "ironhold_pell",
         "look": {"head": "mage_hood", "chest": "mage_vest", "hands": "mage_sleeves", "legs": "mage_skirt", "feet": "mage_boots",
                  "main": "staff"}},
    ],
    "interactables": [
        {"id": "stash", "name": "Stash", "at": [cx - 3, my + 1], "tile": 76, "action": "stash"},
        {"id": "altar", "name": "Founders' altar", "at": [cx - 9, my - 4], "tile": 1293, "dialogue": "ironhold_altar"},
    ],
    "vendors": [
        {"id": "hilde", "name": "Hilde", "title": "Smith", "at": [cx + 8, my + 5], "facing": 5, "stockSize": 12,
         "sells": ["weapons", "armour"], "runework": True,
         "greeting": "Ironhold steel. Bring me a blade and I'll etch it, or strip the runes off your old one.",
         "look": {"head": "chain_coif", "chest": "leather_chest", "hands": "leather_gloves", "legs": "leather_pants",
                  "feet": "leather_boots", "main": "war_hammer"}},
        {"id": "sable", "name": "Sable", "title": "Fence", "at": [cx + 7, my - 3], "facing": 5, "stockSize": 12,
         "greeting": "I sell to anyone with coin. Don't ask where it came from.",
         "supplies": ["alchemists_fire", "frost_vial", "bottled_lightning", "acid_flask", "antidote"],
         "terms": [
             {"requires": {"act1.ferry": "brothers"}, "priceScale": 0.8, "rares": 2,
              "greeting": "A Brother of the Ferry! Coll's word came upriver ahead of you. The good shelf, and a friend's price."},
             {"requires": {"act1.ferry": "lantern"}, "priceScale": 1.25,
              "greeting": "The Lantern's dog. Coll's word came upriver about you too. You'll pay what the Lantern's coin is worth to me."},
         ],
         "look": {"head": "leather_hood", "chest": "leather_chest", "hands": "cloth_gloves", "legs": "cloth_pants",
                  "feet": "leather_boots", "main": "dagger"}},
    ],
    "exits": [
        {"id": "march_gate", "label": "The Iron March", "at": gate, "radius": 1.5, "to": "iron_march", "toExit": "ironhold",
         "arrive": [cx, H - 7]},
        {"id": "north_gate", "label": "The Barrow Fields", "at": [cx, y0 + 1], "radius": 1.5,
         "requires": {"act2.north_gate": "open"},
         "sealedText": "The Guard keeps the north gate barred. (The Barrow Fields are still being written.)"},
    ],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
