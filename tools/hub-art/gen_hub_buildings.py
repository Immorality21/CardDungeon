"""Generates the hub's building sprites: one per lot, plus a shared 'available' foundation.

64x64 each, drawn onto transparency, with the silhouette sitting on the bottom edge so a lot's
DrawRect can hang it above its hit box and it still meets the ground.

Placeholder art, deliberately: readable silhouettes and three tones per material, so the town
plays and reads before anything is painted properly. The palette is the theme's
(CardDungeon.uss), so the lots sit in the same world as the panels docked over them.
"""
import os
import struct
import uuid
import zlib

S = 64
OUT_DIR = os.path.join("Assets", "Sprites", "Hub")

# --- palette (theme-matched) ------------------------------------------------
LINE = (12, 6, 24)
STONE_D = (34, 20, 52)
STONE_M = (58, 38, 82)
STONE_L = (86, 60, 116)
WOOD_D = (46, 27, 30)
WOOD_M = (78, 47, 46)
WOOD_L = (112, 72, 62)
ROOF_D = (44, 20, 70)
ROOF_M = (72, 34, 112)
ROOF_L = (108, 56, 156)
GOLD = (236, 190, 72)
GOLD_D = (150, 112, 34)
FIRE_C = (255, 226, 150)
FIRE_M = (247, 150, 44)
FIRE_D = (192, 68, 30)
ARCANE = (205, 110, 255)
ARCANE_D = (120, 52, 168)
GREEN = (86, 168, 96)
GREEN_D = (44, 96, 56)


def blank():
    return [[None for _ in range(S)] for _ in range(S)]


def put(g, x, y, c):
    if 0 <= x < S and 0 <= y < S and c is not None:
        g[y][x] = c


