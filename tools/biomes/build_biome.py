"""Builds one biome's sprites from generated tiles and props plus the Crypt's doors.

usage: build_biome.py <config.json>

A config is tools/biomes/<biome>.json: which generated tiles and props to use, the colour grade, and
the two door materials. "source" is a folder of tiles/t<N>.png, floor/c<N>.png and wall/c<N>.png, as
downloaded from PixelLab; those downloads are not kept, so a config records the picks, and rebuilding
a shipped biome means generating or downloading again. See docs/PIXEL_ART.md.
"""
import glob
import json
import os
import sys
from collections import Counter

from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Sprites', 'Environment', 'Biomes')
CRYPT = os.path.join(ROOT, 'Crypt')

# The Crypt's common floor and wall colours: every biome sits at the same brightness, so a unit
# reads the same against any floor.
FLOOR_LUM = 58.0
WALL_LUM = 75.0
EDGE_GAIN = 1.45
FLOOR_PROP_GAIN = 0.78


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def hex_rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def is_glow(c, keep):
    """Lava and embers: saturated warm pixels that must not be dimmed with the stone."""
    if not keep:
        return False
    r, g, b = c[:3]
    return r > 120 and r > g * 1.5 and r > b * 1.8


def dominant(im):
    counts = Counter(p[:3] for p in im.get_flattened_data() if p[3] > 0)
    # The most common colours that are not the frame or mortar.
    light = [(n, c) for c, n in counts.items() if lum(c) > 25]
    light.sort(reverse=True)
    top = light[:4] or [(1, (60, 60, 60))]
    total = sum(n for n, _ in top)
    return tuple(sum(c[i] * n for n, c in top) / total for i in range(3))


def gain(im, factor, keep_glow):
    out = im.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            p = px[x, y]
            if p[3] == 0 or is_glow(p, keep_glow):
                continue
            px[x, y] = (min(255, int(p[0] * factor)), min(255, int(p[1] * factor)), min(255, int(p[2] * factor)), p[3])
    return out


def clean_floor(im):
    """The generator's 1 px black frame would draw a 2 px grid: keep one joint, left and bottom."""
    out = im.copy()
    px = out.load()
    n = out.width
    mortar = min((p[:3] for p in im.crop((1, 1, n - 1, n - 1)).get_flattened_data()), key=lum)
    for i in range(n):
        px[n - 1, i] = px[n - 2, i]
        px[i, 0] = px[i, 1]
    for i in range(n):
        px[0, i] = mortar + (255,)
        px[i, n - 1] = mortar + (255,)
    return out


def clean_wall(im):
    """Bricks run on sideways; the dark base line stays, where the wall meets the floor."""
    out = im.copy()
    px = out.load()
    n = out.width
    for y in range(n):
        px[0, y] = px[1, y]
        px[n - 1, y] = px[n - 2, y]
    for x in range(n):
        px[x, 0] = px[x, 1]
    return out


def grade(im, rgb, keep_glow):
    out = im.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            p = px[x, y]
            if p[3] == 0 or is_glow(p, keep_glow):
                continue
            px[x, y] = tuple(min(255, int(p[i] * rgb[i])) for i in range(3)) + (p[3],)
    return out


def to_level(im, target, keep_glow):
    return gain(im, target / max(1.0, lum(dominant(im))), keep_glow)


