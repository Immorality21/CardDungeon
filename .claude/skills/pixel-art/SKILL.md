---
name: pixel-art
description: Generate pixel art as a PNG image. Use when the user asks to draw, create, or generate pixel art, sprites, icons, or small bitmap images.
disable-model-invocation: true
argument-hint: "description of what to draw, e.g. 16x16 treasure chest"
---

# Pixel Art Generator

Generate pixel art based on the user's description and save it as a PNG file.

## Instructions

1. Parse the user's request from `$ARGUMENTS`. Determine:
   - **Subject**: What to draw (e.g. "treasure chest", "sword", "heart")
   - **Size**: Grid dimensions (default 16x16 if not specified). Common sizes: 8x8, 16x16, 32x32, 64x64.
   - **Output path**: Save to the current working directory as `<subject>.png` unless the user specifies a path.

2. Write a **Python script** that generates the pixel art using only the Python standard library (`struct` and `zlib` for raw PNG encoding — no Pillow/PIL dependency needed).

3. The script must:
   - Define the pixel art as a 2D grid of hex color values (e.g. `"#8B4513"` for brown, `None` for transparent).
   - Design the sprite with care — use shading, highlights, and outlines to make it look good at small sizes.
   - Encode it as a valid PNG with transparency support (RGBA).
   - Save it to disk.

4. Run the script with `python` to produce the PNG file.

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

## Example Pixel Grid Pattern

For a 16x16 treasure chest, you might structure it as:

```
Row 0:  _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _
Row 1:  _ _ _ _ O O O O O O O O _ _ _ _
Row 2:  _ _ _ O L L L L L L L L O _ _ _
...
```

Where O=outline, L=lid color, B=body color, H=highlight, G=gold trim, etc.

Think carefully about the shape, proportions, and details that make the subject recognizable at the given pixel size.
