#!/usr/bin/env python3
"""Builds the Gunslinger's firearm art from Flare's own avatar layers: no firearm art exists in Flare, so the guns are
modelled here as a handful of 3D capsules (barrel, stock, lock, grip...) and drawn into every frame of every animation
and direction, posed by the hands of the existing layers.

Usage: python3 tools/build_guns.py [--sheets <dir>]
  Needs Pillow and numpy (not installed system-wide on the development machine; a venv works:
  python3 -m venv /tmp/guns && /tmp/guns/bin/pip install pillow numpy && /tmp/guns/bin/python tools/build_guns.py).
  --sheets <dir> also writes contact sheets (the guns composited over the default body at 2x) for checking by eye.

Writes, under src/AshenReliquary.App/Assets/Flare:
  animations/avatar/male/{pistol,pistol_offhand,long_gun}.txt and images/avatar/male/{...}.png  (gfx ids)
  animations/loot/{pistol,long_gun}.txt and images/loot/{...}.png                                 (loot ids)

How each frame is posed (every Flare avatar layer is rendered from one body animation, so the layers agree):
  - the right fist is where the wand's grip end meets default_hands; the left fist is where the longbow meets it;
  - pistol: along the wand (its 2D direction, foreshortened by its visible length, lifted to 3D pointing the way the
    hero faces); in "shoot" it is in the left hand, the one the bow pose extends at the target, along the facing;
  - pistol_offhand: in the left fist along the facing, tilted to follow the forearm; in "shoot" it is raised in the
    right hand (drawn back by the bow pose);
  - long_gun: along the staff, the right fist at the stock's wrist, the muzzle at the staff's gem end; in "shoot" it is
    shouldered: butt at the right fist, barrel through the left fist, along the facing.
Flare bakes the body's hold-out into every layer; here a gun pixel over the body is kept only near the visible part of
the layer it was posed from (the wand, staff or fist), so a gun behind the body is hidden as the source layer is.
Frame counts, durations, loop types and section names are copied from the source layer.
"""
import argparse, math, os
import numpy as np
from PIL import Image, ImageDraw

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FLARE = os.path.join(REPO, "src/AshenReliquary.App/Assets/Flare")
AX, AY, CW, CH = 200, 260, 400, 340  # the anchor (feet) on a work canvas big enough for any frame
SS = 4                                # supersampling when drawing the capsules
K_Y, K_Z = 0.5, math.sqrt(3) / 2      # Flare's 2:1 isometric camera: 30 degrees down
VIEW = np.array([0.0, K_Z, -K_Y])     # into the scene (world: x east/right, y north/into the screen, z up)


# ---------------------------------------------------------------- Flare files

def parse_animation(path):
    """Returns (image, [(section, meta lines, {(frame, direction): [x, y, w, h, ox, oy]})])."""
    image, sections, current = None, [], None
    for line in open(path):
        line = line.strip()
        if line.startswith("["):
            current = (line[1:-1], [], {})
            sections.append(current)
        elif current is None:
            if line.startswith("image="):
                image = line[len("image="):]
        elif line.startswith("frame="):
            v = [int(x) for x in line[len("frame="):].split(",")[:8]]
            current[2][(v[0], v[1])] = v[2:]
        elif line and not line.startswith("#"):
            current[1].append(line)
    return image, sections


class Layer:
    def __init__(self, name, kind="avatar/male"):
        image, self.sections = parse_animation(os.path.join(FLARE, f"animations/{kind}/{name}.txt"))
        self.sheet = np.asarray(Image.open(os.path.join(FLARE, image)).convert("RGBA"))
        self.by_name = {s[0]: s for s in self.sections}
        self.cache = {}

    def frame(self, anim, f, d):
        """The frame on the work canvas (CH x CW x 4), placed by its anchor."""
        key = (anim, f, d)
        if key not in self.cache:
            out = np.zeros((CH, CW, 4), np.uint8)
            x, y, w, h, ox, oy = self.by_name[anim][2][(f, d)]
            out[AY - oy:AY - oy + h, AX - ox:AX - ox + w] = self.sheet[y:y + h, x:x + w]
            self.cache[key] = out
        return self.cache[key]


def frames_of(section):
    return 1 + max(f for f, _ in section[2])


# ---------------------------------------------------------------- geometry

