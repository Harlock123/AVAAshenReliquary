# Generates zones/ember_gate.json: Act 4's hub (level 16), the Ember Gate camp. The last knights of the Order of the
# Pale Lantern hold a burned chapter-house on the ash plain under Mount Cindral, where the road comes down off the cliffs
# from the Bell Tower. The wastes lie north, toward the mountain and its gate.
#
# Its people: Warden Corwen (the act's story, and the Last Lantern: the beacon on the chapter-house's tower is hers to
# light or keep dark), Brannoc the smith (arms, armour and rune work; his forge hatches the wyrm's egg), Old Hesk (wares
# and the belt's supplies), the stash, the Lantern altar (respec) and the Ember Gate Shrine (a Rift Shrine).
# The tileset is tileset_cinder (see tools/cinder_common.py).
#
# Deterministic. Bump REVISION whenever the layout changes.
import sys
from cinder_common import Rng, legend, open_cell, write

REVISION = 1
W, H = 46, 44
rnd = Rng(16180)
grid = [['.'] * W for _ in range(H)]

def put(x, y, ch):
    grid[y][x] = ch

# The ash plain: spires and boulders round the edge, pebbles and bones scattered.
for y in range(H):
    for x in range(W):
        edge = min(x, W - 1 - x, y, H - 1 - y)
        if edge < 2 or (edge < 4 and rnd() < 0.55):
            grid[y][x] = 'C'
        elif rnd() < 0.03:
            grid[y][x] = 'o'
        elif rnd() < 0.08:
            grid[y][x] = ','

cx = W // 2
# The chapter-house: a ring of ruined wall with flagstones inside; gaps north and south.
x0, y0, x1, y1 = 9, 10, W - 10, 31
for y in range(y0, y1 + 1):
    for x in range(x0, x1 + 1):
        grid[y][x] = 'W' if x in (x0, x1) or y in (y0, y1) else '='
for (x, y) in [(x0, y0), (x1, y0), (x0, y1), (x1, y1)]:
    put(x, y, 'R')
for x in range(cx - 1, cx + 2):
    put(x, y0, '=')
    put(x, y1, '=')
# Broken places in the wall, where the Prophet's fire took it.
for (x, y) in [(x0, 15), (x0, 16), (x1, 24), (x1 + 0, 25), (x0 + 6, y1), (x1 - 4, y0)]:
    put(x, y, '.')
# The road: down the cliffs from the south, through the house, out north toward the wastes.
for y in list(range(1, y0)) + list(range(y1 + 1, H - 1)):
    for x in range(cx - 1, cx + 2):
        grid[y][x] = '='
    for x in (cx - 2, cx + 2):
        if grid[y][x] in '.,o':
            grid[y][x] = ','
# The beacon tower in the north-east corner: a square of pillars round the beacon.
bx, by = x1 - 4, y0 + 4
for (dx, dy) in [(-1, -1), (1, -1), (-1, 1), (1, 1)]:
    put(bx + dx, by + dy, 'R')
beacon = [bx, by]
put(*beacon, 'X')
# The forge in the south-east: braziers and a stone table.
forge = [x1 - 4, y1 - 4]
put(forge[0] - 1, forge[1] - 1, 'B')
put(forge[0] + 1, forge[1] - 1, 'B')
put(forge[0], forge[1] - 2, 'k')
# The Warden's table in the north-west, with lecterns.
put(x0 + 4, y0 + 3, 't')
put(x0 + 3, y0 + 2, 'L')
put(x0 + 6, y0 + 2, 'L')
# Braziers at the doors; barrels and crates; the shrine in the court.
for (x, y) in [(cx - 2, y0 + 1), (cx + 2, y0 + 1), (cx - 2, y1 - 1), (cx + 2, y1 - 1)]:
    put(x, y, 'B')
for (x, y) in [(x0 + 2, y1 - 2), (x0 + 3, y1 - 2), (x0 + 2, y1 - 3), (x1 - 2, y0 + 8)]:
    put(x, y, 'k')
shrine = [cx - 5, 21]
put(*shrine, 'S')
# Outside: a few pallets where the knights sleep, braziers on the road.
for (x, y) in [(5, 36), (8, 38), (37, 36), (40, 39)]:
    put(x, y, 'p')
