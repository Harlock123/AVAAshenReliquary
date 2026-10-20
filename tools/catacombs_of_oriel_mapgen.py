# Generates zones/catacombs_of_oriel.json: Act 3's third zone (levels 13-14), a dungeon: the monastery's catacombs under
# the Hollow Woods, where the abbots of Saint Oriel lie and the Choir of Whispers now keeps its acolytes. Deterministic:
# no randomness beyond a fixed LCG.
#
# The catacombs, top (north) to bottom: the stair hall (the way down from the Hollow Woods, a shrine), the long ossuary
# (bone-shelved galleries), the Choir's chapel to the west (acolytes in white), the penitents' cells to the east (Brother
# Caddoc waits in the last one to make his confession: the Penitent's story), the abbots' hall (a second shrine), the
# abbots' crypt (the Hollow Abbot, a named guardian, and the reliquary), and at the south end the stair up to the Bell
# Tower (a third shrine), barred until step 7. Three burned pages of the Library of Ash lie here.
#
# Act 3's third mechanic: domination. The Choir's acolytes turn the hero's allies against them for a while, or with no
# ally to turn, frighten the hero.
#
# Walls as in the Drowned Chapel: a solid cell next to floor shows a face toward it; the far walls stand full height,
# the near walls are low. Dark: torches and braziers light it; the hero carries a small light.
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import json, sys

REVISION = 1
W, H = 62, 96
grid = [['#'] * W for _ in range(H)]
seed = 112358
def rnd():
    global seed
    seed = (seed * 1103515245 + 12345) & 0x7fffffff
    return seed / 0x7fffffff

def room(x0, y0, x1, y1, ch='.'):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            grid[y][x] = ',' if ch == '.' and rnd() < 0.18 else ch
def put(x, y, ch):
    grid[y][x] = ch

# --- The rooms and the passages between them ---
room(25, 3, 36, 10)                                    # the stair hall
room(29, 11, 32, 14)
room(14, 15, 47, 20)                                   # the long ossuary, upper gallery
room(14, 21, 17, 30)                                   # its west and east galleries
room(44, 21, 47, 30)
room(14, 31, 47, 35)                                   # its lower gallery
room(29, 21, 32, 30)                                   # the central walk
room(8, 17, 13, 19)                                    # to the Choir's chapel
room(2, 12, 7, 40)                                     # the Choir's chapel
room(48, 17, 52, 19)                                   # to the penitents' cells
room(53, 12, 59, 40)                                   # the cells' corridor
room(29, 36, 32, 41)                                   # south to the abbots' hall
room(16, 42, 45, 54)                                   # the abbots' hall
room(3, 41, 7, 43)                                     # the chapel's back stair down to the hall
room(3, 44, 15, 47)
room(53, 41, 57, 47)                                   # the cells' back way to the hall
room(46, 46, 52, 48)
room(29, 55, 32, 60)                                   # down to the crypt
room(14, 61, 47, 80)                                   # the abbots' crypt
room(29, 81, 32, 86)                                   # the stair up to the Bell Tower
room(25, 87, 36, 92)

# --- Furniture ---
for x in range(16, 46, 3):                             # the ossuary's bone shelves
    if x not in (29, 30, 31, 32):
        put(x, 15, 'i'); put(x, 35, 'i')
for y in range(22, 30, 3):
    put(14, y, 'i'); put(47, y, 'i')
for y in range(14, 40, 4):                             # the chapel's pews
    put(4, y, 'p'); put(5, y, 'p')
put(4, 13, 'Y')                                        # the Choir's altar
for y in range(13, 40, 5):                             # the cells' cots, along the east wall
    put(59, y, 'd')
for (x, y) in [(19, 44), (42, 44), (19, 52), (42, 52)]:
    put(x, y, 'I')                                     # the abbots' hall's pillars
for row in (65, 70, 75):                               # the abbots' tombs in ranks
    for x in list(range(18, 27, 4)) + list(range(35, 44, 4)):
        put(x, row, 'u')
for (x, y) in [(27, 5), (34, 5), (16, 17), (45, 17), (3, 15), (3, 37), (55, 14), (55, 38), (18, 43), (43, 43), (16, 62), (45, 62),
               (16, 79), (45, 79), (27, 89), (34, 89)]:
    put(x, y, 'Z')                                     # braziers
for (x, y) in [(20, 18), (39, 33), (45, 26), (6, 27), (57, 30), (25, 48), (37, 50), (22, 72), (40, 67), (30, 77), (10, 46)]:
    put(x, y, 'q')                                     # bones
