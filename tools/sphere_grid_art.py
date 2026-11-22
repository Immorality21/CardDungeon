"""Procedural pixel art for the sphere grid: socket, orb body (grey, tinted in USS), gloss,
halo, start sigil and a parallax star tile. Writes into the folder given as argv[1]
(Assets/UI/SphereGrid). sg_sky.png is a PixelLab image and is not made here."""
import math
import random
import sys
from PIL import Image

out = sys.argv[1]


def save(img, name, preview_scale=8):
    img.save(f"{out}/{name}.png")


def dist(x, y, cx, cy):
    return math.hypot(x + 0.5 - cx, y + 0.5 - cy)


# --- orb body: 22x22, a grey glass sphere lit from the top left -----------------------------
S = 22
C = S / 2
R_ORB = 8.6
tones = [70, 110, 150, 195, 235, 255]
orb = Image.new("RGBA", (S, S), (0, 0, 0, 0))
light = (-0.55, -0.65, 0.52)
ln = math.sqrt(sum(v * v for v in light))
light = tuple(v / ln for v in light)
for y in range(S):
    for x in range(S):
        d = dist(x, y, C, C)
        if d > R_ORB:
            continue
        if d > R_ORB - 1.0:
            orb.putpixel((x, y), (40, 34, 52, 255))  # outline, survives any tint as near-black
            continue
        nx = (x + 0.5 - C) / R_ORB
        ny = (y + 0.5 - C) / R_ORB
        nz = math.sqrt(max(0.0, 1 - nx * nx - ny * ny))
        lam = nx * light[0] + ny * light[1] + nz * light[2]
        # inner glow: glass orbs are lit from inside too, brighter in the core
        core = 1 - d / R_ORB
        v = 0.35 + 0.5 * max(0.0, lam) + 0.25 * core
        # bounce light on the lower right rim
        if nx > 0.25 and ny > 0.25 and d > R_ORB - 2.6:
            v += 0.18
        i = min(len(tones) - 1, max(0, int(v * (len(tones) - 1) + 0.15)))
        t = tones[i]
        orb.putpixel((x, y), (t, t, t, 255))
save(orb, "sg_orb")

# --- gloss: white specks over the orb, never tinted --------------------------------------------
gloss = Image.new("RGBA", (S, S), (0, 0, 0, 0))
for (x, y, a) in [(7, 6, 255), (8, 6, 255), (7, 7, 255), (6, 7, 200), (9, 6, 160), (7, 8, 120), (10, 5, 90),
                  (14, 15, 110), (15, 14, 90), (13, 16, 70)]:
    gloss.putpixel((x, y), (255, 255, 255, a))
save(gloss, "sg_orb_gloss")

# --- socket: a dark iron ring with four gold studs, the setting the orb sits in ---------------
R_OUT = 10.9
R_IN = 8.4
sock = Image.new("RGBA", (S, S), (0, 0, 0, 0))
for y in range(S):
    for x in range(S):
        d = dist(x, y, C, C)
        if d > R_OUT or d < R_IN:
            continue
        if d > R_OUT - 1.0 or d < R_IN + 0.6:
            col = (12, 7, 22, 255)
        else:
            ang = math.atan2(y + 0.5 - C, x + 0.5 - C)
            # lit from the top left
            lit = -math.cos(ang + math.pi * 0.75)
            if lit > 0.45:
                col = (150, 128, 170, 255)
            elif lit > -0.1:
                col = (98, 78, 124, 255)
            else:
                col = (58, 42, 82, 255)
        sock.putpixel((x, y), col)
# gold studs at the four points
for (sx, sy) in [(10, 0), (11, 0), (10, 21), (11, 21), (0, 10), (0, 11), (21, 10), (21, 11)]:
    sock.putpixel((sx, sy), (12, 7, 22, 255))
for (sx, sy) in [(10, 1), (11, 1), (10, 20), (11, 20), (1, 10), (1, 11), (20, 10), (20, 11)]:
    sock.putpixel((sx, sy), (236, 190, 72, 255))
for (sx, sy) in [(10, 1), (1, 10)]:
    sock.putpixel((sx, sy), (255, 236, 160, 255))
save(sock, "sg_socket")

# --- halo: a stepped glow, white so USS tints it to the node's hue ---------------------------
H = 44
halo = Image.new("RGBA", (H, H), (0, 0, 0, 0))
for y in range(H):
    for x in range(H):
        d = dist(x, y, H / 2, H / 2)
        if d > 21:
            continue
        # four hard steps, a dither band between the outer two
        if d < 12:
            a = 150
        elif d < 15:
            a = 95
        elif d < 18:
            a = 50
        else:
            a = 22 if (x + y) % 2 == 0 else 0
        if a:
            halo.putpixel((x, y), (255, 255, 255, a))
save(halo, "sg_halo")

# --- start sigil: a faint arcane circle under the grid's first node ---------------------------
G = 64
sig = Image.new("RGBA", (G, G), (0, 0, 0, 0))
gc = G / 2
ink = (205, 150, 255)


def put(x, y, a):
    if 0 <= x < G and 0 <= y < G:
        old = sig.getpixel((x, y))[3]
        sig.putpixel((x, y), ink + (max(old, a),))


for y in range(G):
    for x in range(G):
        d = dist(x, y, gc, gc)
        if abs(d - 30.5) < 0.55:
            put(x, y, 170)
        if abs(d - 27.5) < 0.5:
            put(x, y, 110)
        if abs(d - 17.5) < 0.5:
            put(x, y, 120)
# runes between the outer rings: short ticks every 15 degrees, every third a little diamond
for k in range(24):
    ang = k * math.pi / 12
    for r in (28.5, 29.5):
        put(int(gc + math.cos(ang) * r), int(gc + math.sin(ang) * r), 150)
    if k % 3 == 0:
        mx, my = gc + math.cos(ang) * 29, gc + math.sin(ang) * 29
        for (dx, dy) in [(0, -1), (1, 0), (0, 1), (-1, 0)]:
            put(int(mx + dx), int(my + dy), 200)
# a six-pointed star joining the inner ring to the middle one
pts = [(gc + math.cos(i * math.pi / 3 - math.pi / 2) * 27, gc + math.sin(i * math.pi / 3 - math.pi / 2) * 27) for i in range(6)]
for tri in ([0, 2, 4], [1, 3, 5]):
    for i in range(3):
        a = pts[tri[i]]
        b = pts[tri[(i + 1) % 3]]
        steps = int(max(abs(b[0] - a[0]), abs(b[1] - a[1]))) + 1
        for s in range(steps + 1):
            t = s / steps
            put(int(a[0] + (b[0] - a[0]) * t), int(a[1] + (b[1] - a[1]) * t), 90)
save(sig, "sg_sigil")

# --- star tile: sparse stars on transparent, seamless by construction --------------------------
T = 128
rng = random.Random(7300)
stars = Image.new("RGBA", (T, T), (0, 0, 0, 0))
palette = [(245, 240, 255), (227, 168, 255), (180, 190, 255), (205, 110, 255)]
for _ in range(70):
    x, y = rng.randrange(T), rng.randrange(T)
    col = rng.choice(palette)
    stars.putpixel((x, y), col + (rng.choice([60, 90, 130, 170]),))
for _ in range(7):
    x, y = rng.randrange(2, T - 2), rng.randrange(2, T - 2)
    col = rng.choice(palette[:3])
    stars.putpixel((x, y), col + (230,))
    for (dx, dy) in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
        stars.putpixel((x + dx, y + dy), col + (110,))
save(stars, "sg_stars")

print("ok")