for (x, y) in [(cx - 2, 37), (cx + 2, 6)]:
    put(x, y, 'B')

rows = [''.join(r) for r in grid]
legend_ = legend({
    "k": {"ground": "ash", "tiles": [1024, 1025], "objects": [1285, 1286, 1287, 162, 163], "blocks": True},
    "t": {"ground": "ash", "tiles": [1024], "objects": [1281, 1282], "blocks": True},
    "L": {"ground": "ash", "tiles": [1024], "objects": [1274, 1275], "blocks": True},
    "p": {"ground": "ash", "objects": [224, 225, 226, 227]},
})
start = [cx, H - 5]
zone = {
    "id": "ember_gate", "name": "The Ember Gate Camp", "level": 16, "act": 4, "revision": REVISION, "tileset": "tileset_cinder",
    "town": True, "tagline": "The last lanterns of the Order burn at the foot of the burning mountain.",
    "darkness": 0.5,
    "rows": rows,
    "legend": legend_,
    "playerStart": start,
    "spawns": [],
    "shrines": [{"id": "ember_gate_shrine", "name": "The Ember Gate Shrine", "at": shrine}],
    "npcs": [
        {"id": "corwen", "name": "Warden Corwen", "title": "Warden of the Last Lantern", "at": [x0 + 4, y0 + 5], "facing": 2,
         "portrait": "flare/female04", "dialogue": "ember_corwen",
         "look": {"head": "plate_helm", "chest": "plate_cuirass", "hands": "plate_gauntlets", "legs": "plate_greaves", "feet": "plate_boots",
                  "main": "longsword", "off": "kite_shield"}},
    ],
    "interactables": [
        {"id": "stash", "name": "Stash", "at": open_cell(rows, cx + 4, 25), "tile": 1264, "action": "stash"},
        {"id": "altar", "name": "Lantern altar", "at": open_cell(rows, cx + 5, 14), "tile": 1293, "dialogue": "ember_altar"},
        # Malachar's Daughter (M5 step 3): a child's grave under the beacon.
        {"id": "daughter_ember_gate", "name": "A small grave", "at": open_cell(rows, bx - 3, by + 3), "dialogue": "daughter_ember_gate",
         "action": "keepsake", "blocks": False},
        {"id": "beacon", "name": "The beacon", "at": beacon, "tile": 1295, "action": "beacon", "dialogue": "ember_beacon"},
    ],
    "vendors": [
        {"id": "brannoc", "name": "Brannoc", "title": "Smith", "at": [forge[0], forge[1] + 1], "facing": 5, "stockSize": 12,
         "sells": ["weapons", "firearms", "armour"], "runework": True,
         "greeting": "The mountain's hot enough to forge in, if you stand close. I'd rather not. What needs mending?",
         "look": {"head": "leather_hood", "chest": "leather_chest", "hands": "leather_gloves", "legs": "leather_pants", "feet": "leather_boots",
                  "main": "war_hammer"}},
        {"id": "hesk", "name": "Old Hesk", "title": "Quartermaster", "at": open_cell(rows, x0 + 4, y1 - 4), "facing": 6, "stockSize": 12,
         "greeting": "Water, salt, powder and bombs. Ash in all of it, I'm afraid.",
         "supplies": ["alchemists_fire", "frost_vial", "bottled_lightning", "acid_flask", "antidote", "incendiary_round", "silver_round",
                      "scatter_round", "black_powder", "lead_shot", "gun_parts"],
         "look": {"head": "leather_hood", "chest": "mage_vest_alt2", "hands": "cloth_gloves", "legs": "cloth_pants", "feet": "leather_boots", "main": "rod"}},
    ],
    "exits": [
        {"id": "cliff_road", "label": "The Cliff Road", "at": [cx, H - 2], "radius": 1.5, "to": "bell_tower", "toExit": "cliff_road",
         "arrive": start},
        {"id": "wastes", "label": "The Ash Wastes", "at": [cx, 1], "radius": 1.5, "to": "ash_wastes", "toExit": "camp",
         "arrive": [cx, 5]},
    ],
}
write(zone, sys.argv[1])
