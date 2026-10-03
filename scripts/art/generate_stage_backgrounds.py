#!/usr/bin/env python3
"""
Generates the top-down background set for each Salinlahi stage: base tiles, a scatter
layer, and side-margin scenery. Also renders the side-by-side preview to docs/art/.

House style, read off the tiles already in the project (tileset_map01_foliage.png and
tileset_map01_ground.png): 32 px tiles at 32 PPU, palettes of 6-8 colours, hue-shifted
ramps (darks go red or blue, lights go yellow), no black keyline on terrain, light from
the top-right, detail carried in 1-3 px clusters rather than per-pixel noise.

Three layers, because each solves a different problem:

  base tiles   quiet surface, 8 variants + 2 "worn" variants for the enemy track.
               Anything baked in here repeats on the 32 px lattice, so they stay plain.
  scatter      small transparent sprites sown at arbitrary sub-tile positions. This is
               what breaks the lattice. Lit elements are never mirrored; the key light
               would flip.
  margins      64 px strips of scenery either side of the lane, tiling in Y with two
               variants per side. A darker inner edge reads as "you cannot walk here".

Stage settings are taken from the art that exists, since the design docs do not
describe them: Ugat is the jungle village behind the map01 palisade, Ugnayan the
sandstone town behind the map02 gate, Pamana the slate town behind the map03 gate.

Usage:  python3 scripts/art/generate_stage_backgrounds.py [--out DIR] [--preview]
"""

import argparse
import glob
import os
import re
import uuid

import numpy as np
from PIL import Image, ImageDraw

TILE = 32
MARGIN_W = 64
MARGIN_H = 256
DIRS = ((1, 0), (-1, 0), (0, 1), (0, -1))


def hx(*vals):
    return [tuple(int(v[i:i + 2], 16) for i in (1, 3, 5)) for v in vals]


# Lifted verbatim from tileset_map01_foliage.png. The only green in the project, so
# every stage's plant life uses it; the dark steps for moss and weeds, the full ramp
# only for tree crowns in the margins where a bush is what is being drawn.
GRASS = hx("#253f1c", "#34581f", "#456826", "#60802e", "#819939", "#a4af48", "#cac74f")

# The slate accent from tileset_map01_ground.png. Used for standing water in Ugat, so
# puddles get a cool tone without a colour that is not already in the project.
AZULEJO_BLUE = hx("#414a65", "#51607b")

STAGES = {
    "ugat": {
        "label": "Ugat — jungle village (map01 palisade)",
        "wall": "sprite_map01_base_zone.png",
        # Anchored on #875618, the road brown the shipped background already uses.
        "ground": hx("#2e1d0e", "#452a11", "#5e3a15", "#744917", "#875618", "#a27025", "#be9139"),
        "material": "earth",
        "water": AZULEJO_BLUE,
        "leaf": (GRASS[1], GRASS[2]),
        # Weights: quiet elements dominate; water is rare because a cool blue on warm
        # earth is the highest-contrast thing on the lane and ~50 of them read as paint.
        "scatter": {"grit": 14, "pebbles": 8, "stone_sm": 8, "leaf": 8, "crack": 6, "moss": 6,
                    "grass_sm": 6, "stone_md": 5, "twig": 4, "grass_lg": 3, "root": 3,
                    "stone_lg": 2, "puddle": 1.5},
        "margin": "jungle",
    },
    "ugnayan": {
        "label": "Ugnayan — sandstone town (map02 gate)",
        "wall": "sprite_map02_base_zone.png",
        # Sampled from the map02 wall: #5d4946 #78513a #9e7758 #ae8f67 #c3a76c #b8ab95 #d5cdb3
        "ground": hx("#3d2f2b", "#5d4946", "#78513a", "#9e7758", "#ae8f67", "#c3a76c", "#d5cdb3"),
        "material": "sand",
        # The Japanese shrine's blue-greys: rooftops, cool stones, standing water.
        "accent": hx("#343861", "#4a4f80", "#545e8a", "#6e78a6", "#97aaca", "#b4c6dc"),
        "glow": (0x38, 0xf0, 0xff),
        "water": (hx("#4a4f80")[0], hx("#6e78a6")[0]),
        "leaf": (GRASS[1], GRASS[2]),
        "scatter": {"grit": 14, "sand_ripple": 12, "pebbles": 8, "dry_grass": 8, "flagstone": 6,
                    "slab_frag": 5, "crack": 5, "leaf": 5, "moss": 3, "cool_stone": 2, "puddle": 1.5},
        "margin": "sandstone_town",
    },
    "pamana": {
        "label": "Pamana — slate town (map03 gate)",
        "wall": "sprite_map03_base_zone.png",
        # Sampled from the map03 wall: #080b11 #1f223e #343a65 #434c70 #526386 #687d97 #84a0b6
        "ground": hx("#0f1220", "#1f223e", "#343a65", "#434c70", "#526386", "#687d97", "#84a0b6"),
        "material": "cobble",
        # The American shrine's carved wood and gold: autumn leaves, lamp light, ridge lines.
        "accent": hx("#4a2316", "#7f371f", "#9d5524", "#c6894e", "#e5b662"),
        "glow": (0xe5, 0xb6, 0x62),
        "water": (hx("#343a65")[0], hx("#84a0b6")[0]),
        "leaf": (hx("#9d5524")[0], hx("#c6894e")[0]),
        # Wet patches and cracks are kept rare and one step under the lane: a dark blob
        # on this stage is a hole a dark enemy can stand in and disappear.
        "scatter": {"grit": 12, "moss": 10, "leaf": 8, "loose_cobble": 5, "crack": 4,
                    "wet_patch": 2, "puddle": 2, "gold_fleck": 2},
        "quiet_detail": True,
        "margin": "slate_town",
    },
}


