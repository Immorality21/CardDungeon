---
name: pixel-art
description: Generate pixel art as a PNG image. Use when the user asks to draw, create, or generate pixel art, sprites, icons, or small bitmap images.
disable-model-invocation: false
argument-hint: "description of what to draw, e.g. 32x32 treasure chest"
---

# Pixel Art Generator

Generate pixel art from `$ARGUMENTS` and save it as a PNG (plus a Unity `.meta` when the project is
a Unity project).

There are two paths:

1. **PixelLab MCP (primary).** An image model built for pixel art. Use it whenever it is connected
   and has generations left.
2. **Hand-authored Python grid (legacy fallback).** Only when PixelLab is unavailable or out of
   generations, and only after warning the user (see Step 2).

## Step 0 — Read the project's notes

**Read `docs/PIXEL_ART.md` first.** It holds Card Dungeon's sizes, PPU, animation conventions,
import settings and verification recipe, and it overrides the defaults below. The rules that bite:

- **Heroes and normal enemies are 32×32 @ 32 PPU; bosses may be 64×64 @ 38 PPU** with
  `EnemySO.CombatScale` 1.8. Match the sprite being replaced.
- **Author enemies facing right (or dead front)** — the battle stage mirrors them to face the party.
- Idle animations are **3-frame horizontal strips** in `Assets/Sprites/Animation/`, wired through
  `AnimationFrames` + `AnimationFps: 4`. There is no procedural idle to fall back on.
- **Verify in the sandbox** (`docs/SANDBOX.md`, `SandboxLauncher.Launch`), not by spawning into a
  random room — that writes the player's bestiary.

## Step 1 — Work out what to make

From `$ARGUMENTS` determine:

- **Subject**: what to draw.
- **Size**: if the sprite **replaces an existing one**, open the existing PNG and its siblings and
  use *their* size. Never assume a size from other, newer art (heroes and enemies can differ).
  Otherwise use the size given, or 32x32.
- **Output path**: replacing → overwrite the existing PNG in place (keeps its `.meta`, GUID and every
  reference). New → `Assets/Sprites/<subject>.png` unless told otherwise.
- **Animated?**: if the unit's siblings have idle strips, this one needs one too.

## Step 2 — Choose the path

Load the PixelLab tools with ToolSearch (`+pixellab`) and call `mcp__pixellab__get_balance` (free).

- **Connected and `generations_remaining` covers the job** → Step 3 (PixelLab).
- **Not connected, or not enough generations** → stop and tell the user plainly before doing
  anything else:

  > ⚠️ **PixelLab is unavailable** (*not connected* / *0 generations left*). I can fall back to the
  > legacy method — hand-placing pixels in a Python script — but the result is much weaker than
  > PixelLab: fine for simple icons and placeholders, poor for characters, enemies or anything
  > animated. Options: wait for PixelLab's daily free generations, top up the account, or accept the
  > legacy fallback.

  Only continue to Step 4 (legacy) if the user accepts, or if the request is a simple icon where the
  fallback is clearly adequate — and say that you are using it.

Budget rule: tell the user the cost before spending more than ~3 generations, and never call a
20–40 generation tool (`create_image_pro`, `create_character` pro mode, `animate_object` pro mode)
without their explicit OK.

## Step 3 — PixelLab path

### Generate

| need | tool | cost |
|---|---|---|
| small sprite ≤32 px (heroes, icons) | `create_image_pixen` (side a multiple of 4; square below 32) | 1 |
| larger sprite, forced palette, or img2img from an existing sprite | `create_image_pixflux` | 1 |
| idle / motion loop from a loose sprite | `animate_image` | ~1 |
| hand edits, lint, palette work | `pixelart_workbench` | free |

- Always `no_background: true`, `outline: "single color black outline"`, a fixed `seed`, and a
  `direction` that matches how the unit faces in game.
- Match the existing art's style in the prompt (e.g. "cute chibi, big head, thick dark outline,
  simple readable shapes" for a chibi party). Without it the model drifts to realistic proportions.