shrine1, shrine2, shrine3 = [33, 8], [24, 47], [33, 90]
for s in (shrine1, shrine2, shrine3):
    put(*s, 'S')

# --- Walls ---
def floor(x, y):
    return 0 <= x < W and 0 <= y < H and grid[y][x] not in '#'
out = [row[:] for row in grid]
for y in range(H):
    for x in range(W):
        if grid[y][x] != '#':
            continue
        sw, se, ne, nw = floor(x, y + 1), floor(x + 1, y), floor(x, y - 1), floor(x - 1, y)
        if sw and se:
            out[y][x] = 'C'
        elif sw:
            out[y][x] = 'A'
        elif se:
            out[y][x] = 'B'
        elif floor(x + 1, y + 1):
            out[y][x] = 'D'
        elif ne and nw:
            out[y][x] = 'O'
        elif ne:
            out[y][x] = 'L'
        elif nw:
            out[y][x] = 'M'
        elif floor(x - 1, y - 1):
            out[y][x] = 'N'
grid = out

# Dress the far walls: torches every few cells, niches of skulls, cracks.
for y in range(H):
    for x in range(W):
        c = grid[y][x]
        if c not in 'AB':
            continue
        r = rnd()
        if (x * 5 + y * 3) % 8 == 0:
            grid[y][x] = 'a' if c == 'A' else 'b'
        elif r < 0.14:
            grid[y][x] = 'e' if c == 'A' else 'f'
        elif r < 0.24:
            grid[y][x] = 'g' if c == 'A' else 'h'
        elif r < 0.30:
            grid[y][x] = 'm' if c == 'A' else 'n'
grid[2][30] = 'H'                                      # the stair up to the Hollow Woods
grid[93][30] = 'H'                                     # the stair up to the Bell Tower

rows = [''.join(r) for r in grid]

def open_cell(x, y):
    best = None
    for dy in range(-4, 5):
        for dx in range(-4, 5):
            cx, cy = x + dx, y + dy
            if not (1 <= cx < W - 1 and 1 <= cy < H - 1) or rows[cy][cx] != '.':
                continue
            if sum(rows[cy + ny][cx + nx] in '.,' for nx, ny in ((1, 0), (-1, 0), (0, 1), (0, -1))) < 2:
                continue
            d = dx * dx + dy * dy
            if best is None or d < best[0]:
                best = (d, [cx, cy])
    return best[1]
def cache(id, x, y, kind="Cache", facing=0):
    return {"id": id, "at": open_cell(x, y), "kind": kind, "facing": facing}
caches = [
    cache("ossuary_west", 15, 28),
    cache("choir_chapel", 6, 38, facing=1),
    cache("penitents_cells", 57, 22, facing=1),
    cache("abbots_hall", 44, 49, facing=1),
    cache("abbots_crypt", 30, 78, kind="Reliquary"),
    cache("bell_stair", 26, 91),
]
pages = [open_cell(46, 33), open_cell(3, 30), open_cell(44, 72)]
caddoc = open_cell(57, 37)

def pack(*members, at, radius=1.8, level=None):
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
DEAD, ACO, WHISP, MIRROR, PHANT, WRAITH, ABBOT = ("catacomb_dead", "choir_acolyte", "whisperer", "mirror_cultist", "phantom",
                                                  "frost_wraith", "hollow_abbot")
spawns = [
    # The long ossuary (level 13)
    pack((DEAD, N, 3), at=[20, 17]),
    pack((DEAD, N, 2), (ACO, N, 1), at=[40, 18]),
    pack((PHANT, N, 2), (DEAD, N, 2), at=[30, 26]),
    pack((DEAD, N, 3), at=[15, 26], radius=1.2),
    pack((WRAITH, N, 2), at=[46, 25], radius=1.2),
    pack((MIRROR, N, 2), (ACO, N, 1), at=[24, 33]),
    # The Choir's chapel
    pack((ACO, N, 2), (MIRROR, N, 1), at=[4, 20]),
    pack((ACO, C, 1, ["mirrored"]), (MIRROR, N, 2), at=[5, 32]),
    # The penitents' cells
    pack((DEAD, N, 3), at=[56, 16]),
    pack((WHISP, N, 1), (DEAD, N, 2), at=[56, 28]),
    # The abbots' hall (level 14 from here)
    pack((DEAD, N, 2), (ACO, N, 1), at=[22, 46], level=14),
    pack((MIRROR, C, 1, ["shielded"]), (MIRROR, N, 1), (ACO, N, 1), at=[38, 48], level=14),
    pack((WHISP, N, 2), (PHANT, N, 2), at=[30, 52], level=14),
    # The abbots' crypt: the Hollow Abbot and his choir
    pack((DEAD, N, 3), at=[20, 67], level=14),
    pack((ACO, N, 2), (DEAD, N, 1), at=[41, 67], level=14),
    pack((ABBOT, C, 1, ["vengeful"]), (ACO, N, 2), at=[30, 74], level=14),
    # The stair up to the Bell Tower
    pack((MIRROR, C, 1, ["mirrored", "hasted"]), (ACO, N, 1), at=[30, 89], level=14),
]
FLOOR = [16, 17, 18, 19]
CRACKED = [36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47]
def wall(ids, light=0, color=None):
    e = {"ground": "none", "objects": ids, "blocks": True, "blocksSight": True}
    if light:
        e["light"] = light
        if color:
            e["lightColor"] = color
    return e