def rect(g, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(g, x, y, c)


def outline_rect(g, x0, y0, x1, y1, c):
    for x in range(x0, x1 + 1):
        put(g, x, y0, c)
        put(g, x, y1, c)
    for y in range(y0, y1 + 1):
        put(g, x0, y, c)
        put(g, x1, y, c)


def gable(g, cx, y0, half, height, c, edge=None):
    """A triangular roof, apex at (cx, y0), base `height` rows down."""
    for i in range(height + 1):
        w = int(half * i / float(height))
        y = y0 + i
        for x in range(cx - w, cx + w + 1):
            put(g, x, y, c)
        if edge is not None:
            put(g, cx - w, y, edge)
            put(g, cx + w, y, edge)


def disc(g, cx, cy, r, c):
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                put(g, x, y, c)


def shade_left(g, x0, y0, x1, y1, light, mid, dark):
    """Three vertical bands: lit on the left, mid, shadowed on the right."""
    span = max(1, x1 - x0)
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            t = (x - x0) / float(span)
            put(g, x, y, light if t < 0.28 else (mid if t < 0.72 else dark))


# --- the buildings ----------------------------------------------------------

def foundation():
    """The 'available' state, shared by every lot: a footing and stakes, waiting.

    Deliberately the brightest of the unbuilt states. It is the affordance that makes a material
    worth wanting, so it has to read as an invitation across a dark backdrop - not as rubble.
    """
    g = blank()
    # Cut-stone footing.
    rect(g, 10, 48, 53, 58, STONE_M)
    shade_left(g, 11, 49, 52, 57, STONE_L, STONE_M, STONE_D)
    outline_rect(g, 10, 48, 53, 58, LINE)
    for x in range(12, 53, 8):
        for y in range(49, 58):
            put(g, x, y, STONE_D)
    for x in range(11, 53):
        put(g, x, 53, STONE_D)
    # Marker stakes and a rope: someone means to build here.
    for x in (13, 25, 38, 50):
        rect(g, x, 34, x + 2, 49, WOOD_M)
        rect(g, x, 34, x, 49, WOOD_L)
        put(g, x + 1, 33, GOLD_D)
    for x in range(13, 51):
        if x % 4 != 3:
            put(g, x, 37, GOLD_D)
    return g


def campfire():
    g = blank()
    # Ring of seating stones.
    for cx, cy, r in ((13, 52, 5), (51, 52, 5), (20, 58, 4), (44, 58, 4)):
        disc(g, cx, cy, r, STONE_D)
        disc(g, cx - 1, cy - 1, r - 2, STONE_M)
    # Fire pit.
    disc(g, 32, 55, 11, STONE_D)
    disc(g, 32, 55, 8, (22, 12, 32))
    # Logs.
    for x0, y0, x1, y1 in ((23, 51, 41, 54), (26, 47, 38, 50)):
        rect(g, x0, y0, x1, y1, WOOD_D)
        rect(g, x0 + 1, y0, x1 - 1, y0 + 1, WOOD_M)
    # Flame.
    for i, (w, c) in enumerate(((9, FIRE_D), (6, FIRE_M), (3, FIRE_C))):
        top = 22 + i * 5
        for y in range(top, 50):
            t = (y - top) / float(50 - top)
            half = int(w * t)
            for x in range(32 - half, 32 + half + 1):
                put(g, x, y, c)
    disc(g, 32, 20, 1, FIRE_C)
    return g


def merchant():
    g = blank()
    rect(g, 10, 34, 54, 58, WOOD_D)
    shade_left(g, 11, 35, 53, 57, WOOD_L, WOOD_M, WOOD_D)
    outline_rect(g, 10, 34, 54, 58, LINE)
    # Striped awning.
    for i in range(0, 46):
        x = 9 + i
        c = GOLD if (i // 4) % 2 == 0 else ROOF_M
        for y in range(26, 34):
            if y - 26 < 8:
                put(g, x, y, c)
    for x in range(9, 55):
        put(g, x, 34, LINE)
    # Counter and goods.
    rect(g, 14, 44, 50, 47, STONE_M)
    outline_rect(g, 14, 44, 50, 47, LINE)
    disc(g, 21, 41, 3, GOLD_D)
    disc(g, 30, 41, 3, ARCANE_D)
    disc(g, 39, 41, 3, GREEN_D)
    # Posts.
    rect(g, 10, 26, 12, 58, WOOD_D)
    rect(g, 52, 26, 54, 58, WOOD_D)
    return g


def forge():
    g = blank()
    rect(g, 12, 34, 52, 58, STONE_D)
    shade_left(g, 13, 35, 51, 57, STONE_L, STONE_M, STONE_D)
    outline_rect(g, 12, 34, 52, 58, LINE)
    gable(g, 32, 20, 24, 14, ROOF_M, LINE)
    for x in range(10, 55):
        put(g, x, 34, LINE)
    # Chimney with embers.
    rect(g, 42, 12, 49, 26, STONE_D)
    outline_rect(g, 42, 12, 49, 26, LINE)
    rect(g, 43, 13, 48, 15, STONE_M)
    for x, y in ((45, 8), (47, 5), (44, 4), (46, 10)):
        put(g, x, y, FIRE_M)
    # The forge mouth, glowing.
    rect(g, 24, 42, 40, 58, LINE)
    rect(g, 26, 44, 38, 58, FIRE_D)
    rect(g, 28, 47, 36, 58, FIRE_M)
    rect(g, 30, 51, 34, 58, FIRE_C)
    # Anvil.
    rect(g, 15, 50, 22, 52, STONE_L)
    rect(g, 17, 52, 20, 57, STONE_M)
    return g


def sphere_hall():
    g = blank()
    # Tower.
    rect(g, 20, 22, 44, 58, STONE_D)
    shade_left(g, 21, 23, 43, 57, STONE_L, STONE_M, STONE_D)
    outline_rect(g, 20, 22, 44, 58, LINE)
    # Conical cap.
    gable(g, 32, 6, 16, 16, ROOF_M, LINE)
    for x in range(16, 49):
        put(g, x, 22, LINE)
    # A hovering orb: this is where a hero decides what they are becoming.
    disc(g, 32, 36, 7, ARCANE_D)
    disc(g, 32, 36, 5, ARCANE)
    disc(g, 30, 34, 2, (240, 210, 255))
    for x, y in ((22, 30), (42, 30), (24, 44), (40, 44)):
        put(g, x, y, ARCANE)
    # Door.
    rect(g, 29, 48, 35, 58, LINE)
    rect(g, 30, 50, 34, 58, WOOD_M)
    return g


def bestiary():
    g = blank()
    # A low hall with a shingled roof.
    rect(g, 10, 36, 54, 58, WOOD_D)
    shade_left(g, 11, 37, 53, 57, WOOD_L, WOOD_M, WOOD_D)
    outline_rect(g, 10, 36, 54, 58, LINE)
    gable(g, 32, 22, 26, 14, GREEN_D, LINE)
    for x in range(6, 59):
        put(g, x, 36, LINE)
    # Skull over the door - what the party has survived, written down.
    disc(g, 32, 44, 5, (222, 214, 198))
    put(g, 30, 44, LINE)
    put(g, 34, 44, LINE)
    rect(g, 30, 47, 34, 49, (222, 214, 198))
    put(g, 32, 48, LINE)
    # Trophy poles.
    rect(g, 13, 28, 15, 40, WOOD_M)
    rect(g, 49, 28, 51, 40, WOOD_M)
    disc(g, 14, 26, 2, GREEN)
    disc(g, 50, 26, 2, GREEN)
    # Door.
    rect(g, 28, 51, 36, 58, LINE)
    rect(g, 29, 52, 35, 58, WOOD_D)
    return g


def storehouse():
    g = blank()
    rect(g, 8, 32, 56, 58, WOOD_D)
    shade_left(g, 9, 33, 55, 57, WOOD_L, WOOD_M, WOOD_D)
    outline_rect(g, 8, 32, 56, 58, LINE)
    # Wide barn roof.
    gable(g, 32, 16, 28, 16, WOOD_D, LINE)
    for i in range(17, 32):
        w = int(28 * (i - 16) / 16.0)
        for x in range(32 - w, 32 + w + 1):
            if (x + i) % 5 == 0:
                put(g, x, i, WOOD_M)
    for x in range(4, 61):
        put(g, x, 32, LINE)
    # Big double doors.
    rect(g, 22, 40, 42, 58, LINE)
    rect(g, 23, 41, 31, 58, WOOD_M)
    rect(g, 33, 41, 41, 58, WOOD_M)
    put(g, 30, 49, GOLD)
    put(g, 34, 49, GOLD)
    # Crates stacked outside.
    for x0, y0 in ((11, 48), (11, 41), (46, 48)):
        rect(g, x0, y0, x0 + 7, y0 + 6, WOOD_L)
        outline_rect(g, x0, y0, x0 + 7, y0 + 6, LINE)
        for x in range(x0 + 1, x0 + 7):
            put(g, x, y0 + 3, WOOD_D)
    return g


# --- output -----------------------------------------------------------------

def save_png(grid, path):
    def chunk(kind, data):
        c = kind + data
        return struct.pack('>I', len(data)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)

    raw = b''
    for row in grid:
        raw += b'\x00'
        for c in row:
            raw += struct.pack('BBBB', *(c + (255,))) if c else b'\x00\x00\x00\x00'

    blob = (b'\x89PNG\r\n\x1a\n'
            + chunk(b'IHDR', struct.pack('>IIBBBBB', S, S, 8, 6, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(raw, 9))
            + chunk(b'IEND', b''))
    with open(path, 'wb') as f:
        f.write(blob)


META = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "meta_template.txt")).read()


def save_meta(path):
    """Write a .meta only if there is not one already.

    A .meta carries the asset GUID, and every reference to the sprite - the BuildingSO fields
    included - is by GUID. Regenerating it on a re-run silently orphans all of them, and the
    symptom is a town that renders its flat placeholders again with nothing in the console.
    """
    meta_path = path + ".meta"
    if os.path.exists(meta_path):
        return
    with open(meta_path, "w") as f:
        f.write(META.replace("{GUID}", uuid.uuid4().hex).replace("{PPU}", str(S)))


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for name, fn in (("lot_foundation", foundation),
                     ("bld_campfire", campfire),
                     ("bld_merchant", merchant),
                     ("bld_forge", forge),
                     ("bld_sphere_hall", sphere_hall),
                     ("bld_bestiary", bestiary),
                     ("bld_storehouse", storehouse)):
        path = os.path.join(OUT_DIR, name + ".png")
        save_png(fn(), path)
        save_meta(path)
        print("wrote", path)


main()
