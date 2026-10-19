#!/usr/bin/env python3
"""Rebuilds the Beastmaster's wolf (Assets/Flare/images/enemies/wolf.png and animations/enemies/wolf.txt) from Danimal's
"Wolf monsters animated" (https://opengameart.org/content/wolf-monsters-animated, CC-BY-SA 4.0/3.0).

Usage: tools/build_wolf.py <blender 2.79 binary> <Wolf.blend>

Blender 2.79 is needed: the scene uses Blender Internal, which 2.8 dropped. It renders all 42 frames in 8 directions
at 50% of the scene's 512 px (256-px frames, twice the published sheet), then each frame is trimmed to the wolf (a
pixel of margin) and shelf-packed into a 2048-wide sheet in Flare's packed format, the anchor (128, 192) kept at its
feet. Drawn at scale 1.1 (creatures.json) it matches the published sheet at 2.2, twice as sharp.
"""
import os, subprocess, sys, tempfile

def pack(full, out_png, out_txt):
    CELL, OX, OY, PAD, WIDTH = 256, 128, 192, 1, 2048
    frames = {}
    for d in range(8):
        for f in range(1, 43):
            path = os.path.join(full, "d{}_{:04d}.png".format(d, f))
            geo = subprocess.run(["magick", path, "-format", "%@", "info:"], capture_output=True, text=True, check=True).stdout.strip()
            wh, x, y = geo.split("+")[0], int(geo.split("+")[1]), int(geo.split("+")[2])
            w, h = map(int, wh.split("x"))
            # A pixel of margin round each frame (inside the cell), so scaling never bleeds a neighbour in.
            x0, y0 = max(0, x - PAD), max(0, y - PAD)
            x1, y1 = min(CELL, x + w + PAD), min(CELL, y + h + PAD)
            frames[(f - 1, d)] = (path, x0, y0, x1 - x0, y1 - y0)
    # Shelf packing, tallest first.
    order = sorted(frames, key=lambda k: -frames[k][4])
    place, x, y, shelf = {}, 0, 0, 0
    for k in order:
        _, _, _, w, h = frames[k]
        if x + w > WIDTH:
            x, y, shelf = 0, y + shelf + 1, 0
        place[k] = (x, y)
        x += w + 1
        shelf = max(shelf, h)
    height = y + shelf
    args = ["magick", "-size", "{}x{}".format(WIDTH, height), "xc:none"]
    for k, (px, py) in place.items():
        path, x0, y0, w, h = frames[k]
        args += ["(", path, "-crop", "{}x{}+{}+{}".format(w, h, x0, y0), "+repage", ")", "-geometry", "+{}+{}".format(px, py), "-composite"]
    args += ["-define", "png:compression-level=9", out_png]
    subprocess.run(args, check=True)
    # The animations (as the grid file: column = position + frame).
    anims = [("stance", 0, 4, "800ms", "back_forth"), ("run", 4, 8, "533ms", "looped"), ("swing", 12, 5, "800ms", "play_once"),
             ("hit", 17, 2, "133ms", "play_once"), ("block", 17, 2, "66ms", "play_once"), ("die", 17, 11, "1000ms", "play_once"),
             ("critdie", 28, 9, "1000ms", "play_once"), ("shoot", 38, 4, "800ms", "play_once"), ("cast", 38, 4, "800ms", "play_once")]
    lines = ["image=images/enemies/wolf.png", ""]
    for name, position, count, duration, kind in anims:
        lines += ["[{}]".format(name), "frames={}".format(count), "duration={}".format(duration), "type={}".format(kind)]
        for i in range(count):
            for d in range(8):
                _, x0, y0, w, h = frames[(position + i, d)]
                px, py = place[(position + i, d)]
                lines.append("frame={},{},{},{},{},{},{},{}".format(i, d, px, py, w, h, OX - x0, OY - y0))
        lines.append("")
    open(out_txt, "w").write("\n".join(lines))
    print("sheet", WIDTH, "x", height)


def main():
    blender, blend = sys.argv[1], sys.argv[2]
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    flare = os.path.join(root, "src/AshenReliquary.App/Assets/Flare")
    with tempfile.TemporaryDirectory() as full:
        subprocess.run([blender, "-b", blend, "-P", os.path.join(root, "tools/wolf_render_blender.py"), "--", full, "50", "1", "42"],
                       check=True, stdout=subprocess.DEVNULL)
        pack(full, os.path.join(flare, "images/enemies/wolf.png"), os.path.join(flare, "animations/enemies/wolf.txt"))

if __name__ == "__main__":
    main()
