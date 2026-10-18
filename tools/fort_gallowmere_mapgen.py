# Generates zones/fort_gallowmere.json: Act 1's hub. For now (M2 step 1) a walled courtyard behind the gate at the
# north end of Gallows Road, with a Rift Shrine; step 3 fills it with the hub's people. Deterministic.
import json, sys

REVISION = 1
W, H = 30, 28
grid = [['.'] * W for _ in range(H)]
seed = 7654321
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

# Forest all round, then the palisade: a fence ring with stone posts at the corners and a gate in the south wall.
x0, y0, x1, y1 = 4, 3, W - 5, H - 4
for y in range(H):
    for x in range(W):
        if not (x0 <= x <= x1 and y0 <= y <= y1):
            grid[y][x] = 'T' if rnd() < 0.8 else 'O'
for x in range(x0, x1 + 1):
    grid[y0][x] = 'F'
    grid[y1][x] = 'F'
for y in range(y0, y1 + 1):
    grid[y][x0] = 'L'
    grid[y][x1] = 'L'
for (x, y) in [(x0, y0), (x1, y0), (x0, y1), (x1, y1)]:
    grid[y][x] = 'P'

# The road in from the south gate, up through the yard.
cx = W // 2
for y in range(y0 + 2, H):
    for x in range(cx - 1, cx + 2):
        grid[y][x] = '='
    for x in (cx - 2, cx + 2):
        if grid[y][x] in '.':
            grid[y][x] = '-'
# The gate: open now (the refugees opened it), with stone posts either side; outside, the road runs on south.
for x in range(cx - 1, cx + 2):
    grid[y1][x] = '='
grid[y1][cx - 2] = 'P'
grid[y1][cx + 2] = 'P'
for y in range(y1 + 1, H):
    for x in range(cx - 2, cx + 3):
        grid[y][x] = '=' if abs(x - cx) <= 1 else '-'

# The yard: a campfire, tents and crates for now.
grid[11][cx] = 'c'
for (x, y) in [(x0 + 3, y0 + 3), (x1 - 4, y0 + 3), (x0 + 3, y1 - 6)]:
    grid[y][x] = 't'; grid[y][x + 1] = 'X'; grid[y + 1][x] = 'X'; grid[y + 1][x + 1] = 'X'
for (x, y) in [(x1 - 3, y1 - 4), (x1 - 2, y1 - 4), (x0 + 2, y0 + 8)]:
    grid[y][x] = 'x'
for (x, y) in [(x0 + 2, y0 + 1), (x1 - 2, y1 - 2), (x0 + 7, y1 - 2)]:
    grid[y][x] = ','
shrine = [cx + 4, 14]
grid[shrine[1]][shrine[0]] = 'S'

rows = [''.join(r) for r in grid]
gate = [cx, H - 2]
zone = {
    "id": "fort_gallowmere", "name": "Fort Gallowmere", "level": 1, "revision": REVISION, "tileset": "tileset_grassland",
    "town": True, "tagline": "The refugees open the gate.",
    "rows": rows,
    "legend": {
        ".": {"ground": "grass"},
        ",": {"ground": "grass", "objects": [112, 113, 114, 115, 116, 117, 118, 120, 121, 122, 123, 124, 125, 126, 127]},
        "=": {"ground": "path"},
        "-": {"ground": "path_edge"},
        "T": {"ground": "grass", "objects": [248, 249, 250, 251], "blocks": True, "blocksSight": True},
        "O": {"ground": "grass", "objects": [252, 253, 254, 255], "blocks": True, "blocksSight": True},
        "c": {"ground": "path", "objects": [102], "blocks": True},
        "t": {"ground": "grass", "objects": [72, 75], "blocks": True, "blocksSight": True},
        "x": {"ground": "grass", "objects": [96, 97, 98, 99, 100, 101], "blocks": True},
        "S": {"ground": "grass", "objects": [265]},
        "X": {"ground": "grass", "blocks": True},
        "P": {"ground": "grass", "objects": [132], "blocks": True, "blocksSight": True},
        "F": {"ground": "grass", "objects": [107], "blocks": True},
        "L": {"ground": "grass", "objects": [104], "blocks": True},
    },
    "playerStart": [cx, H - 6],
    "spawns": [],
    "shrines": [{"id": "fort_shrine", "name": "The Fort Shrine", "at": shrine}],
    "npcs": [
        {"id": "brask", "name": "Sergeant Brask", "title": "Gate warden", "at": [cx + 2, H - 7], "facing": 7,
         "portrait": "flare/male05", "dialogue": "fort_brask",
         "look": {"head": "chain_coif", "chest": "chain_cuirass", "hands": "chain_gloves", "legs": "chain_greaves",
                  "feet": "chain_boots", "main": "shortsword"}},
    ],
    "interactables": [
        {"id": "notice_board", "name": "Notice board", "at": [cx - 4, 16], "tile": 139, "dialogue": "fort_notice"},
    ],
    "exits": [{"id": "gate", "label": "Gallows Road", "at": gate, "radius": 1.5,
               "to": "gallows_road", "toExit": "fort_gallowmere", "arrive": [cx, H - 6]}],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