def strip_backing(im, palette, tol=5):
    """Removes the stone a prop was drawn on: flood in from the transparent outside through exactly
    that stone's colours, then give what is left a 1 px dark outline."""
    out = im.copy()
    px = out.load()
    w, h = out.size
    colours = [hex_rgb(c) for c in palette]

    def backing(p):
        return p[3] > 0 and any(all(abs(p[i] - c[i]) <= tol for i in range(3)) for c in colours)

    def neighbours(x, y):
        return [(a, b) for a, b in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)) if 0 <= a < w and 0 <= b < h]

    stack = []
    for y in range(h):
        for x in range(w):
            if backing(px[x, y]) and (x in (0, w - 1) or y in (0, h - 1) or any(px[a, b][3] == 0 for a, b in neighbours(x, y))):
                stack.append((x, y))
    removed = []
    while stack:
        x, y = stack.pop()
        if not backing(px[x, y]):
            continue
        px[x, y] = (0, 0, 0, 0)
        removed.append((x, y))
        stack.extend(neighbours(x, y))

    # Fragments of stone the fill could not reach: anything left with no colour in it.
    seen = set()
    for y in range(h):
        for x in range(w):
            if px[x, y][3] == 0 or (x, y) in seen:
                continue
            part, todo = [], [(x, y)]
            seen.add((x, y))
            while todo:
                c = todo.pop()
                part.append(c)
                for n in neighbours(*c):
                    if px[n[0], n[1]][3] > 0 and n not in seen:
                        seen.add(n)
                        todo.append(n)
            if all(max(px[a, b][:3]) - min(px[a, b][:3]) < 45 for a, b in part):
                for a, b in part:
                    px[a, b] = (0, 0, 0, 0)

    edge = [(x, y) for x, y in removed if any(px[a, b][3] > 0 for a, b in neighbours(x, y))]
    for x, y in edge:
        px[x, y] = (16, 12, 14, 255)
    return out


def recolour(im, stone, wood):
    """The Crypt's cool stone and warm wood, moved onto this biome's two materials by brightness."""
    out = im.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            p = px[x, y]
            if p[3] == 0:
                continue
            tint = stone if p[2] >= p[0] else wood
            k = lum(p) / lum(tint)
            px[x, y] = tuple(min(255, int(v * k)) for v in tint) + (p[3],)
    return out


def main():
    cfg = json.load(open(sys.argv[1]))
    src, key = cfg['source'], cfg['key']
    dest = os.path.join(ROOT, cfg['folder']).replace('\\', '/')
    os.makedirs(dest, exist_ok=True)
    keep = cfg.get('keep_glow', False)
    tone = cfg.get('grade', [1, 1, 1])
    floor_lum = cfg.get('floor_lum', FLOOR_LUM)
    wall_lum = cfg.get('wall_lum', WALL_LUM)

    def save(im, name):
        im.save(f'{dest}/{key}_{name}.png')

    for i, n in enumerate(cfg['floors']):
        im = Image.open(f'{src}/tiles/t{n}.png').convert('RGBA')
        save(grade(to_level(clean_floor(im), floor_lum, keep), tone, keep), f'floor_{i}')

    wall0 = None
    for i, n in enumerate(cfg['walls']):
        im = Image.open(f'{src}/tiles/t{n}.png').convert('RGBA')
        wall = grade(to_level(clean_wall(im), wall_lum, keep), tone, keep)
        save(wall, f'wall_{i}')
        wall0 = wall0 or wall
    if 'edge_tile' in cfg:
        im = Image.open(f"{src}/tiles/t{cfg['edge_tile']}.png").convert('RGBA')
        wall0 = grade(to_level(clean_wall(im), wall_lum, keep), tone, keep)
    save(gain(wall0, cfg.get('edge_gain', EDGE_GAIN), False), 'edge')

    stone = hex_rgb(cfg['stone'])
    wood = hex_rgb(cfg['wood'])
    for path in glob.glob(f'{CRYPT}/crypt_door_*.png') + [f'{CRYPT}/crypt_archway.png']:
        name = os.path.basename(path).replace('crypt_', '').replace('.png', '')
        if 'shade' in name:
            continue
        save(recolour(Image.open(path).convert('RGBA'), stone, wood), name)

    for name, n in cfg['floor_props'].items():
        im = Image.open(f'{src}/floor/c{n}.png').convert('RGBA')
        if cfg.get('strip_floor_props'):
            im = strip_backing(im, cfg['strip_floor_props'])
        save(gain(im, cfg.get('floor_prop_gain', FLOOR_PROP_GAIN), keep), f'prop_{name}')

    for name, n in cfg['wall_props'].items():
        im = Image.open(f'{src}/wall/c{n}.png').convert('RGBA')
        if cfg.get('strip_wall_props'):
            im = strip_backing(im, cfg['strip_wall_props'])
        im = gain(im, cfg.get('wall_prop_gain', 1.0), keep)
        if cfg.get('grade_wall_props'):
            im = grade(im, tone, keep)
        save(im, f'wall_{name}')

    print(len(glob.glob(f'{dest}/*.png')), 'sprites in', dest)


main()
