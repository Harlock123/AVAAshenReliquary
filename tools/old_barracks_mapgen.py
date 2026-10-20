# Generates zones/old_barracks.json: Act 2's third zone (levels 8-9), a dungeon: the Iron Legion's barracks under the
# Barrow Fields, sealed when the legion was buried. Deterministic: no randomness beyond a fixed LCG.
#
# The barracks, top (north) to bottom: the stair hall (the way down from the Barrow Fields, a shrine), the drill hall
# (pillared), the dormitories to the west (beds; their sleepers rise), the mess hall and kitchen to the east, the
# officers' hall (a second shrine), the armoury to the east (Quartermaster Corvin, the reliquary, the Clockwork
# Confessor's speaking-grille), a west vault, and down a trapped passage the officers' crypt (a third shrine) where
# Ser Hadric's tomb lies open and empty (Hadric's Oath: his journal and his sword).
#
# Act 2's mechanic here: traps. Pressure runes under the flagstones, found with Perception as the hero comes near and
# disarmed with Thievery (see GameWorld.Traps); more of them the deeper the passage.
#
# Walls as in the Drowned Chapel: a solid cell next to floor shows a face toward it; the far walls stand full height,
# the near walls are low. Dark: torches and braziers light it; the hero carries a small light.
#
# Bump REVISION whenever the layout changes: saves made on another revision start the zone again at its entrance.
import json, sys

REVISION = 1
W, H = 60, 80
grid = [['#'] * W for _ in range(H)]
seed = 24680
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
room(24, 3, 35, 10)                                    # the stair hall
room(28, 11, 31, 14)
room(16, 15, 43, 32)                                   # the drill hall
room(10, 22, 15, 24)                                   # to the west dormitory
room(2, 16, 9, 30)                                     # the west dormitory
room(5, 31, 7, 33)
room(2, 34, 13, 44)                                    # the south dormitory
room(44, 22, 47, 24)                                   # to the mess hall
room(48, 16, 57, 32)                                   # the mess hall
room(52, 33, 54, 33)
room(48, 34, 57, 42)                                   # the kitchen
room(28, 33, 31, 40)                                   # south to the officers' hall
room(20, 41, 39, 50)                                   # the officers' hall
room(40, 45, 43, 47)                                   # to the armoury
room(44, 44, 57, 58)                                   # the armoury
room(14, 47, 19, 49)                                   # to the west vault
room(4, 46, 13, 58)                                    # the west vault
room(28, 51, 31, 60)                                   # the trapped passage down
room(16, 61, 43, 76)                                   # the officers' crypt

# --- Furniture ---
for y in range(18, 31, 4):                             # the drill hall's pillars
    for x in (20, 39):
        put(x, y, 'I')
for x in range(3, 9, 2):                               # beds in the dormitories
    for y in (17, 29):
        put(x, y, 'd')
for x in range(3, 13, 3):
    for y in (35, 43):
        put(x, y, 'd')
for y in range(19, 31, 4):                             # the mess hall's tables
    for x in (50, 54):
        put(x, y, 't')
for (x, y) in [(49, 35), (50, 35), (56, 35), (56, 41), (49, 41), (17, 16), (42, 16), (45, 57), (56, 57), (56, 45)]:
    put(x, y, 'k')                                     # barrels and crates
for (x, y) in [(46, 46), (50, 46), (54, 46), (46, 56), (50, 56)]:
    put(x, y, 'k')                                     # the armoury's stores
for (x, y) in [(21, 42), (38, 42), (21, 49), (38, 49), (17, 62), (42, 62), (17, 75), (42, 75)]:
    put(x, y, 's')                                     # statues of the legion's officers
for row in (65, 69):                                   # the crypt's tombs in ranks
    for x in list(range(19, 27, 3)) + list(range(33, 41, 3)):
        put(x, row, 'u')
put(29, 72, 'X'); put(30, 72, 'X')                     # Hadric's tomb (an interactable stands on its west cell)
for (x, y) in [(6, 50), (11, 50), (6, 56), (11, 56)]:
    put(x, y, 'u')                                     # the west vault's sarcophagi
for (x, y) in [(26, 5), (33, 5), (18, 17), (41, 17), (22, 44), (37, 44), (19, 63), (40, 63), (19, 74), (40, 74), (46, 48), (55, 50)]:
    put(x, y, 'Z')                                     # braziers
for (x, y) in [(5, 20), (8, 26), (11, 38), (52, 25), (23, 46), (30, 67), (36, 72), (8, 54), (49, 52), (25, 28)]:
    put(x, y, 'q')                                     # bones
shrine1, shrine2, shrine3 = [32, 8], [24, 43], [30, 62]
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

# Dress the far walls: torches every few cells, shelves of old kit, banners, cracks.
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
grid[2][29] = 'H'                                      # the stair up to the Barrow Fields

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
    cache("west_dormitory", 3, 23),
    cache("south_dormitory", 12, 40, facing=1),
    cache("kitchen", 55, 38, facing=1),
    cache("west_vault", 5, 57),
    cache("armoury", 55, 54, kind="Reliquary", facing=1),
    cache("crypt", 41, 70, facing=1),
]

