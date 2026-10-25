#!/usr/bin/env python3
"""Builds Act 4's tileset, tileset_cinder (M4 step 8): the ash wastes, lava rivers, the Wyrm's Nest and the Reliquary.

Usage: tools/build_cinder_tiles.py <checkout of github.com/flareteam/flare-game>

Flare has no lava or ash ground (the art survey's gap), so the set is made from what it has:
- images/tilesets/tileset_cinder.png: Flare's cave tileset (mods/fantasycore) recoloured, its brown earth and rock turned
  to ash-grey and basalt; its tile ids are the cave set's own (ground 16-31, walls 64-135, stalagmites, rocks, the
  wooden walkways ...).
- images/tilesets/tileset_cinder_lava.png: lava, generated here from the cave set's pool texture and its ground. Every
  way a lava pool's corners can fall (TileMap.ShapeLava picks by them) is drawn over ash ground: id
  3000 + 4 * corners + variant, the corners a bit mask (1 top, 2 right, 4 bottom, 8 left; set where the corner is
  lava) and the variant the cell's parity, (x & 1) + 2 * (y & 1): the lava repeats every two cells, so tiles always meet
  without a seam. Embers, a crust that can be walked on and burns, are 3100-3103, by parity too.
- tilesetdefs/tileset_cinder.txt: the two, then rubberduck's Old Ruins at +1000 (copied from tileset_oriel.txt), for
  the Ember Gate's walls and the Reliquary's vaults.

Needs Pillow and numpy (the venv the gun tools use). Deterministic.
"""
import os, sys
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "src/AshenReliquary.App/Assets/Flare")
TW, TH = 192, 96


def parse_defs(path):
    tiles = {}
    for line in open(path):
        if line.startswith("tile="):
            v = [int(x) for x in line[5:].split(",")[:7]]
            tiles[v[0]] = v[1:]
    return tiles


def recolour(img):
    """Earth and rock to ash and basalt: most of the colour out, the shadows cooled, a faint warmth kept in the light."""
    a = np.asarray(img.convert("RGBA")).astype(np.float32)
    rgb, alpha = a[..., :3], a[..., 3:]
    grey = rgb @ np.array([0.30, 0.59, 0.11], dtype=np.float32)
    grey = grey[..., None]
    out = grey + (rgb - grey) * 0.22                      # 22% of the colour left
    out *= np.array([1.02, 0.98, 0.95], dtype=np.float32)  # ash is a little warm
    out = np.clip(out * 0.92, 0, 255)
    return Image.fromarray(np.concatenate([out, alpha], axis=-1).astype(np.uint8), "RGBA")


def rng(seed):
    return np.random.default_rng(seed)


def value_noise(w, h, cells, seed):
    """Smooth tileable value noise, 0..1."""
    r = rng(seed)
    grid = r.random((cells, cells)).astype(np.float32)
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    fx, fy = xs / w * cells, ys / h * cells
    x0, y0 = np.floor(fx).astype(int) % cells, np.floor(fy).astype(int) % cells
    x1, y1 = (x0 + 1) % cells, (y0 + 1) % cells
    tx, ty = fx - np.floor(fx), fy - np.floor(fy)
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    a = grid[y0, x0] * (1 - tx) + grid[y0, x1] * tx
    b = grid[y1, x0] * (1 - tx) + grid[y1, x1] * tx
    return a * (1 - ty) + b * ty


def fractal(w, h, seed, octaves=(4, 8, 16, 32)):
    total, weight = np.zeros((h, w), np.float32), 0.0
    for i, cells in enumerate(octaves):
        amp = 0.5 ** i
        total += value_noise(w, h, cells, seed + i * 101) * amp
        weight += amp
    return total / weight


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


# The lava and embers are drawn in the cells' own coordinates, repeating every two cells: a tile's variant is its cell's
# parity (TileMap.ShapeLava picks variant (x & 1) + 2 * (y & 1)), so neighbouring tiles always meet without a seam, the
# banks between them included.
FIELD = 512                                                   # pixels across two cells