- Generating 2 candidates (different seeds) in parallel is usually worth it for anything
  character-like.
- Jobs are async: queue, then `wait_for_jobs` (free), then `get_image`. Download with the returned
  `download_url` via `curl` into the scratchpad — do not transcribe base64.
- `init_image_strength` on pixflux is how much of the input is **kept** (500 ≈ unchanged, 150 = a
  real edit, ~50 = composition only).

### Look at the result

View the PNG (and an upscaled nearest-neighbour preview beside an existing sibling). Check the
proportions, outline and palette match the neighbours. If it clashes, say so and regenerate rather
than shipping it.

### Animate (when needed)

1. `animate_image(first_frame_url=<download_url>, action="subtle idle breathing loop, … stays in
   place, returns to starting pose", frame_count=4)` → returns 5 images (index 0 = the input).
2. Download all frames and view them. Pick the frames the project uses (3 for Card Dungeon) whose
   cyclic pixel differences are all small, so the loop has no pop.
3. Reject frames that change the character (e.g. the face turning to profile). If too few survive,
   build the loop by hand: rest → upper body dropped 1 px with feet planted → rest variant.
4. Assemble a horizontal strip with Pillow.

### Import (Unity)

- Replacing: overwrite the PNG; keep the `.meta`.
- New: write the `.meta` (single sprite: the template in Step 4 with a new GUID; strip:
  `spriteMode: 2` with one full-frame slice per frame — copy an existing strip's meta).
- Point filtering, no compression, PPU per the project's notes.
- Refresh through the Unity MCP and confirm the asset's sprite and frames resolve.

### Verify

Import checks are not enough. If the Unity MCP is available, run the sprite in the real game
(the project's `docs/GAMEPLAY_VALIDATION.md` / `docs/PIXEL_ART.md` have the recipe): capture it next
to its neighbours to check size, and record the frame changes over a few seconds to prove the
animation cycles. Report what was and was not verified.

## Step 4 — Legacy fallback (hand-authored grid)

⚠️ Use only per Step 2, and tell the user this is the fallback.

1. Write a **Python script** using only the standard library (`struct` + `zlib` for raw PNG
   encoding — no Pillow needed).
2. Define the art as a 2D grid of hex colours (`None` = transparent). Design with care: outline,
   2–3 tones per material, highlights. Use single-character colour variables (e.g. `O` outline, `B`
   body, `H` highlight, `_` transparent) to lay the grid out row by row.
3. Encode RGBA PNG and save it.
4. Generate the Unity `.meta` alongside (below).
5. Tell the user the file path. Do not read the PNG back.

### Palette guidelines

- **Outlines**: dark variant of the main colour, or near-black (`#1a1a2e`)
- **Highlights**: light variant or white-ish (`#f0e0c0`)
- **Shading**: at least 2–3 tones per material
- **Transparency**: `None` for empty pixels (alpha 0)

### PNG encoding (no dependencies)

```python
import struct, zlib

def save_png(pixels, width, height, filepath):
    """pixels = list of rows, each row = list of (r,g,b,a) tuples."""
    def chunk(chunk_type, data):
        c = chunk_type + data
        return struct.pack('>I', len(data)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)

    raw = b''
    for row in pixels:
        raw += b'\x00'  # filter byte
        for r, g, b, a in row:
            raw += struct.pack('BBBB', r, g, b, a)

    data = (
        b'\x89PNG\r\n\x1a\n' +
        chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) +
        chunk(b'IDAT', zlib.compress(raw)) +
        chunk(b'IEND', b'')
    )
    with open(filepath, 'wb') as f:
        f.write(data)
```

### Unity `.meta` template (single sprite)

Replace `GENERATED_GUID` and `GENERATED_SPRITE_ID` with two different random 32-char hex strings, and
`PIXELS_PER_UNIT` with the project's PPU for this kind of sprite (default: the sprite's pixel width).

```yaml
fileFormatVersion: 2
guid: GENERATED_GUID
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
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
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
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
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: PIXELS_PER_UNIT
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 3
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
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
    spriteID: GENERATED_SPRITE_ID
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
```