# ---------------------------------------------------------------- primitives ---

def grow(size, rng, h=TILE, w=TILE, start=None, wrap=True):
    """Random-walk blob. Wrapping walks make seamless tiles; the reference tiles are
    clumpy, and a threshold on smooth noise gives round blobs instead."""
    sy, sx = start if start else (int(rng.integers(0, h)), int(rng.integers(0, w)))
    if not wrap and not (0 <= sy < h and 0 <= sx < w):
        return set()  # an off-canvas start can never expand; caller wanted an overhang
    cells = {(sy, sx)}
    frontier = [(sy, sx)]
    budget = size * 40  # the frontier is never popped, so bound the walk explicitly
    while len(cells) < size and budget > 0:
        budget -= 1
        cy, cx = frontier[int(rng.integers(0, len(frontier)))]
        dy, dx = DIRS[int(rng.integers(0, 4))]
        ny, nx = cy + dy, cx + dx
        if wrap:
            ny, nx = ny % h, nx % w
        elif not (0 <= ny < h and 0 <= nx < w):
            continue
        if (ny, nx) not in cells:
            cells.add((ny, nx))
            frontier.append((ny, nx))
    return cells


def canvas(h, w):
    return np.zeros((h, w, 3), dtype=np.uint8), np.zeros((h, w), dtype=np.uint8)


def put(rgb, alpha, y, x, colour):
    if 0 <= y < rgb.shape[0] and 0 <= x < rgb.shape[1]:
        rgb[y, x] = colour
        alpha[y, x] = 255


def paint(rgb, cells, colour, alpha=None):
    for y, x in cells:
        rgb[y, x] = colour
        if alpha is not None:
            alpha[y, x] = 255


def lit_blob(rgb, alpha, cells, body, hi, lo, h, w):
    """Fill a blob, then shade its rim by the top-right key: highlight on the top/right
    edge cells, shadow cast on the row below and column left of the silhouette."""
    for y, x in cells:
        put(rgb, alpha, y, x, body)
    for y, x in cells:
        if (y - 1, x) not in cells or (y, x + 1) not in cells:
            put(rgb, alpha, y, x, hi)
    for y, x in cells:
        if (y + 1, x) not in cells:
            put(rgb, alpha, y + 1, x, lo)
        if (y, x - 1) not in cells:
            put(rgb, alpha, y, x - 1, lo)


def sunk_blob(rgb, alpha, cells, body, rim_dark, rim_lit, h, w):
    """The inverse: a depression is dark on the key side and lit on the far side."""
    for y, x in cells:
        put(rgb, alpha, y, x, body)
    for y, x in cells:
        if (y - 1, x) not in cells:
            put(rgb, alpha, y - 1, x, rim_dark)
        if (y + 1, x) not in cells:
            put(rgb, alpha, y + 1, x, rim_lit)


# ---------------------------------------------------------------- base tiles ---

def tile_earth(rng, ramp, worn=False):
    img = np.zeros((TILE, TILE, 3), dtype=np.uint8)
    img[:, :] = ramp[4]
    for _ in range(4):
        paint(img, grow(int(rng.integers(45, 85)), rng), ramp[3])
    for _ in range(3):
        paint(img, grow(int(rng.integers(35, 65)), rng), ramp[5])
    for _ in range(10):
        paint(img, grow(int(rng.integers(5, 14)), rng), ramp[int(rng.integers(3, 6))])
    grain(img, rng, ramp, 18)
    if worn:
        worn_track(img, rng, ramp)
    return img


def tile_sand(rng, ramp, worn=False):
    """Packed sand: lighter, smoother than earth, with faint wind ripples."""
    img = np.zeros((TILE, TILE, 3), dtype=np.uint8)
    img[:, :] = ramp[4]
    for _ in range(3):
        paint(img, grow(int(rng.integers(50, 90)), rng), ramp[3])
    for _ in range(4):
        paint(img, grow(int(rng.integers(30, 70)), rng), ramp[5])
    # Ripples: short horizontal runs one step lighter, offset row to row.
    for _ in range(7):
        y, x = int(rng.integers(0, TILE)), int(rng.integers(0, TILE))
        for t in range(int(rng.integers(3, 7))):
            img[y, (x + t) % TILE] = ramp[5]
    grain(img, rng, ramp, 10)
    if worn:
        worn_track(img, rng, ramp)
    return img