def project(p):
    p = np.asarray(p, float)
    return np.array([p[..., 0], -(p[..., 1] * K_Y + p[..., 2] * K_Z)]).T if p.ndim > 1 else np.array([p[0], -(p[1] * K_Y + p[2] * K_Z)])


def depth(p):
    return float(np.dot(p, VIEW))


def facing(d):
    """The ground direction (3D, unit) a Flare direction index faces: 0 W, 1 NW, 2 N ... clockwise on screen."""
    a = math.radians(180 - 45 * d)
    return np.array([math.cos(a), math.sin(a), 0.0])


def unit(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-9 else v


def unproject(screen, ratio, face):
    """A 3D unit vector whose projection is `ratio` times the unit screen vector, the hidden part pointing the way
    the hero faces."""
    s = unit(np.asarray(screen, float))
    ratio = min(1.0, max(0.0, ratio))
    inplane = s[0] * np.array([1.0, 0, 0]) + (-s[1]) * np.array([0, K_Y, K_Z])
    hidden = math.sqrt(max(0.0, 1 - ratio * ratio))
    sign = 1.0 if np.dot(VIEW, face) > 1e-6 else -1.0
    return unit(ratio * inplane + sign * hidden * VIEW)


def tilted(face, angle):
    """The facing, pitched up by `angle` radians."""
    return unit(face * math.cos(angle) + np.array([0, 0, 1.0]) * math.sin(angle))


def best_tilt(face, screen_dir, lo=-1.2, hi=1.2):
    """The pitch of the facing whose projection best lines up with a screen direction."""
    target = unit(np.asarray(screen_dir, float))
    best, best_cos = 0.0, -2
    for a in np.linspace(lo, hi, 49):
        c = float(np.dot(unit(project(tilted(face, a))), target))
        if c > best_cos:
            best, best_cos = a, c
    return best, best_cos


def basis(forward, face):
    up = np.array([0, 0, 1.0]) - np.dot([0, 0, 1.0], forward) * forward
    if np.linalg.norm(up) < 0.2:
        up = -face if forward[2] > 0 else face
        up = up - np.dot(up, forward) * forward
    up = unit(up)
    side = np.cross(up, forward)
    return forward, side, up


# ---------------------------------------------------------------- the guns (game pixels; x forward, y side, z up)

STEEL, STEEL_LIGHT, WOOD, WOOD_DARK, BRASS = (58, 60, 66), (120, 124, 134), (112, 64, 32), (84, 46, 22), (178, 138, 58)


def pistol_model():
    # Origin: the middle of the fist round the grip. The barrel runs above it, the grip drops down and back.
    m = [
        ((-8.5, 0, -7.5), (-3.0, 0, -0.5), 2.0, WOOD),          # grip
        ((-9.0, 0, -8.3), (-8.0, 0, -8.8), 2.4, BRASS),         # butt cap
        ((-5.0, 0, 0.6), (11.0, 0, 0.6), 1.5, WOOD_DARK),       # fore-stock
        ((-4.0, 0, 2.6), (21.0, 0, 2.6), 1.45, STEEL),          # barrel
        ((20.0, 0, 2.6), (21.6, 0, 2.6), 1.85, STEEL_LIGHT),    # muzzle
        ((-4.0, 1.1, 1.2), (1.0, 1.1, 1.2), 1.15, STEEL_LIGHT), # lock plate
        ((-3.6, 0.6, 2.4), (-4.6, 0.6, 5.0), 0.8, STEEL),       # hammer
        ((-0.8, 0, -0.6), (1.4, 0, -2.6), 0.55, BRASS),         # trigger guard
        ((1.4, 0, -2.6), (-2.2, 0, -2.4), 0.55, BRASS),
    ]
    return m


def long_gun_model():
    # Origin: the fist at the stock's wrist, behind the trigger. The muzzle is 74 px forward, the butt 34 px back.
    return [
        ((-2.0, 0, -0.6), (-12.0, 0, -3.2), 2.3, WOOD),         # wrist
        ((-12.0, 0, -2.6), (-33.0, 0, -3.0), 2.9, WOOD),        # butt, upper line
        ((-14.0, 0, -4.6), (-33.0, 0, -7.6), 2.9, WOOD),        # butt, lower line
        ((-34.0, 0, -1.2), (-34.0, 0, -9.6), 1.3, BRASS),       # butt plate
        ((-2.0, 0, 0.6), (60.0, 0, 0.6), 2.0, WOOD),            # fore-stock
        ((-1.0, 0, 2.8), (74.0, 0, 2.8), 1.5, STEEL),           # barrel
        ((72.8, 0, 2.8), (74.6, 0, 2.8), 1.9, STEEL_LIGHT),     # muzzle
        ((26.0, 0, 1.8), (27.6, 0, 1.8), 2.6, BRASS),           # barrel bands
        ((48.0, 0, 1.8), (49.6, 0, 1.8), 2.6, BRASS),
        ((-1.0, 1.6, 1.4), (6.0, 1.6, 1.4), 1.3, STEEL_LIGHT),  # lock plate
        ((0.0, 0.8, 3.0), (-1.2, 0.8, 5.8), 0.9, STEEL),        # hammer
        ((2.0, 0, -1.4), (5.0, 0, -3.8), 0.6, BRASS),           # trigger guard
        ((5.0, 0, -3.8), (0.0, 0, -3.6), 0.6, BRASS),
    ]


def shade(c, k):
    return tuple(max(0, min(255, int(round(v * k)))) for v in c)


def render(model, origin2d, forward, face, scale=1.0, size=(CW, CH), anchor=(AX, AY), frame=None):
    """Draws the model at a screen point (relative to the anchor) with its x axis along `forward` (or the given
    (forward, side, up) frame); returns an RGBA array."""
    f, s, u = frame if frame is not None else basis(forward, face)
    W, H = size
    img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    caps = []
    for p0, p1, r, colour in model:
        w0 = scale * (p0[0] * f + p0[1] * s + p0[2] * u)
        w1 = scale * (p1[0] * f + p1[1] * s + p1[2] * u)
        caps.append(((depth(w0) + depth(w1)) / 2, w0, w1, r * scale, colour))
    caps.sort(key=lambda c: -c[0])  # far first
    ox, oy = anchor[0] + origin2d[0], anchor[1] + origin2d[1]
    for _, w0, w1, r, colour in caps:
        a, b = project(w0), project(w1)
        a = ((ox + a[0]) * SS, (oy + a[1]) * SS)
        b = ((ox + b[0]) * SS, (oy + b[1]) * SS)
        light = (-0.35 * r * SS, -0.45 * r * SS)
        for width, col, off in ((2 * r + 1.1, shade(colour, 0.45), (0, 0)), (2 * r, colour, (0, 0)),
                                (0.9 * r, shade(colour, 1.45), light)):
            wpx = max(1.0, width * SS)
            pa, pb = (a[0] + off[0], a[1] + off[1]), (b[0] + off[0], b[1] + off[1])
            draw.line([pa, pb], fill=col + (255,), width=int(round(wpx)))
            for q in (pa, pb):
                draw.ellipse([q[0] - wpx / 2, q[1] - wpx / 2, q[0] + wpx / 2, q[1] + wpx / 2], fill=col + (255,))
    return np.asarray(img.reduce(SS))


# ---------------------------------------------------------------- reading poses from the layers

def coords(mask):
    ys, xs = np.nonzero(mask)
    return np.stack([xs - AX, ys - AY], 1).astype(float)


def axis(points):
    """Endpoints and length of a stick of pixels, by its principal axis."""
    m = points.mean(0)
    _, _, vt = np.linalg.svd(points - m, full_matrices=False)
    a = vt[0]
    t = (points - m) @ a
    return m + a * t.min(), m + a * t.max(), float(t.max() - t.min())


def dilate(mask, r):
    out = mask.copy()
    for _ in range(r):
        m = out.copy()
        m[1:] |= out[:-1]; m[:-1] |= out[1:]; m[:, 1:] |= out[:, :-1]; m[:, :-1] |= out[:, 1:]
        out = m
    return out


def nearest(points, p):
    if len(points) == 0:
        return math.inf
    return float(np.min(np.linalg.norm(points - p, axis=1)))


class Poses:
    def __init__(self):
        self.hands, self.wand, self.staff, self.bow = Layer("default_hands"), Layer("wand"), Layer("staff"), Layer("longbow")
        self.body = [Layer(n) for n in ("default_chest", "default_legs", "default_feet", "head_short")]
        self.poses = {}
        self.wand_max = self.max_length(self.wand)
        self.staff_max = self.max_length(self.staff)

    @staticmethod
    def max_length(layer):
        best = 0
        for name, _, frames in layer.sections:
            for f, d in frames:
                m = layer.frame(name, f, d)[..., 3] >= 128
                if m.sum() > 6:
                    best = max(best, axis(coords(m))[2])
        return best

    def shoot_left(self, d):
        """The left fist in any frame of "shoot" facing d (for frames where it is hidden)."""
        for f in (1, 2, 0, 3):
            left = self.pose("shoot", f, d)["left"]
            if left is not None:
                return left
        return None

    def body_mask(self, anim, f, d):
        m = np.zeros((CH, CW), bool)
        for layer in self.body + [self.hands]:
            m |= layer.frame(anim, f, d)[..., 3] >= 100
        return m

    def hand_mask(self, anim, f, d):
        h = self.hands.frame(anim, f, d)
        return (h[..., 3] >= 200) & (h[..., :3].mean(-1) > 70)

    def pose(self, anim, f, d):
        key = (anim, f, d)
        if key not in self.poses:
            self.poses[key] = self.find_pose(anim, f, d)
        return self.poses[key]

    def find_pose(self, anim, f, d):
        p = {"face": facing(d)}
        hand = self.hand_mask(anim, f, d)
        hp = coords(hand)
        p["hand_mask"] = hand
        # Right fist: the wand's grip end.
        wm = self.wand.frame(anim, f, d)[..., 3] >= 128
        p["wand_mask"] = wm
        right = None
        if wm.sum() > 6:
            e0, e1, length = axis(coords(wm))
            d0, d1 = nearest(hp, e0), nearest(hp, e1)
            if min(d0, d1) > 9:  # the fist is hidden: the grip is the end nearer the chest
                chest = coords(self.body[0].frame(anim, f, d)[..., 3] >= 128)
                c = chest.mean(0) if len(chest) else np.zeros(2)
                grip, tip = (e0, e1) if np.linalg.norm(e0 - c) < np.linalg.norm(e1 - c) else (e1, e0)
            else:
                grip, tip = (e0, e1) if d0 < d1 else (e1, e0)
                near = hp[np.linalg.norm(hp - grip, axis=1) < 7]
                if len(near) >= 4:
                    right = near.mean(0)
            p["wand"] = (grip, tip, length / self.wand_max)
            p["wand_grip"] = right if right is not None else grip
        p["right"] = right
        # Left fist: where the longbow meets a hand that is not the right one.
        bp = coords(self.bow.frame(anim, f, d)[..., 3] >= 128)
        left = None
        cand = hp if right is None else hp[np.linalg.norm(hp - right, axis=1) > 11]
        if len(cand) and len(bp):
            dist = np.array([nearest(bp, q) for q in cand[::2]])
            touching = cand[::2][dist < 2.5]
            if len(touching) >= 2:
                c = touching.mean(0)
                near = cand[np.linalg.norm(cand - c, axis=1) < 7]
                left = near.mean(0) if len(near) >= 4 else c
        p["left"] = left
        if left is not None:
            arm = cand[np.linalg.norm(cand - left, axis=1) < 22]
            if len(arm) > 8:
                e0, e1, _ = axis(arm)
                elbow = e0 if np.linalg.norm(e0 - left) > np.linalg.norm(e1 - left) else e1
                p["left_arm"] = left - elbow
        # Staff: its gem marks the head.
        st = self.staff.frame(anim, f, d)
        sm = st[..., 3] >= 128
        p["staff_mask"] = sm
        if sm.sum() > 10:
            e0, e1, length = axis(coords(sm))
            rgb = st[..., :3].astype(int)
            gem = coords(sm & (rgb[..., 1] > rgb[..., 0] + 40) & (rgb[..., 1] > rgb[..., 2] + 40))
            if len(gem) >= 3:
                g = gem.mean(0)
                head, foot = (e0, e1) if np.linalg.norm(e0 - g) < np.linalg.norm(e1 - g) else (e1, e0)
            else:
                ref = right if right is not None else (e0 + e1) / 2
                head, foot = (e0, e1) if np.linalg.norm(e0 - ref) > np.linalg.norm(e1 - ref) else (e1, e0)
            p["staff"] = (head, foot, length / self.staff_max)
        return p


# ---------------------------------------------------------------- posing each gun

def hold_out(gun, body, front, r):
    out = gun.copy()
    hidden = body & ~dilate(front, r)
    out[hidden, 3] = 0
    return out


def fist_area(hand_mask, at, r=8):
    if at is None:
        return np.zeros_like(hand_mask)
    ys, xs = np.mgrid[0:CH, 0:CW]
    return hand_mask & ((xs - AX - at[0]) ** 2 + (ys - AY - at[1]) ** 2 < r * r)


def pose_pistol(poses, anim, f, d, model, scale):
    p = poses.pose(anim, f, d)
    face, body = p["face"], poses.body_mask(anim, f, d)
    if anim == "shoot":
        hand = p["left"]
        if hand is None:
            return None
        arm = p.get("left_arm")
        tilt = best_tilt(face, arm, -1.0, 0.5)[0] if arm is not None else 0.0
        if f in (1, 2):
            tilt = max(-0.15, min(0.15, tilt))
        forward = tilted(face, tilt)
        front = fist_area(p["hand_mask"], hand, 10) | (poses.bow.frame(anim, f, d)[..., 3] >= 128)
        gun = render(model, hand, forward, face, scale)
        return hold_out(gun, body, front, 3)
    if "wand" not in p:
        return None
    grip, tip, ratio = p["wand"]
    forward = unproject(tip - grip, max(ratio, 0.3), face)
    hand = p["wand_grip"]
    front = p["wand_mask"] | fist_area(p["hand_mask"], p["right"], 9)
    gun = render(model, hand, forward, face, scale)
    return hold_out(gun, body, front, 4)


def pose_offhand(poses, anim, f, d, model, scale):
    p = poses.pose(anim, f, d)
    face, body = p["face"], poses.body_mask(anim, f, d)
    if anim == "shoot":
        hand = p["right"] if p["right"] is not None else p.get("wand_grip")
        if hand is None:
            return None
        forward = tilted(face, 0.95 if f in (1, 2) else -0.4)
        front = fist_area(p["hand_mask"], p["right"], 10) | p["wand_mask"]
        gun = render(model, hand, forward, face, scale)
        return hold_out(gun, body, front, 3)
    hand = p["left"]
    if hand is None:
        return None
    arm = p.get("left_arm")
    tilt = best_tilt(face, arm, -1.1, 1.1)[0] * 0.7 if arm is not None else -0.3
    forward = tilted(face, tilt)
    front = fist_area(p["hand_mask"], hand, 10)
    gun = render(model, hand, forward, face, scale)
    return hold_out(gun, body, front, 3)


def pose_long_gun(poses, anim, f, d, model, scale):
    p = poses.pose(anim, f, d)
    face, body = p["face"], poses.body_mask(anim, f, d)
    if anim == "shoot":
        # Shouldered along the facing: the left fist (which the bow pose thrusts at the target) under the fore-stock,
        # the butt back at the shoulder. The bow's draw hand flies back on release, so it is no guide here.
        left, arm = p["left"], p.get("left_arm")
        if left is None:
            left = poses.shoot_left(d)
            if left is None:
                return None
        if f in (1, 2):
            tilt = 0.0
        else:
            tilt = max(-0.8, min(0.0, best_tilt(face, arm, -1.0, 0.3)[0])) if arm is not None else -0.5
        forward = tilted(face, tilt)
        origin = left - project(forward * 30 * scale)
        front = fist_area(p["hand_mask"], p["left"], 12) | fist_area(p["hand_mask"], p["right"], 10)
        ys, xs = np.mgrid[0:CH, 0:CW]
        ab = left - origin
        t = np.clip(((xs - AX - origin[0]) * ab[0] + (ys - AY - origin[1]) * ab[1]) / max(1e-6, ab @ ab), 0, 1)
        dx, dy = xs - AX - (origin[0] + t * ab[0]), ys - AY - (origin[1] + t * ab[1])
        if p["left"] is not None:
            front |= dx * dx + dy * dy < 16
        gun = render(model, origin, forward, face, scale)
        if d in (5, 6, 7) and f in (1, 2):
            return gun  # aimed toward the camera: in front of the body
        return hold_out(gun, body, front, 4)
    if "staff" not in p:
        return None
    head, foot, ratio = p["staff"]
    forward = unproject(head - foot, max(ratio, 0.25), face)
    if p["right"] is not None:
        origin = p["right"]
    else:  # the fist is hidden: a third of the way up the staff
        origin = foot + (head - foot) * 0.35
    front = p["staff_mask"] | fist_area(p["hand_mask"], p["right"], 10)
    gun = render(model, origin, forward, face, scale)
    return hold_out(gun, body, front, 5)


# ---------------------------------------------------------------- packing and writing

def crop(arr):
    if arr is None:
        return None
    ys, xs = np.nonzero(arr[..., 3] > 0)
    if len(xs) == 0:
        return None
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    return arr[y0:y1, x0:x1], AX - x0, AY - y0


def pack(pieces, width=1024):
    """Shelf-packs (key, image) pairs, tallest first. Returns the sheet and key -> (x, y). Pixel (0, 0) stays empty."""
    order = sorted(pieces, key=lambda kv: -kv[1].shape[0])
    places, x, y, shelf = {}, 2, 0, 0
    for key, img in order:
        h, w = img.shape[:2]
        if x + w > width:
            x, y, shelf = 0, y + shelf + 1, 0
        places[key] = (x, y)
        x += w + 1
        shelf = max(shelf, h)
    sheet = np.zeros((y + shelf + 1, width, 4), np.uint8)
    for key, img in pieces:
        px, py = places[key]
        sheet[py:py + img.shape[0], px:px + img.shape[1]] = img
    return sheet, places


def write_layer(name, sections, frames, kind="avatar/male"):
    """frames: {(section, f, d): (image, ox, oy) or None}. Empty frames point at the sheet's blank pixel (0, 0)."""
    pieces = [(k, v[0]) for k, v in frames.items() if v is not None]
    sheet, places = pack(pieces)
    image = f"images/{kind}/{name}.png"
    Image.fromarray(sheet).save(os.path.join(FLARE, image), optimize=True)
    lines = [f"# Generated by tools/build_guns.py from Flare's avatar layers; do not edit by hand.", "",
             f"image={image}", ""]
    for section, meta, source_frames in sections:
        lines.append(f"[{section}]")
        lines += meta
        for f, d in sorted(source_frames):
            v = frames.get((section, f, d))
            if v is None:
                lines.append(f"frame={f},{d},0,0,1,1,0,0")
            else:
                px, py = places[(section, f, d)]
                img, ox, oy = v
                lines.append(f"frame={f},{d},{px},{py},{img.shape[1]},{img.shape[0]},{ox},{oy}")
        lines.append("")
    with open(os.path.join(FLARE, f"animations/{kind}/{name}.txt"), "w") as out:
        out.write("\n".join(lines))
    return sheet.shape


def build_layer(poses, name, source, posing, model, scale):
    frames = {}
    for section, meta, source_frames in source.sections:
        for f, d in source_frames:
            frames[(section, f, d)] = crop(posing(poses, section, f, d, model, scale))
    shape = write_layer(name, source.sections, frames)
    print(f"{name}: {sum(v is not None for v in frames.values())}/{len(frames)} frames drawn, sheet {shape[1]}x{shape[0]}")
    return frames


# ---------------------------------------------------------------- loot

def build_loot(name, model, scale, shadow_size=16):
    """Six frames of a tumble to the ground, as Flare's loot: frames 0-4 in the air over a shadow, 5 lying down."""
    W, H, ax, ay = 220, 340, 110, 300
    heights = [150, 190, 200, 160, 70, 0]
    spins = [0.3, 1.5, 2.6, 3.7, 4.9, 6.28]
    frames = {}
    for i, (h, spin) in enumerate(zip(heights, spins)):
        if i < 5:
            forward = unit(np.array([math.cos(spin), 0.35 * math.sin(spin), 0.55 * math.sin(spin * 1.3)]))
            img = render(model, (0, 0), forward, np.array([0, -1.0, 0]), scale, (W, H), (ax, ay - h))
        else:  # lying on its side: its "up" is a ground direction across the barrel
            f = unit(np.array([1.0, 0.45, 0.0]))
            u = unit(np.cross([0, 0, 1.0], f))
            img = render(model, (0, 0), f, None, scale, (W, H), (ax, ay), frame=(f, np.cross(u, f), u))
        shadow = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
        sd = ImageDraw.Draw(shadow)
        rx = (shadow_size if i == 5 else shadow_size * 0.75) * scale * SS
        sd.ellipse([ax * SS - rx, ay * SS - rx * 0.35, ax * SS + rx, ay * SS + rx * 0.35], fill=(0, 0, 0, 70))
        sh = np.asarray(shadow.reduce(SS)).copy()
        out = Image.fromarray(sh)
        out.alpha_composite(Image.fromarray(img))
        arr = np.asarray(out)
        ys, xs = np.nonzero(arr[..., 3] > 0)
        x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
        frames[("power", i, 0)] = (arr[y0:y1, x0:x1], ax - x0, ay - y0)
    sections = [("power", ["frames=6", "duration=600ms", "type=play_once"], {(i, d): None for i in range(6) for d in range(8)})]
    shape = write_layer(name, sections, frames, kind="loot")
    print(f"loot {name}: sheet {shape[1]}x{shape[0]}")
    return frames


# ---------------------------------------------------------------- contact sheets

def contact_sheet(prefix, gun_frames, gun_slot, sections, extra=None):
    """One PNG per animation, at 2x: its frames down, the eight directions across, over the default body drawn in
    hero_layers.txt's order with the gun in `gun_slot` (and `extra` = (slot, frames), a second gun)."""
    order = {0: "SW", 1: "W", 2: "NW", 3: "N", 4: "NE", 5: "E", 6: "SE", 7: "S"}
    layers = {}
    for line in open(os.path.join(FLARE, "hero_layers.txt")):
        if line.startswith("layer="):
            parts = line.strip()[6:].split(",")
            layers[parts[0]] = parts[1:]
    body = {"feet": Layer("default_feet"), "legs": Layer("default_legs"), "hands": Layer("default_hands"),
            "chest": Layer("default_chest"), "head": Layer("head_short")}
    cell_w, cell_h = 150, 170
    for anim, _, _ in sections:
        count = frames_of(next(s for s in sections if s[0] == anim))
        sheet = Image.new("RGBA", (cell_w * 8, cell_h * count), (88, 92, 84, 255))
        draw = ImageDraw.Draw(sheet)
        for f in range(count):
            for d in range(8):
                canvas = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
                for slot in layers[order[d]]:
                    if slot in body:
                        canvas.alpha_composite(Image.fromarray(body[slot].frame(anim, f, d)))
                    for gslot, frames in ((gun_slot, gun_frames),) + ((extra,) if extra else ()):
                        if slot == gslot and frames.get((anim, f, d)) is not None:
                            img, ox, oy = frames[(anim, f, d)]
                            canvas.alpha_composite(Image.fromarray(img), (AX - ox, AY - oy))
                cell = canvas.crop((AX - cell_w // 2, AY - cell_h + 25, AX + cell_w // 2, AY + 25))
                sheet.alpha_composite(cell, (d * cell_w, f * cell_h))
            draw.text((4, f * cell_h + 4), f"{anim} {f}", fill=(255, 255, 255, 255))
        sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(f"{prefix}_{anim}.png")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheets", help="also write contact sheets to this folder")
    args = ap.parse_args()
    poses = Poses()
    pistol = build_layer(poses, "pistol", poses.wand, pose_pistol, pistol_model(), 1.0)
    offhand = build_layer(poses, "pistol_offhand", poses.wand, pose_offhand, pistol_model(), 1.0)
    long_gun = build_layer(poses, "long_gun", poses.staff, pose_long_gun, long_gun_model(), 1.0)
    loot_p = build_loot("pistol", pistol_model(), 1.4)
    loot_l = build_loot("long_gun", long_gun_model(), 0.75, shadow_size=40)
    if args.sheets:
        os.makedirs(args.sheets, exist_ok=True)
        secs = poses.wand.sections
        contact_sheet(os.path.join(args.sheets, "pistol"), pistol, "main", secs)
        contact_sheet(os.path.join(args.sheets, "twin_pistols"), pistol, "main", secs, extra=("off", offhand))
        contact_sheet(os.path.join(args.sheets, "long_gun"), long_gun, "main", secs)
        for n, fr in (("loot_pistol.png", loot_p), ("loot_long_gun.png", loot_l)):
            row = Image.new("RGBA", (220 * 6, 340), (88, 92, 84, 255))
            for i in range(6):
                img, ox, oy = fr[("power", i, 0)]
                row.alpha_composite(Image.fromarray(img), (220 * i + 110 - ox, 300 - oy))
            row.resize((row.width * 2, row.height * 2), Image.NEAREST).save(os.path.join(args.sheets, n))


if __name__ == "__main__":
    main()
