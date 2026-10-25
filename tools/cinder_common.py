# Shared pieces of Act 4's map generators (M4 step 8): the tileset_cinder legend, cave walls chosen by the side the floor
# lies on, open-cell finding, packs. tileset_cinder is Flare's cave set recoloured to ash (its ids), generated lava and
# embers (TileMap.LavaTile, EmberTile: the engine shapes them), and rubberduck's Old Ruins at +1000.
#
# Map characters every Act 4 zone shares:
#   .  ash             ,  ash with pebbles and bones     =  flagstones (the ruins' floor)
#   ~  lava (blocked)  #  a walkway over lava (a bridge) e  embers (walkable; they burn)
#   C  rock spires and boulders (outdoors: blocks sight)  w  a cave wall (indoors: shaped by wall_tiles)
#   W  ruined wall     R  ruined pillar                   B  brazier (fire)
#   S  shrine circle   X  open floor kept clear for a person or a thing
#   o  a boulder (cover)
import json

FLAG = [1024, 1025, 1026, 1027, 1028, 1029, 1030, 1031]
ASH = list(range(16, 32))
PEBBLES = [48, 49, 50, 51, 52, 53, 54, 55]
SPIRES = [144, 145, 146, 147, 148, 149, 153]
BOULDERS = [150, 151, 152]
WALKWAY = [232, 233, 234, 235, 236, 237, 238, 239]


class Rng:
    """The fixed LCG every generator uses, so a zone is the same each time."""

    def __init__(self, seed):
        self.seed = seed

    def __call__(self):
        self.seed = (self.seed * 1103515245 + 12345) & 0x7fffffff
        return self.seed / 0x7fffffff

    def pick(self, items):
        return items[int(self() * len(items)) % len(items)]


def legend(extra=None):
    base = {
        ".": {"ground": "ash"},
        ",": {"ground": "ash", "tiles": PEBBLES},
        "=": {"ground": "ash", "tiles": FLAG},
        "~": {"ground": "lava", "light": 0},
        "#": {"ground": "lava", "objects": WALKWAY, "bridge": True},
        "e": {"ground": "embers"},
        "C": {"ground": "ash", "objects": SPIRES, "blocks": True, "blocksSight": True},
        "o": {"ground": "ash", "objects": BOULDERS, "blocks": True, "cover": True},
        "W": {"ground": "ash", "tiles": FLAG, "objects": [1223, 1224, 1225, 1226, 1227, 1228, 1229], "blocks": True, "blocksSight": True},
        "R": {"ground": "ash", "objects": [1185, 1186], "blocks": True, "blocksSight": True},
        "B": {"ground": "ash", "tiles": FLAG, "objects": [1295], "blocks": True, "light": 4.5},
        "S": {"ground": "ash", "tiles": FLAG, "objects": [264], "light": 3.5, "lightColor": "#ffd9a0"},
        "X": {"ground": "ash", "tiles": FLAG},
        " ": {"ground": "none", "blocks": True, "blocksSight": True},
    }
    for k in [k for k, v in base.items() if v.get("light") == 0]:
        del base[k]["light"]
    base.update(extra or {})
    return base


def wall_tiles(grid, rng, floor=".,=eX#~SBo!abcdfg"):
    """
    Turns each cave-wall cell ('w') into the wall the floor beside it calls for (Flare's cave walls show their rock on
    the faces toward the camera, +x and +y): rock on both faces where floor lies to +x and +y, on one where it lies to
    one side, a dark back wall where the floor lies behind it, and solid rock (nothing drawn) inside. Returns the legend
    entries it used.
    """
    H, W = len(grid), len(grid[0])
    def floor_at(x, y):
        return 0 <= x < W and 0 <= y < H and grid[y][x] in floor
    for y in range(H):
        for x in range(W):
            if grid[y][x] != 'w':
                continue
            fx, fy = floor_at(x + 1, y), floor_at(x, y + 1)
            if fx and fy:
                grid[y][x] = '4'
            elif fy:
                grid[y][x] = '1'
            elif fx:
                grid[y][x] = '2'
            elif floor_at(x - 1, y) or floor_at(x, y - 1) or floor_at(x - 1, y + 1) or floor_at(x + 1, y - 1) or floor_at(x + 1, y + 1):
                grid[y][x] = '3'
            else:
                grid[y][x] = ' '
    return {
        "1": {"ground": "ash", "objects": [64, 68], "blocks": True, "blocksSight": True},
        "2": {"ground": "ash", "objects": [65, 69], "blocks": True, "blocksSight": True},
        "4": {"ground": "ash", "objects": [72, 76, 96, 98, 100, 102], "blocks": True, "blocksSight": True},
        "3": {"ground": "ash", "objects": [74, 78], "blocks": True, "blocksSight": True},
    }


def open_cell(rows, x, y, ok=".,=X", need=3, reach=6):
    """The nearest cell to (x, y) on open ground with at least `need` open neighbours: somewhere to stand a chest or a person."""
    best = None
    H, W = len(rows), len(rows[0])
    for dy in range(-reach, reach + 1):
        for dx in range(-reach, reach + 1):
            cx, cy = x + dx, y + dy
            if not (1 <= cx < W - 1 and 1 <= cy < H - 1) or rows[cy][cx] not in ok:
                continue
            if sum(rows[cy + ny][cx + nx] in ok for nx, ny in ((1, 0), (-1, 0), (0, 1), (0, -1))) < need:
                continue
            d = dx * dx + dy * dy
            if best is None or d < best[0]:
                best = (d, [cx, cy])
    if best is None:
        raise SystemExit(f"no open cell near {x},{y}")
    return best[1]


def pack(*members, at, radius=2.0, level=None, **extra):
    """A spawn: members are (creature, rank, count[, affixes[, options]]); options are PackMember flags ("leads", "echo")."""
    out = []
    for m in members:
        d = dict(zip(("creature", "rank", "count"), m[:3]))
        if len(m) > 3 and m[3]:
            d["affixes"] = m[3]
        if len(m) > 4:
            d.update(m[4])
        out.append(d)
    spawn = {"at": at, "radius": radius, "pack": out}
    if level:
        spawn["level"] = level
    spawn.update(extra)
    return spawn


LEADS = {"leads": True}


def write(zone, path):
    json.dump(zone, open(path, "w"), indent=1)
    print("\n".join(zone["rows"]))