def lava_field(seed):
    """Molten rock: flowing orange, veins of yellow heat, dark crust plates drifting with glowing rims."""
    n1 = fractal(FIELD, FIELD, seed, octaves=(4, 8, 16, 32, 64))
    n2 = fractal(FIELD, FIELD, seed + 31, octaves=(3, 6, 12, 24))
    n3 = fractal(FIELD, FIELD, seed + 63, octaves=(4, 8, 16, 32))
    veins = (1 - np.abs(2 * n2 - 1)) ** 5
    crust = smoothstep(0.54, 0.6, n3)
    rim = smoothstep(0.47, 0.54, n3) * (1 - crust)
    heat = np.clip(((0.30 + 0.40 * n1 + 0.55 * veins) * (1 - 0.8 * crust) + 0.35 * rim) * 0.88, 0, 1)
    ramp = np.array([[26, 12, 10], [80, 20, 10], [160, 40, 12], [228, 96, 20], [255, 170, 54], [255, 230, 150]], np.float32)
    pos = heat * (len(ramp) - 1)
    i0 = np.clip(np.floor(pos).astype(int), 0, len(ramp) - 2)
    t = (pos - i0)[..., None]
    return ramp[i0] * (1 - t) + ramp[i0 + 1] * t


def ember_field(seed):
    """A crust cooling over lava: dark rock, fine cracks glowing through."""
    n1 = fractal(FIELD, FIELD, seed, octaves=(4, 8, 16, 32))
    n2 = fractal(FIELD, FIELD, seed + 17, octaves=(6, 12, 24, 48))
    cracks = (1 - np.abs(2 * n2 - 1)) ** 9
    rock = np.array([46, 38, 34], np.float32) * (0.7 + 0.6 * n1[..., None])
    hot = np.array([255, 116, 30], np.float32)
    return rock * (1 - cracks[..., None]) + hot * cracks[..., None]


def sample(field, a, b, variant):
    """The field under a tile of this variant: (a, b) in the cell, the cell's parity picking the quarter."""
    qx, qy = variant & 1, variant >> 1
    u = (((a + qx) / 2) * FIELD).astype(int) % FIELD
    v = (((b + qy) / 2) * FIELD).astype(int) % FIELD
    return field[v, u]


def diamond_coords():
    """For each pixel of a ground tile: its place in the cell (a along +x, b along +y, 0..1) and whether it is inside."""
    ys, xs = np.mgrid[0:TH, 0:TW].astype(np.float32) + 0.5
    a = (xs - TW / 2) / TW + ys / TH
    b = ys / TH - (xs - TW / 2) / TW
    inside = (a >= 0) & (a <= 1) & (b >= 0) & (b <= 1)
    return np.clip(a, 0, 0.9999), np.clip(b, 0, 0.9999), inside


