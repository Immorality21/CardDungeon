"""Generates the hub town backdrop.

320x180, which is exactly 1/4 of the hub's 1280x720 reference rect, so every authored lot
position maps to a whole pixel here. Procedural rather than hand-placed because a backdrop is
mostly gradients and noise; the parts that matter (where the horizon sits, where the ground is
clear) are pinned to the lot layout in Hub.asset:

    lots occupy y 30..157 and x 37..200 in THIS space (reference / 4)
    the road sits off to the right, past x 270

So the horizon is high, the ground is wide and open, and the right edge carries the path out.
"""
import math
import os
import random
import struct
import uuid
import zlib

W, H = 320, 180
SEED = 20260905

# --- palette ---------------------------------------------------------------
SKY_TOP = (7, 4, 18)
SKY_HORIZON = (66, 33, 84)
EMBER = (150, 74, 74)
MOUNTAIN_FAR = (30, 16, 50)
MOUNTAIN_NEAR = (16, 8, 28)
TREE_DARK = (9, 5, 18)
TREE_MID = (15, 9, 28)
GROUND_FAR = (44, 26, 58)
GROUND_NEAR = (22, 13, 34)
PATH = (72, 52, 74)
PATH_EDGE = (50, 33, 56)
ROCK = (32, 20, 44)
ROCK_LIT = (58, 41, 70)

# High horizon: the top row of lots has its base at y=57 in this space, so the ground has to
# start above that or the far buildings float. Their sprites still rise into the sky, which is
# what a town screen wants - a tall silhouette against the glow.
HORIZON = 52
GROUND_TOP = 52


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def save_png(pixels, width, height, path):
    def chunk(kind, data):
        c = kind + data
        return struct.pack('>I', len(data)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)

    raw = b''
    for row in pixels:
        raw += b'\x00'
        for r, g, b, a in row:
            raw += struct.pack('BBBB', r, g, b, a)

    blob = (b'\x89PNG\r\n\x1a\n'
            + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(raw, 9))
            + chunk(b'IEND', b''))
    with open(path, 'wb') as f:
        f.write(blob)


def save_meta(png_path, width):
    guid = uuid.uuid4().hex
    meta = f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: -1
    mipBias: -100
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: {width}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
  spritePackingTag:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    with open(png_path + ".meta", "w") as f:
        f.write(meta)