def tile_cobble(rng, ramp, worn=False):
    """Cobblestones on a jittered brick grid, positions taken modulo the tile so the
    pattern wraps. Three things keep it from reading as a grid: +/-2 px jitter, a
    12% chance a stone is missing, and a highlight that is one step above each
    stone's own body rather than a fixed bright colour."""
    img = np.zeros((TILE, TILE, 3), dtype=np.uint8)
    img[:, :] = ramp[4]  # the joints: one step under the stones, not the ramp floor
    pitch = 8
    for row in range(TILE // pitch):
        for col in range(TILE // pitch):
            if rng.random() < 0.12:
                continue
            cy = (row * pitch + pitch // 2 + int(rng.integers(-2, 3))) % TILE
            cx = (col * pitch + pitch // 2 + (pitch // 2 if row % 2 else 0) + int(rng.integers(-2, 3))) % TILE
            # Stones live on the upper half of the ramp so the lane reads light
            # against the dark corruption sprites and the indigo protagonist.
            body_i = int(rng.integers(5, 7))
            cells = grow(int(rng.integers(16, 30)), rng, start=(cy, cx))
            cells = {c for c in cells
                     if abs(((c[0] - cy + 16) % 32) - 16) <= 3 and abs(((c[1] - cx + 16) % 32) - 16) <= 4}
            for y, x in cells:
                img[y, x] = ramp[body_i]
            for y, x in cells:
                if ((y - 1) % TILE, x) not in cells and rng.random() > 0.35:
                    img[y, x] = ramp[min(6, body_i + 1)]
                if ((y + 1) % TILE, x) not in cells:
                    img[(y + 1) % TILE, x] = ramp[3]
    if worn:
        cx0 = TILE // 2
        for y in range(TILE):
            for x in range(cx0 - 6, cx0 + 6):
                if tuple(img[y, x]) == tuple(ramp[5]) and rng.random() > 0.55:
                    img[y, x] = ramp[6]
    return img


def grain(img, rng, ramp, count):
    for _ in range(count):
        y, x = int(rng.integers(0, TILE)), int(rng.integers(0, TILE))
        colour = ramp[3] if rng.random() > 0.5 else ramp[5]
        run = int(rng.integers(1, 4))
        horizontal = rng.random() > 0.45
        for t in range(run):
            yy = y if horizontal else (y + t) % TILE
            xx = (x + t) % TILE if horizontal else x
            img[yy, xx] = colour


def worn_track(img, rng, ramp):
    """A faint compacted band down the tile centre. Two of these placed in the
    centre columns give the lane a trodden path without a drawn line."""
    for y in range(TILE):
        wobble = int(round(np.sin(y / TILE * 2 * np.pi) * 2))
        for x in range(TILE // 2 - 7 + wobble, TILE // 2 + 7 + wobble):
            if rng.random() > 0.35:
                img[y, x % TILE] = ramp[3]


MATERIALS = {"earth": tile_earth, "sand": tile_sand, "cobble": tile_cobble}


# ------------------------------------------------------------------- scatter ---
# Each returns (rgb, alpha, flippable). Flippable only when the element has no lighting.

def sc_stone(rng, st, size):
    ramp = st["ground"]
    n, span = {"sm": (4, 6), "md": (9, 8), "lg": (16, 11)}[size]
    rgb, alpha = canvas(span, span)
    cells = grow(n, rng, span - 2, span - 2, start=((span - 2) // 2, (span - 2) // 2), wrap=False)
    lit_blob(rgb, alpha, cells, ramp[5], ramp[6], ramp[1], span, span)
    return rgb, alpha, False


def sc_pebbles(rng, st):
    ramp = st["ground"]
    rgb, alpha = canvas(9, 10)
    for _ in range(int(rng.integers(3, 6))):
        cy, cx = int(rng.integers(1, 7)), int(rng.integers(1, 8))
        put(rgb, alpha, cy, cx, ramp[5])
        if rng.random() > 0.5:
            put(rgb, alpha, cy, cx + 1, ramp[5])
        put(rgb, alpha, cy + 1, cx, ramp[1])
    return rgb, alpha, False


def sc_grass(rng, st, tall, ramp=None, steps=(0, 3)):
    ramp = ramp or GRASS
    h = 9 if tall else 6
    rgb, alpha = canvas(h, 9)
    for blade in range(int(rng.integers(3, 6))):
        bx = 1 + blade * 2 + int(rng.integers(0, 2))
        length = int(rng.integers(3, h - 1))
        lean = rng.random() > 0.5
        for t in range(length):
            xx = bx + (1 if lean and t > length // 2 else 0)
            step = steps[0] + int((t / max(1, length - 1)) * (steps[1] - steps[0] + 0.4))
            put(rgb, alpha, h - 1 - t, xx, ramp[min(step, steps[1])])
    return rgb, alpha, False


def sc_crack(rng, st):
    ramp = st["ground"]
    q = 1 if st.get("quiet_detail") else 0
    rgb, alpha = canvas(13, 13)
    y, x = int(rng.integers(2, 11)), 0
    for _ in range(int(rng.integers(8, 14))):
        put(rgb, alpha, y, x, ramp[1 + q])
        if rng.random() > 0.75:
            put(rgb, alpha, y + 1, x, ramp[2 + q])
        x += 1
        y += int(rng.integers(-1, 2))
        if not (0 <= y < 13 and 0 <= x < 13):
            break
    return rgb, alpha, True


def sc_twig(rng, st):
    ramp = st["ground"]
    rgb, alpha = canvas(7, 10)
    y = 3
    for x in range(int(rng.integers(5, 10))):
        put(rgb, alpha, y, x, ramp[1])
        if x == 3:
            put(rgb, alpha, y - 1, x + 1, ramp[1])
            put(rgb, alpha, y - 2, x + 2, ramp[1])
        if rng.random() > 0.7:
            y = min(6, y + 1)
    return rgb, alpha, True


def sc_grit(rng, st):
    ramp = st["ground"]
    rgb, alpha = canvas(6, 6)
    for _ in range(int(rng.integers(2, 5))):
        put(rgb, alpha, int(rng.integers(0, 6)), int(rng.integers(0, 6)),
            ramp[2] if rng.random() > 0.5 else ramp[6])
    return rgb, alpha, True


def sc_moss(rng, st):
    """A low cushion of moss: dark foliage steps, no highlight, sits in the surface."""
    rgb, alpha = canvas(9, 11)
    cells = grow(int(rng.integers(14, 30)), rng, 9, 11, start=(4, 5), wrap=False)
    for y, x in cells:
        put(rgb, alpha, y, x, GRASS[1] if rng.random() > 0.3 else GRASS[2])
    for y, x in cells:
        if (y + 1, x) not in cells:
            put(rgb, alpha, y + 1, x, GRASS[0])
    return rgb, alpha, False


def sc_root(rng, st):
    """A tree root surfacing through the lane: a thick dark curve with a lit top edge."""
    ramp = st["ground"]
    rgb, alpha = canvas(9, 18)
    y = 5.0
    for x in range(int(rng.integers(10, 18))):
        y += np.sin(x / 3.0) * 0.6
        iy = int(round(y))
        put(rgb, alpha, iy, x, ramp[1])
        put(rgb, alpha, iy + 1, x, ramp[0])
        put(rgb, alpha, iy - 1, x, ramp[2])
    return rgb, alpha, False


def sc_puddle(rng, st):
    """Standing water: a sunk oval in the stage's cool tone, lit rim on the far side."""
    body, hi = st["water"]
    ramp = st["ground"]
    rgb, alpha = canvas(9, 13)
    cells = grow(int(rng.integers(18, 34)), rng, 9, 13, start=(4, 6), wrap=False)
    sunk_blob(rgb, alpha, cells, body, ramp[1], ramp[5], 9, 13)
    # One reflected highlight, top-right of centre.
    ys, xs = zip(*cells)
    put(rgb, alpha, min(ys) + 1, max(xs) - 2, hi)
    return rgb, alpha, False


def sc_leaf(rng, st):
    a, b = st["leaf"]
    rgb, alpha = canvas(5, 6)
    cy, cx = 2, 2
    for dy, dx in ((0, 0), (0, 1), (0, 2), (-1, 1), (1, 1)):
        put(rgb, alpha, cy + dy, cx + dx, a)
    put(rgb, alpha, cy, cx + 1, b)
    return rgb, alpha, True


def sc_flagstone(rng, st):
    """A paving slab half-buried in sand: flat top one step lighter, dark joint."""
    ramp = st["ground"]
    h, w = int(rng.integers(7, 11)), int(rng.integers(9, 14))
    rgb, alpha = canvas(h + 1, w + 1)
    cells = {(y, x) for y in range(1, h - 1) for x in range(1, w - 1)}
    # Chip the corners so it does not read as a rectangle.
    for (y, x) in ((1, 1), (1, w - 2), (h - 2, 1), (h - 2, w - 2)):
        if rng.random() > 0.4:
            cells.discard((y, x))
    lit_blob(rgb, alpha, cells, ramp[5], ramp[6], ramp[2], h + 1, w + 1)
    return rgb, alpha, False


def sc_slab_frag(rng, st):
    ramp = st["ground"]
    rgb, alpha = canvas(7, 8)
    cells = grow(int(rng.integers(8, 16)), rng, 6, 7, start=(3, 3), wrap=False)
    lit_blob(rgb, alpha, cells, ramp[5], ramp[6], ramp[2], 7, 8)
    return rgb, alpha, False


def sc_sand_ripple(rng, st):
    ramp = st["ground"]
    rgb, alpha = canvas(5, 14)
    for row in range(int(rng.integers(2, 4))):
        y = row * 2
        x0 = int(rng.integers(0, 4))
        for t in range(int(rng.integers(6, 11))):
            put(rgb, alpha, y, x0 + t, ramp[5])
    return rgb, alpha, True


def sc_cool_stone(rng, st):
    """A blue-grey stone among the sand, in the shrine's palette."""
    acc = st["accent"]
    rgb, alpha = canvas(8, 8)
    cells = grow(int(rng.integers(6, 12)), rng, 6, 6, start=(3, 3), wrap=False)
    lit_blob(rgb, alpha, cells, acc[2], acc[4], acc[0], 8, 8)
    return rgb, alpha, False


def sc_loose_cobble(rng, st):
    ramp = st["ground"]
    rgb, alpha = canvas(8, 9)
    cells = grow(int(rng.integers(10, 18)), rng, 6, 7, start=(3, 3), wrap=False)
    q = 1 if st.get("quiet_detail") else 0
    lit_blob(rgb, alpha, cells, ramp[4 + q], ramp[6], ramp[0 + 2 * q], 8, 9)
    return rgb, alpha, False


def sc_wet_patch(rng, st):
    ramp = st["ground"]
    rgb, alpha = canvas(10, 12)
    q = 1 if st.get("quiet_detail") else 0
    cells = grow(int(rng.integers(20, 40)), rng, 10, 12, start=(5, 6), wrap=False)
    for y, x in cells:
        put(rgb, alpha, y, x, ramp[2 + q] if rng.random() > 0.25 else ramp[1 + q])
    return rgb, alpha, True


def sc_gold_fleck(rng, st):
    rgb, alpha = canvas(3, 3)
    put(rgb, alpha, 1, 1, st["glow"])
    put(rgb, alpha, 1, 2, st["accent"][3])
    return rgb, alpha, True


SCATTER_FNS = {
    "stone_sm": lambda r, s: sc_stone(r, s, "sm"),
    "stone_md": lambda r, s: sc_stone(r, s, "md"),
    "stone_lg": lambda r, s: sc_stone(r, s, "lg"),
    "pebbles": sc_pebbles,
    "grass_sm": lambda r, s: sc_grass(r, s, False),
    "grass_lg": lambda r, s: sc_grass(r, s, True),
    "dry_grass": lambda r, s: sc_grass(r, s, False, ramp=s["ground"], steps=(2, 5)),
    "crack": sc_crack,
    "twig": sc_twig,
    "grit": sc_grit,
    "moss": sc_moss,
    "root": sc_root,
    "puddle": sc_puddle,
    "leaf": sc_leaf,
    "flagstone": sc_flagstone,
    "slab_frag": sc_slab_frag,
    "sand_ripple": sc_sand_ripple,
    "cool_stone": sc_cool_stone,
    "loose_cobble": sc_loose_cobble,
    "wet_patch": sc_wet_patch,
    "gold_fleck": sc_gold_fleck,
}


# ------------------------------------------------------------------- margins ---
# 64 x 256 strips either side of the lane, tiling in Y, drawn in the 3/4 view the
# sprites and the base-zone walls already use: ground seen from above, anything
# vertical shows a lit top and a front face with a dark line where it meets the
# ground, and a thing lower on screen is nearer, so it is drawn later and covers
# what stands behind it.
#
# A strip is a list of props with a base y in [0, H). Each prop is instanced at
# y - H, y and y + H, the instances are sorted by base y and drawn with clipping,
# so painter's order holds across the wrap seam and the strip still tiles.

TRUNK = hx("#661e10", "#7d3616", "#92501e")   # the treeline sprite's browns


def fill(rgb, alpha, colour):
    rgb[:, :] = colour
    alpha[:, :] = 255


def draw_canopy(rgb, alpha, cy, cx, r, rng):
    """A tree crown at 3/4: lit toward the top-right, a dark band along the underside
    where the foliage overhangs, rim eroded so it is not a circle."""
    h, w = alpha.shape
    ry = int(r * 0.85)
    cells = set()
    for dy in range(-ry, ry + 1):
        for dx in range(-r, r + 1):
            if (dx / r) ** 2 + (dy / max(1, ry)) ** 2 <= 1.0:
                cells.add((cy + dy, cx + dx))
    for _ in range(r * 2):
        ang = rng.random() * 2 * np.pi
        sy, sx = int(cy + np.sin(ang) * ry), int(cx + np.cos(ang) * r)
        for c in grow(int(rng.integers(2, 6)), rng, h, w, start=(sy, sx), wrap=False):
            cells.discard(c)
    cells = {c for c in cells if 0 <= c[0] < h and 0 <= c[1] < w}
    for y, x in cells:
        put(rgb, alpha, y, x, GRASS[2])
    for _ in range(max(2, r // 2)):
        c0 = (int(rng.integers(cy - ry, cy + ry + 1)), int(rng.integers(cx - r, cx + r + 1)))
        if c0 in cells:
            for c in grow(int(rng.integers(4, 12)), rng, h, w, start=c0, wrap=False):
                if c in cells:
                    put(rgb, alpha, c[0], c[1], GRASS[3])
    for y, x in cells:
        dy, dx = y - cy, x - cx
        if dy > ry * 0.45:
            put(rgb, alpha, y, x, GRASS[0] if rng.random() > 0.3 else GRASS[1])
        elif dx - dy > r * 0.5 and rng.random() > 0.35:
            put(rgb, alpha, y, x, GRASS[4] if rng.random() > 0.3 else GRASS[5])
    for y, x in cells:
        if (y + 1, x) not in cells:
            put(rgb, alpha, y + 1, x, GRASS[0])
    return cells


def draw_tree(rgb, alpha, base_y, cx, r, rng):
    trunk_h = max(3, int(r * 0.55))
    for dx in range(-r // 2, r // 2 + 1):
        put(rgb, alpha, base_y, cx + dx, GRASS[0])
    for t in range(trunk_h):
        put(rgb, alpha, base_y - t, cx - 1, TRUNK[0])
        put(rgb, alpha, base_y - t, cx, TRUNK[1])
        put(rgb, alpha, base_y - t, cx + 1, TRUNK[2])
    draw_canopy(rgb, alpha, base_y - trunk_h - int(r * 0.7), cx, r, rng)


def draw_bush(rgb, alpha, base_y, cx, r, rng):
    draw_canopy(rgb, alpha, base_y - int(r * 0.6), cx, r, rng)


def draw_rock(rgb, alpha, base_y, cx, r, ramp, rng):
    """A boulder at 3/4: lit top surface, mid front face, dark base."""
    h, w = alpha.shape
    cells = grow(int(r * r * 1.6), rng, h, w, start=(base_y - r // 2, cx), wrap=False)
    cells = {c for c in cells if abs(c[1] - cx) <= r and 0 <= base_y - c[0] <= r + 1}
    top = base_y - r
    for y, x in cells:
        t = (y - top) / max(1, r)
        put(rgb, alpha, y, x, ramp[5] if t < 0.4 else ramp[3])
    for y, x in cells:
        if (y - 1, x) not in cells:
            put(rgb, alpha, y, x, ramp[6])
        if (y + 1, x) not in cells:
            put(rgb, alpha, y + 1, x, ramp[0])
        if (y, x - 1) not in cells:
            put(rgb, alpha, y, x, ramp[2])


def draw_lane_wall(rgb, alpha, side, cap, face, course, base_dark, wall_w=8):
    """A low wall along the lane: light top surface, a stone face toward the lane
    with horizontal courses, and a dark line at its foot."""
    h, w = alpha.shape
    for y in range(h):
        for i in range(wall_w):
            x = (w - wall_w + i) if side == "l" else (wall_w - 1 - i)
            on_cap = i < 3
            colour = cap if on_cap else (course if y % 5 == 0 else face)
            put(rgb, alpha, y, x, colour)
        put(rgb, alpha, y, (w - 1) if side == "l" else 0, base_dark)


def render_props(rgb, alpha, props):
    """props: (base_y, height, draw_fn). Instanced across the seam, sorted, drawn."""
    h = alpha.shape[0]
    inst = []
    for base_y, height, fn in props:
        for k in (-1, 0, 1):
            sy = base_y + k * h
            if sy - height < h and sy + 2 >= 0:
                inst.append((sy, fn))
    inst.sort(key=lambda t: t[0])
    for sy, fn in inst:
        fn(sy)


def margin_jungle(rng, st, side):
    h, w = MARGIN_H, MARGIN_W
    ramp = st["ground"]
    rgb, alpha = canvas(h, w)
    fill(rgb, alpha, GRASS[0])
    for _ in range(40):
        paint(rgb, grow(int(rng.integers(6, 22)), rng, h, w), GRASS[1])
    for _ in range(8):
        paint(rgb, grow(int(rng.integers(4, 10)), rng, h, w), ramp[2])
    outer = 0 if side == "l" else w - 1
    inner = w - 1 if side == "l" else 0
    props = []
    # Back row: big trees overhanging the outer edge. Middle row: trees. Front row,
    # nearest the lane: bushes and a few rocks. Enough of each that the canopies
    # overlap and the floor only shows in slivers.
    for i in range(12):
        r = int(rng.integers(13, 19))
        off = int(rng.integers(-8, 18))
        cx = outer + off if side == "l" else outer - off
        y = int(i * h / 12 + int(rng.integers(-6, 7))) % h
        props.append((y, r * 2 + 12, (lambda sy, cx=cx, r=r: draw_tree(rgb, alpha, sy, cx, r, rng))))
    for i in range(10):
        r = int(rng.integers(10, 15))
        off = int(rng.integers(20, 40))
        cx = outer + off if side == "l" else outer - off
        y = int(i * h / 10 + 12 + int(rng.integers(-8, 9))) % h
        props.append((y, r * 2 + 12, (lambda sy, cx=cx, r=r: draw_tree(rgb, alpha, sy, cx, r, rng))))
    for i in range(9):
        r = int(rng.integers(5, 9))
        off = int(rng.integers(4, 16))
        cx = inner - off if side == "l" else inner + off
        y = int(i * h / 9 + int(rng.integers(-10, 11))) % h
        props.append((y, r * 2, (lambda sy, cx=cx, r=r: draw_bush(rgb, alpha, sy, cx, r, rng))))
    for _ in range(3):
        r = int(rng.integers(4, 7))
        off = int(rng.integers(6, 20))
        cx = inner - off if side == "l" else inner + off
        props.append((int(rng.integers(0, h)), r + 2, (lambda sy, cx=cx, r=r: draw_rock(rgb, alpha, sy, cx, r, ramp, rng))))
    render_props(rgb, alpha, props)
    # Roots reaching toward the lane, and the canopy's shadow on the lane edge.
    for _ in range(6):
        y = int(rng.integers(0, h))
        for t in range(int(rng.integers(5, 14))):
            x = inner - t if side == "l" else inner + t
            yy = (y + int(np.sin(t / 3.0) * 1.5)) % h
            put(rgb, alpha, yy, x, TRUNK[1])
            put(rgb, alpha, (yy + 1) % h, x, TRUNK[0])
    for y in range(h):
        put(rgb, alpha, y, inner, ramp[0])
        put(rgb, alpha, y, inner - 1 if side == "l" else inner + 1, GRASS[0])
    return rgb, alpha


BAYER4 = ((0, 8, 2, 10), (12, 4, 14, 6), (3, 11, 1, 9), (15, 7, 13, 5))


def margin_walled_shade(rng, st, side, cap, face, course, base_dark):
    """The town stages' margin: the lane wall, and beyond it the ground falling off
    into shade. Procedural houses did not reach the bar the ground tiles set, so the
    margin claims nothing it cannot draw: a wall that reads, and a vignette that pulls
    the eye to the lane. The fall-off is stepped through the stage's own ramp with
    ordered dithering between steps, so it stays in palette and in style. Real props,
    when they exist, drop into render_props without touching this."""
    h, w = MARGIN_H, MARGIN_W
    ramp = st["ground"]
    # Ground texture first, as ramp indices, so the shade darkens texture, not flat fill.
    # Mottling is heavier than on the lane tiles so the dithered fall-off breaks up
    # into ground rather than reading as parallel bands.
    idx = np.full((h, w), 4, dtype=int)
    for _ in range(48):
        for y, x in grow(int(rng.integers(8, 26)), rng, h, w):
            idx[y, x] = 3
    for _ in range(22):
        for y, x in grow(int(rng.integers(4, 12)), rng, h, w):
            idx[y, x] = 5
    xs = np.arange(w)
    dist = (w - 1 - xs) if side == "l" else xs          # 0 at the lane edge
    fall = dist / (w - 1) * 3.6                          # ramp steps to drop by the outer edge
    for y in range(h):
        for x in range(w):
            drop = int(np.floor(fall[x] + BAYER4[y % 4][x % 4] / 16.0))
            idx[y, x] = max(0, idx[y, x] - drop)
    rgb = np.array(ramp, dtype=np.uint8)[np.clip(idx, 0, 6)]
    alpha = np.full((h, w), 255, np.uint8)
    # A little moss at the foot of the wall, where damp collects.
    for _ in range(14):
        x0 = int(rng.integers(w - 20, w - 8)) if side == "l" else int(rng.integers(8, 20))
        for y, x in grow(int(rng.integers(3, 8)), rng, h, w, start=(int(rng.integers(0, h)), x0)):
            if idx[y, x] >= 2:
                rgb[y, x] = GRASS[int(rng.integers(0, 2))]
    draw_lane_wall(rgb, alpha, side, cap, face, course, base_dark)
    return rgb, alpha


def margin_sandstone_town(rng, st, side):
    r = st["ground"]
    return margin_walled_shade(rng, st, side, r[6], r[4], r[2], r[1])


def margin_slate_town(rng, st, side):
    # One step quieter than the sandstone wall on every part: the lane is dark here,
    # so a bright cap and black courses became the loudest thing on screen.
    r = st["ground"]
    return margin_walled_shade(rng, st, side, r[6], r[3], r[1], r[0])


MARGINS = {"jungle": margin_jungle, "sandstone_town": margin_sandstone_town, "slate_town": margin_slate_town}


def rgba(rgb, alpha=None):
    if alpha is None:
        alpha = np.full(rgb.shape[:2], 255, np.uint8)
    return Image.fromarray(np.dstack([rgb, alpha]))


def write_meta(png, template):
    meta = png + ".meta"
    if os.path.exists(meta):
        return
    open(meta, "w").write(re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + uuid.uuid4().hex,
                                 template, count=1, flags=re.M))


def meta_template(here):
    tpl = open(os.path.join(here, "Assets/Art/Environment/tileset_map01_ground.png.meta")).read()
    tpl = re.sub(r"\n  spriteMode: \d+", "\n  spriteMode: 1", tpl, count=1)
    tpl = re.sub(r"\n    spriteID: [0-9a-f]*", "\n    spriteID: ", tpl)
    tpl = tpl.replace("    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n", "    wrapU: 0\n    wrapV: 0\n    wrapW: 0\n")
    tpl = re.sub(r"(\n    textureCompression: )1", r"\g<1>0", tpl)
    # Readable: StageBackgroundBaker composites these on the CPU into one texture.
    return tpl.replace("  isReadable: 0\n", "  isReadable: 1\n")


def generate(stage, st, out, seed, tpl):
    d = os.path.join(out, stage.capitalize())
    os.makedirs(os.path.join(d, "Scatter"), exist_ok=True)
    os.makedirs(os.path.join(d, "Margin"), exist_ok=True)
    manifest = {
        "stage": stage,
        "ground": [f"tile_ground_{c}.png" for c in "abcdefgh"],
        "worn": [f"tile_ground_worn_{c}.png" for c in "ab"],
        "scatter": [],
        "marginLeft": [f"Margin/margin_l_{c}.png" for c in "abc"],
        "marginRight": [f"Margin/margin_r_{c}.png" for c in "abc"],
        "scatterPerTile": PER_TILE,
        "scatterMinDistancePx": MIN_DIST,
    }
    tiles = {}
    make = MATERIALS[st["material"]]
    for i, suffix in enumerate("abcdefgh"):
        rng = np.random.default_rng(seed + i * 101)
        p = os.path.join(d, f"tile_ground_{suffix}.png")
        img = make(rng, st["ground"])
        rgba(img).save(p)
        write_meta(p, tpl)
        tiles[f"ground_{suffix}"] = img
    for i, suffix in enumerate("ab"):
        rng = np.random.default_rng(seed + 500 + i * 101)
        p = os.path.join(d, f"tile_ground_worn_{suffix}.png")
        img = make(rng, st["ground"], worn=True)
        rgba(img).save(p)
        write_meta(p, tpl)
        tiles[f"worn_{suffix}"] = img
    scatter = []   # (sprite, flippable, weight)
    i = 0
    for name, weight in st["scatter"].items():
        # Frequent elements get more drawn variants so the same shape is not everywhere.
        for v in range(max(1, int(round(weight / 4)))):
            rng = np.random.default_rng(seed + 900 + i * 37)
            rgb, alpha, flip = SCATTER_FNS[name](rng, st)
            p = os.path.join(d, "Scatter", f"scatter_{name}_{v}.png")
            rgba(rgb, alpha).save(p)
            write_meta(p, tpl)
            scatter.append((rgba(rgb, alpha), flip, weight))
            manifest["scatter"].append({"file": f"Scatter/scatter_{name}_{v}.png",
                                        "weight": weight / max(1, int(round(weight / 4))),
                                        "flippable": bool(flip)})
            i += 1
    margins = {}
    for side in "lr":
        for i, suffix in enumerate("abc"):
            rng = np.random.default_rng(seed + 2000 + i * 53 + (0 if side == "l" else 7))
            rgb, alpha = MARGINS[st["margin"]](rng, st, side)
            p = os.path.join(d, "Margin", f"margin_{side}_{suffix}.png")
            rgba(rgb, alpha).save(p)
            write_meta(p, tpl)
            margins[f"{side}{suffix}"] = rgba(rgb, alpha)
    import json
    with open(os.path.join(d, "manifest.json"), "w") as f:
        json.dump(manifest, f, indent=2)
    return tiles, scatter, margins


# ------------------------------------------------------------------- preview ---

COL_W, COL_H = 360, 704   # the 11.25-unit play column; 22 tiles tall for the sheet
PER_TILE, MIN_DIST = 5.0, 6


def compose(st, tiles, scatter, margins, rng, with_scatter=True, with_margins=True):
    base = [tiles[f"ground_{s}"] for s in "abcdefgh"]
    worn = [tiles["worn_a"], tiles["worn_b"]]
    img = Image.new("RGBA", (COL_W, COL_H))
    cols, rows = COL_W // TILE + 1, COL_H // TILE + 1
    for r in range(rows):
        for c in range(cols):
            centre = abs(c - cols // 2) <= 1
            t = worn[int(rng.integers(0, 2))] if centre and rng.random() > 0.35 else base[int(rng.integers(0, 8))]
            img.paste(rgba(t), (c * TILE, r * TILE))
    lane = (MARGIN_W, COL_W - MARGIN_W) if with_margins else (0, COL_W)
    n = 0
    if with_scatter:
        weights = np.array([wt for _, _, wt in scatter], float)
        # A type's weight is shared across its variants, so variants do not multiply it.
        counts = {}
        for _, _, wt in scatter:
            counts[wt] = counts.get(wt, 0) + 1
        weights = np.array([wt / counts[wt] for _, _, wt in scatter], float)
        weights /= weights.sum()
        placed = []
        target = int(PER_TILE * ((lane[1] - lane[0]) / TILE) * rows)
        tries = 0
        while len(placed) < target and tries < target * 40:
            tries += 1
            x, y = int(rng.integers(lane[0], lane[1])), int(rng.integers(0, COL_H))
            if any((x - px) ** 2 + (y - py) ** 2 < MIN_DIST ** 2 for px, py in placed):
                continue
            el, flip, _ = scatter[int(rng.choice(len(scatter), p=weights))]
            if flip and rng.random() > 0.5:
                el = el.transpose(Image.FLIP_LEFT_RIGHT)
            img.alpha_composite(el, (x - el.width // 2, y - el.height // 2))
            placed.append((x, y))
        n = len(placed)
    if with_margins:
        for side, x in (("l", 0), ("r", COL_W - MARGIN_W)):
            y = -int(rng.integers(0, MARGIN_H))
            while y < COL_H:
                img.alpha_composite(margins[f"{side}{'abc'[int(rng.integers(0, 3))]}"], (x, y))
                y += MARGIN_H
    return img, n


def repeat_dip(img):
    """Self-similarity at lag 32 against its neighbours. 100% = a perfect repeat."""
    a = np.asarray(img.convert("L")).astype(float)
    d = {lag: np.abs(a[:, lag:] - a[:, :-lag]).mean() for lag in (30, 31, 32, 33, 34)}
    return 100 * (1 - d[32] / np.mean([d[30], d[31], d[33], d[34]]))


def coverage(a, b):
    x = np.asarray(a.convert("RGB")).astype(int)
    y = np.asarray(b.convert("RGB")).astype(int)
    return 100.0 * np.mean(np.any(x != y, axis=2))


def preview(here, results, out_png):
    def world(u):
        return int(round(u * 32))
    es = Image.open(os.path.join(here, "Assets/Art/Characters/Enemies/Corruption/sprite_enemy_mantsa_walk-Sheet.png")).convert("RGBA").crop((0, 0, 1024, 1024))
    es = es.crop(es.getbbox())
    eh = world(1.87)
    enemy = es.resize((int(es.width * eh / es.height), eh), Image.NEAREST)
    ps = Image.open(os.path.join(here, "Assets/Art/Characters/Protagonist/sprite_prot_japanese_idle_back-Sheet.png")).convert("RGBA").crop((0, 0, 32, 32))
    ps = ps.crop(ps.getbbox())
    ph = world(1.07)
    prot = ps.resize((int(ps.width * ph / ps.height), ph), Image.NEAREST)

    panels = []
    stats = []
    for stage, (st, tiles, scatter, margins) in results.items():
        wall = Image.open(os.path.join(here, "Assets/Art/Environment", st["wall"])).convert("RGBA")
        wall = wall.resize((world(8.75), world(1.39)), Image.NEAREST)
        rng = np.random.default_rng(31)
        plain, _ = compose(st, tiles, scatter, margins, np.random.default_rng(31), with_scatter=False, with_margins=False)
        full, n = compose(st, tiles, scatter, margins, np.random.default_rng(31))
        lane_only, _ = compose(st, tiles, scatter, margins, np.random.default_rng(31), with_margins=False)
        cov = coverage(plain, lane_only)
        dip = repeat_dip(lane_only.crop((MARGIN_W, 0, COL_W - MARGIN_W, COL_H)))
        stats.append((stage, n, cov, dip))
        shot = full.copy()
        for (ex, ey) in ((92, 150), (188, 300), (126, 440), (216, 90)):
            shot.alpha_composite(enemy, (ex, ey))
        shot.alpha_composite(wall, ((COL_W - wall.width) // 2, COL_H - world(4.2)))
        shot.alpha_composite(prot, ((COL_W - prot.width) // 2, COL_H - world(2.5)))
        z = 2
        big = shot.resize((shot.width * z, shot.height * z), Image.NEAREST)
        sec = lane_only.crop((MARGIN_W + 32, 96, MARGIN_W + 32 + 96, 192)).resize((96 * 5, 96 * 5), Image.NEAREST)
        panels.append((st["label"], big, sec, n, cov, dip))

    pad = 16
    pw = max(max(b.width, sc.width) for _, b, sc, _, _, _ in panels)
    ph_ = panels[0][1].height + panels[0][2].height + 60
    sheet = Image.new("RGB", (len(panels) * (pw + pad) + pad, ph_ + 30), (26, 26, 30))
    d = ImageDraw.Draw(sheet)
    x = pad
    for label, big, sec, n, cov, dip in panels:
        d.text((x, 8), label, fill=(240, 240, 225))
        sheet.paste(big.convert("RGB"), (x, 26))
        y = 26 + big.height + 8
        d.text((x, y), f"3x3 lane section, 5x   |  {n} scatter elements, {cov:.1f}% of lane pixels, lag-32 dip {dip:.1f}%", fill=(240, 240, 225))
        sheet.paste(sec.convert("RGB"), (x, y + 18))
        x += pw + pad
    sheet.save(out_png)
    return stats


def main():
    here = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(here, "Assets/Art/Environment/Tileset"))
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--preview", default=os.path.join(here, "docs/art/stage-backgrounds.png"))
    args = ap.parse_args()
    tpl = meta_template(here)
    results = {}
    for stage, st in STAGES.items():
        results[stage] = (st,) + generate(stage, st, args.out, args.seed, tpl)
        n = len(glob.glob(os.path.join(args.out, stage.capitalize(), "**", "*.png"), recursive=True))
        print(f"{stage:<8} {n} sprites -> {os.path.relpath(os.path.join(args.out, stage.capitalize()), here)}")
    stats = preview(here, results, args.preview)
    print()
    print(f"{'stage':<9}{'scatter':>8}{'lane px':>9}{'lag-32 dip':>12}")
    for stage, n, cov, dip in stats:
        print(f"{stage:<9}{n:>8}{cov:>8.1f}%{dip:>11.1f}%")
    print(f"\npreview -> {os.path.relpath(args.preview, here)}")


if __name__ == "__main__":
    main()
