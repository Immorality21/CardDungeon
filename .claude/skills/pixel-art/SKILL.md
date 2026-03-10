---
name: pixel-art
description: Generate pixel art as a PNG image. Use when the user asks to draw, create, or generate pixel art, sprites, icons, or small bitmap images.
disable-model-invocation: true
argument-hint: "description of what to draw, e.g. 32x32 treasure chest"
---

# Pixel Art Generator

Generate pixel art based on the user's description and save it as a PNG file with a Unity .meta file.

## Instructions

1. Parse the user's request from `$ARGUMENTS`. Determine:
   - **Subject**: What to draw (e.g. "treasure chest", "sword", "heart")
   - **Size**: Grid dimensions (default 32x32 if not specified). Common sizes: 8x8, 16x16, 32x32, 64x64.
   - **Output path**: Save to `Assets/Sprites/<subject>.png` unless the user specifies a path.

2. Write a **Python script** that generates the pixel art using only the Python standard library (`struct` and `zlib` for raw PNG encoding — no Pillow/PIL dependency needed).

3. The script must:
   - Define the pixel art as a 2D grid of hex color values (e.g. `"#8B4513"` for brown, `None` for transparent).
   - Design the sprite with care — use shading, highlights, and outlines to make it look good at small sizes.
   - Encode it as a valid PNG with transparency support (RGBA).
   - Save the PNG to disk.
   - Generate a Unity `.meta` file next to the PNG (see Unity Meta File Reference below).

4. Run the script with `python` to produce the PNG and `.meta` files.

5. After generating, read the PNG file using the Read tool so the user can see the result.

## Color Palette Guidelines

Use limited, cohesive palettes typical of pixel art:
- **Outlines**: Dark variant of the main color, or near-black (`#1a1a2e`)
- **Highlights**: Light variant or white-ish (`#f0e0c0`)
- **Shading**: At least 2-3 tones per material (light, mid, dark)
- **Transparency**: Use `None` for empty pixels (alpha = 0)

## PNG Encoding Reference (no dependencies)

```python
import struct, zlib

def save_png(pixels, width, height, filepath):
    """Save RGBA pixel data as PNG. pixels = list of rows, each row = list of (r,g,b,a) tuples."""
    def chunk(chunk_type, data):
        c = chunk_type + data
        return struct.pack('>I', len(data)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)

    raw = b''
    for row in pixels:
        raw += b'\x00'  # filter byte
        for r, g, b, a in row:
            raw += struct.pack('BBBB', r, g, b, a)

    return (
        b'\x89PNG\r\n\x1a\n' +
        chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) +
        chunk(b'IDAT', zlib.compress(raw)) +
        chunk(b'IEND', b'')
    )

with open(filepath, 'wb') as f:
    f.write(save_png(pixels, width, height, filepath))
```

## Unity Meta File Reference

After saving the PNG, generate a `.meta` file alongside it so Unity imports it as a pixel-art sprite. Use `uuid.uuid4().hex` for the GUID.

```python
import uuid

def save_meta(png_path, width, height):
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

save_meta(filepath, width, height)
```

Key settings for pixel art: `filterMode: 0` (Point/nearest-neighbor), `enableMipMap: 0`, `spriteMode: 1` (Single), `textureType: 8` (Sprite), `spritePixelsToUnits` set to the sprite width so one sprite = one world unit.

## Example Pixel Grid Pattern

For a 32x32 treasure chest, you might structure it as:

```
Row 0:  _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _
Row 1:  _ _ _ _ _ _ _ _ O O O O O O O O O O O O O O O O _ _ _ _ _ _ _ _
Row 2:  _ _ _ _ _ _ _ O L L L L L L L L L L L L L L L L O _ _ _ _ _ _ _
...
```

Where O=outline, L=lid color, B=body color, H=highlight, G=gold trim, etc.

Think carefully about the shape, proportions, and details that make the subject recognizable at the given pixel size.