def main():
    rng = random.Random(SEED)
    px = [[(0, 0, 0, 255) for _ in range(W)] for _ in range(H)]

    def put(x, y, rgb, a=255):
        if 0 <= x < W and 0 <= y < H:
            px[y][x] = (rgb[0], rgb[1], rgb[2], a)

    # --- sky: vertical gradient, warmed toward the horizon ------------------
    for y in range(HORIZON + 8):
        t = y / float(HORIZON + 8)
        base = lerp(SKY_TOP, SKY_HORIZON, t ** 2.0)
        for x in range(W):
            # A low glow behind the mountains, brightest right-of-centre where the road leaves.
            glow = max(0.0, 1.0 - abs(x - 236) / 170.0) * max(0.0, (y - HORIZON + 24) / 30.0)
            put(x, y, lerp(base, EMBER, min(0.65, glow * 0.65)))

    # --- stars ---------------------------------------------------------------
    for _ in range(90):
        x = rng.randrange(W)
        y = rng.randrange(0, HORIZON - 8)
        # Fewer, dimmer stars low down where the sky brightens.
        if rng.random() > 1.0 - (y / float(HORIZON)) * 0.75:
            continue
        shade = rng.choice([(150, 140, 185), (120, 110, 160), (196, 184, 220)])
        put(x, y, shade)

    # --- mountains: two ridges of overlapping triangles ---------------------
    def ridge(base_y, height, colour, step, jitter):
        peaks = []
        x = -step
        while x < W + step:
            peaks.append((x, base_y - height - rng.randint(0, jitter)))
            x += step + rng.randint(-4, 4)
        for i in range(len(peaks) - 1):
            x0, y0 = peaks[i]
            x1, y1 = peaks[i + 1]
            mid = (x0 + x1) // 2
            for x in range(max(0, x0), min(W, x1 + 1)):
                if x <= mid:
                    t = (x - x0) / float(max(1, mid - x0))
                    top = int(base_y + (y0 - base_y) * t)
                else:
                    t = (x - mid) / float(max(1, x1 - mid))
                    top = int(y0 + (y1 - y0) * t)
                for y in range(max(0, top), base_y + 1):
                    put(x, y, colour)

    ridge(HORIZON + 1, 26, MOUNTAIN_FAR, 52, 11)
    ridge(HORIZON + 3, 16, MOUNTAIN_NEAR, 36, 8)

    # --- ground ---------------------------------------------------------------
    for y in range(GROUND_TOP, H):
        t = (y - GROUND_TOP) / float(H - GROUND_TOP)
        base = lerp(GROUND_FAR, GROUND_NEAR, t ** 0.8)
        for x in range(W):
            n = rng.random()
            shade = base
            if n > 0.94:
                shade = lerp(base, ROCK_LIT, 0.25)
            elif n < 0.06:
                shade = lerp(base, (0, 0, 0), 0.25)
            # A worn, lighter clearing where the camp actually is, so the lots sit on something
            # rather than on undifferentiated noise.
            d = math.hypot((x - 118) / 96.0, (y - 108) / 58.0)
            if d < 1.0:
                shade = lerp(shade, ROCK_LIT, (1.0 - d) * 0.30)
            put(x, y, shade)

    # --- the path out, hugging the right edge --------------------------------
    for y in range(GROUND_TOP + 2, H):
        t = (y - GROUND_TOP) / float(H - GROUND_TOP)
        cx = int(238 + 46 * t + math.sin(t * 3.1) * 5)
        half = int(5 + 20 * t)
        for x in range(cx - half, cx + half + 1):
            edge = abs(x - cx) > half - 2
            put(x, y, PATH_EDGE if edge else PATH)
        for _ in range(2):
            put(cx + rng.randint(-half, half), y, lerp(PATH, (0, 0, 0), 0.3))

    # --- treeline: conifer silhouettes along the top of the ground -----------
    def tree(x, base_y, h, colour):
        w = max(2, h // 3)
        for i in range(h):
            y = base_y - i
            spread = int(w * (1.0 - i / float(h)))
            for dx in range(-spread, spread + 1):
                put(x + dx, y, colour)
        put(x, base_y + 1, TREE_DARK)

    x = -4
    while x < W + 6:
        # Leave the middle of the clearing and the path clear of trees.
        in_clearing = 24 < x < 214
        on_path = x > 226
        if not (in_clearing or on_path):
            tree(x, GROUND_TOP + rng.randint(4, 9), rng.randint(11, 20), TREE_MID)
            tree(x + 3, GROUND_TOP + rng.randint(2, 6), rng.randint(8, 15), TREE_DARK)
        elif in_clearing and rng.random() < 0.30:
            tree(x, GROUND_TOP + rng.randint(1, 4), rng.randint(6, 10), TREE_DARK)
        x += rng.randint(5, 9)

    # --- scattered rocks, kept out of the lot footprints ----------------------
    lots = [(37, 30, 37, 27), (100, 30, 37, 27), (162, 30, 37, 27),
            (62, 80, 37, 27), (130, 80, 37, 27), (97, 120, 60, 37)]

    def clear_of_lots(x, y):
        for lx, ly, lw, lh in lots:
            if lx - 3 <= x <= lx + lw + 3 and ly - 3 <= y <= ly + lh + 3:
                return False
        return True

    for _ in range(70):
        x = rng.randrange(4, W - 4)
        y = rng.randrange(GROUND_TOP + 6, H - 3)
        if not clear_of_lots(x, y) or 226 < x < 300:
            continue
        r = rng.randint(1, 3)
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r:
                    put(x + dx, y + dy, ROCK if dy >= 0 else ROCK_LIT)

    # --- vignette, so the docked windows read against it ---------------------
    for y in range(H):
        for x in range(W):
            edge = min(x, W - 1 - x, y, H - 1 - y)
            if edge < 26:
                k = (1.0 - edge / 26.0) * 0.55
                r, g, b, a = px[y][x]
                px[y][x] = (int(r * (1 - k)), int(g * (1 - k)), int(b * (1 - k)), a)

    out = os.path.join("Assets", "Sprites", "Hub", "hub_backdrop.png")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    save_png(px, W, H, out)
    save_meta(out, W)
    print("wrote", out)


main()