# Traps: pressure runes in the doorways and passages, thickest on the way down to the crypt.
TRAPS = [
    ("stair_passage", [29, 12], "spikes"), ("west_door", [12, 23], "spikes"), ("mess_door", [45, 23], "spikes"),
    ("drill_south", [30, 35], "spikes"), ("drill_south_2", [29, 38], "flame"), ("officers", [31, 45], "spikes"),
    ("armoury_door", [41, 46], "flame"), ("vault_door", [16, 48], "poison"), ("vault", [9, 53], "poison"),
    ("descent_1", [29, 53], "spikes"), ("descent_2", [30, 55], "flame"), ("descent_3", [28, 57], "spikes"),
    ("descent_4", [31, 59], "flame"), ("crypt_west", [24, 67], "flame"), ("crypt_east", [35, 67], "spikes"),
    ("crypt_tomb", [29, 70], "poison"),
]
traps = [{"id": id_, "at": at, "kind": kind} for (id_, at, kind) in TRAPS]
for t in traps:
    assert rows[t["at"][1]][t["at"][0]] in '.,', t

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
LEG, XB, BLEG, DEAD, WIGHT, KNIGHT, CORVIN = ("legionary", "legion_crossbowman", "barrow_legionary", "barrow_dead", "barrow_wight",
                                               "barrow_knight", "quartermaster_corvin")
spawns = [
    # The drill hall (level 8)
    pack((LEG, N, 2), (XB, N, 1), at=[24, 20]),
    pack((BLEG, N, 3), at=[35, 24]),
    pack((XB, N, 2), at=[30, 30], radius=1.2),
    # The dormitories: the sleepers rise
    pack((DEAD, N, 3), at=[5, 22]),
    pack((DEAD, N, 2), (BLEG, N, 1), at=[7, 39]),
    # The mess hall and kitchen
    pack((BLEG, N, 2), (WIGHT, N, 1), at=[52, 22]),
    pack((LEG, C, 1, ["shielded"]), (LEG, N, 1), at=[53, 38]),
    # The officers' hall (level 9 from here)
    pack((KNIGHT, N, 1), (BLEG, N, 2), at=[30, 46], level=9),
    pack((WIGHT, N, 2), at=[35, 43], radius=1.2, level=9),
    # The armoury: Quartermaster Corvin and his stores
    pack((XB, N, 2), (LEG, N, 1), at=[48, 49], level=9),
    pack((CORVIN, C, 1), (BLEG, N, 2), at=[52, 53], level=9),
    # The west vault
    pack((WIGHT, N, 1), (DEAD, N, 2), at=[8, 52], level=9),
    # The officers' crypt
    pack((KNIGHT, N, 1), (BLEG, N, 2), at=[22, 64], level=9),
    pack((WIGHT, N, 2), (DEAD, N, 1), at=[37, 64], level=9),
    pack((KNIGHT, C, 1, ["vengeful"]), (KNIGHT, N, 1), at=[29, 74], level=9),
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
def thing(ids, blocks=True, light=0, tiles=FLOOR, sight=False):
    e = {"ground": "floor", "tiles": tiles, "objects": ids, "blocks": blocks}
    if sight:
        e["blocksSight"] = True
    if light:
        e["light"] = light
    return e
zone = {
    "id": "old_barracks", "name": "The Old Barracks", "level": 8, "act": 2, "revision": REVISION, "tileset": "tileset_dungeon",
    "tagline": "The legion's beds are still made.",
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
        "d": thing([202, 203, 204, 205]),
        "t": thing([136, 137]),
        "k": thing([146, 147, 162, 163]),
        "s": thing([128, 129, 130, 131]),
        "u": thing([194, 197, 199, 200]),
        "X": thing([]),
        "Z": thing([167], light=4.5),
        "q": thing([176, 177, 178, 179, 180, 181, 182, 183], blocks=False),
        "S": thing([265], blocks=False, light=3.5),
    },
    "playerStart": [29, 6],
    "spawns": spawns,
    "shrines": [
        {"id": "stair_hall_shrine", "name": "The Stair Hall Shrine", "at": shrine1},
        {"id": "officers_shrine", "name": "The Officers' Shrine", "at": shrine2},
        {"id": "officers_crypt_shrine", "name": "The Officers' Crypt Shrine", "at": shrine3},
    ],
    "exits": [
        {"id": "stair", "label": "The Barrow Fields", "at": [29, 3], "radius": 1.5, "to": "barrow_fields", "toExit": "barracks",
         "arrive": [29, 6]},
    ],
    "caches": caches,
    "traps": traps,
    "interactables": [
        {"id": "hadrics_tomb", "name": "Ser Hadric's tomb", "at": [29, 72], "tile": 194, "dialogue": "barracks_tomb"},
        {"id": "speaking_grille", "name": "A broken crate of brass", "at": [53, 51], "tile": 163, "dialogue": "barracks_grille"},
    ],
}
json.dump(zone, open(sys.argv[1], "w"), indent=1)
print("\n".join(rows))