def ground_tile(sheet, defs, tid):
    x, y, w, h, ox, oy = defs[tid]
    tile = Image.new("RGBA", (TW, TH), (0, 0, 0, 0))
    tile.alpha_composite(sheet.crop((x, y, x + w, y + h)), (TW // 2 - ox, TH // 2 - oy))
    return np.asarray(tile).astype(np.float32)


def lava_tile(ground, lava, wobble, mask, variant, a, b, inside):
    """Ash ground with lava over the corners in the mask, the edge broken by noise, a basalt lip and a glow inside it."""
    top, right, bottom, left = [(mask >> i) & 1 for i in range(4)]
    # Corners on the grid: top (0,0), right (1,0), bottom (1,1), left (0,1).
    f = top * (1 - a) * (1 - b) + right * a * (1 - b) + bottom * a * b + left * (1 - a) * b
    if mask != 15:
        # Along a cell's edge the wobble fades out, so the bank meets its neighbour's where the corners say.
        f = f + (sample(wobble, a, b, variant) - 0.5) * 0.55
    molten = f > 0.5
    lip = (f > 0.36) & ~molten
    out = ground.copy()
    depth = smoothstep(0.5, 0.75, f)[..., None] if mask != 15 else np.ones((TH, TW, 1), np.float32)
    molten_rgb = sample(lava, a, b, variant) * (0.72 + 0.28 * depth)
    out[..., :3] = np.where(molten[..., None], molten_rgb, out[..., :3])
    out[..., 3] = np.where(molten, 255, out[..., 3])
    glow = smoothstep(0.36, 0.5, f)[..., None]
    lip_rgb = out[..., :3] * 0.30 * (1 - glow) + np.array([130, 36, 12], np.float32) * glow
    out[..., :3] = np.where(lip[..., None], lip_rgb, out[..., :3])
    out[..., 3] = np.where(inside, out[..., 3], 0)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")


def ember_tile(embers, variant, a, b, inside):
    out = np.zeros((TH, TW, 4), np.float32)
    out[..., :3] = sample(embers, a, b, variant)
    out[..., 3] = np.where(inside, 255, 0)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")


def main():
    flare = os.path.join(sys.argv[1], "mods/fantasycore")
    cave_defs = parse_defs(os.path.join(flare, "tilesetdefs/tileset_cave.txt"))
    cave = recolour(Image.open(os.path.join(flare, "images/tilesets/tileset_cave.png")))
    cave.save(os.path.join(ASSETS, "images/tilesets/tileset_cinder.png"), optimize=True)

    a, b, inside = diamond_coords()
    lava, wobble, embers = lava_field(77), fractal(FIELD, FIELD, 900, octaves=(4, 8, 16)), ember_field(4000)
    grounds = [ground_tile(cave, cave_defs, t) for t in (16, 17, 18, 19)]
    tiles = []                                                # (id, image)
    for mask in range(1, 16):
        for variant in range(4):
            tiles.append((3000 + 4 * mask + variant, lava_tile(grounds[(mask + variant) % 4], lava, wobble, mask, variant, a, b, inside)))
    for variant in range(4):
        tiles.append((3100 + variant, ember_tile(embers, variant, a, b, inside)))
    cols = 8
    sheet = Image.new("RGBA", (cols * TW, ((len(tiles) + cols - 1) // cols) * TH), (0, 0, 0, 0))
    lines = ["[tileset]", "img=images/tilesets/tileset_cinder_lava.png"]
    for i, (tid, img) in enumerate(tiles):
        x, y = (i % cols) * TW, (i // cols) * TH
        sheet.alpha_composite(img, (x, y))
        lines.append(f"tile={tid},{x},{y},{TW},{TH},{TW // 2},{TH // 2}")
    sheet.save(os.path.join(ASSETS, "images/tilesets/tileset_cinder_lava.png"), optimize=True)

    # The definition: the cave set (its own image), the lava, then the ruins at +1000 from tileset_oriel.txt.
    cave_text = open(os.path.join(flare, "tilesetdefs/tileset_cave.txt")).read().replace(
        "img=images/tilesets/tileset_cave.png", "img=images/tilesets/tileset_cinder.png")
    oriel = open(os.path.join(ASSETS, "tilesetdefs/tileset_oriel.txt")).read()
    ruins = oriel[oriel.index("img=images/tilesets/tileset_ruins.png"):]
    with open(os.path.join(ASSETS, "tilesetdefs/tileset_cinder.txt"), "w") as f:
        f.write("# Act 4 (M4 step 8): Flare's cave tileset recoloured to ash, generated lava and embers (3000-3103), and the\n")
        f.write("# Old Ruins at +1000. Built by tools/build_cinder_tiles.py.\n")
        f.write(cave_text.rstrip("\n") + "\n\n" + "\n".join(lines) + "\n\n[tileset]\n" + ruins)
    print(f"{len(tiles)} lava and ember tiles")


if __name__ == "__main__":
    main()