def thing(ids, blocks=True, light=0, tiles=FLOOR, sight=False, color=None):
    e = {"ground": "floor", "tiles": tiles, "objects": ids, "blocks": blocks}
    if sight:
        e["blocksSight"] = True
    if light:
        e["light"] = light
        if color:
            e["lightColor"] = color
    return e
zone = {
    "id": "catacombs_of_oriel", "name": "The Catacombs of Oriel", "level": 13, "act": 3, "revision": REVISION, "xpScale": 0.6,
    "tileset": "tileset_dungeon",
    "tagline": "The abbots sleep. Something sings them dreams.",
    "darkness": 0.92, "heroLight": 4.5,
    "rows": rows,
    "legend": {
        ".": {"ground": "floor", "tiles": FLOOR},
        ",": {"ground": "floor", "tiles": CRACKED},
        "#": {"ground": "none", "blocks": True, "blocksSight": True},
        "A": wall([65, 69, 97]),
        "B": wall([64, 68, 96]),
        "a": wall([103], 3.5),
        "b": wall([102], 3.5),
        "e": wall([107]),
        "f": wall([106]),
        "g": wall([101]),
        "h": wall([100]),
        "m": wall([105]),
        "n": wall([104]),
        "C": wall([73]),
        "D": wall([74]),
        "L": wall([80]),
        "M": wall([81]),
        "N": wall([88]),
        "O": wall([89]),
        "H": wall([285]),
        "I": thing([110, 111], sight=True),
        "i": thing([178, 180, 181]),
        "p": thing([136, 137]),
        "Y": thing([134], light=3.0, color="#c9a8ff"),
        "d": thing([202, 203, 204, 205]),
        "u": thing([194, 197, 199, 200]),
        "Z": thing([167], light=4.5),
        "q": thing([176, 177, 178, 179, 180, 181, 182, 183], blocks=False),
        "S": thing([265], blocks=False, light=3.5),
    },
    "playerStart": [30, 6],
    "spawns": spawns,
    "shrines": [
        {"id": "catacomb_stair_shrine", "name": "The Catacomb Stair", "at": shrine1},
        {"id": "abbots_hall_shrine", "name": "The Abbots' Hall Shrine", "at": shrine2},
        {"id": "bell_stair_shrine", "name": "The Bell Stair Shrine", "at": shrine3},
    ],
    "exits": [
        {"id": "woods", "label": "The Hollow Woods", "at": [30, 3], "radius": 1.5, "to": "hollow_woods", "toExit": "catacombs",
         "arrive": [30, 6]},
        {"id": "bell_tower", "label": "The Bell Tower", "at": [30, 92], "radius": 1.5,
         "requires": {"act3.bell_tower": "open"},
         "sealedText": "The stair up to the Bell Tower is choked with fallen stone. (The Bell Tower is still being written.)"},
    ],
    "caches": caches,
    "npcs": [
        {"id": "caddoc", "name": "Brother Caddoc", "title": "Penitent", "at": caddoc, "facing": 6,
         "portrait": "flare/male10", "dialogue": "catacombs_caddoc", "requires": {"act3.caddoc": "met", "act3.penitent": ""},
         "look": {"head": "leather_hood", "chest": "mage_vest", "hands": "cloth_gloves", "legs": "cloth_pants", "feet": "cloth_sandals",
                  "main": "staff"}},
    ],
    "interactables": [
        {"id": "page_5", "name": "A burned page", "at": pages[0], "tile": 164, "dialogue": "library_page_5"},
        {"id": "page_6", "name": "A burned page", "at": pages[1], "tile": 164, "dialogue": "library_page_6"},
        {"id": "page_7", "name": "A burned page", "at": pages[2], "tile": 164, "dialogue": "library_page_7"},
    ],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
