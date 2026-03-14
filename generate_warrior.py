import struct, zlib, random

def save_png(pixels, width, height, filepath):
    def chunk(chunk_type, data):
        c = chunk_type + data
        return struct.pack('>I', len(data)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
    raw = b''
    for row in pixels:
        raw += b'\x00'
        for r, g, b, a in row:
            raw += struct.pack('BBBB', r, g, b, a)
    return (
        b'\x89PNG\r\n\x1a\n' +
        chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) +
        chunk(b'IDAT', zlib.compress(raw)) +
        chunk(b'IEND', b'')
    )

def hex_to_rgba(h):
    if h is None:
        return (0, 0, 0, 0)
    h = h.lstrip('#')
    return (int(h[0:2],16), int(h[2:4],16), int(h[4:6],16), 255)

# Color palette
_ = None           # transparent
O = "#1a1a2e"      # outline / dark
K = "#2d2d44"      # dark armor shadow
A = "#4a4a6a"      # armor mid
L = "#6a6a8e"      # armor light
H = "#8888aa"      # armor highlight
S = "#c0c0d0"      # steel / sword blade
W = "#e8e8f0"      # sword highlight
P = "#e8c090"      # skin
D = "#c8a070"      # skin shadow
R = "#a08050"      # skin dark
E = "#2040a0"      # eye color
B = "#8b2020"      # cape/plume dark
C = "#c03030"      # cape/plume mid
F = "#d84848"      # cape/plume light
G = "#d4a020"      # gold trim
Y = "#e8c040"      # gold highlight
T = "#b08818"      # gold dark
V = "#3a2a1a"      # hair dark
J = "#5a4030"      # hair mid
M = "#705038"      # hair light
Q = "#383850"      # shield dark
U = "#505070"      # shield mid
I = "#606888"      # shield light

grid = [
#   0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30 31
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  O,  O,  O,  O,  O,  O,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 0  helmet top
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  A,  A,  L,  L,  L,  L,  A,  A,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 1
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  A,  L,  L,  H,  H,  H,  H,  L,  L,  A,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 2
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  A,  L,  H,  H,  S,  S,  S,  S,  H,  H,  L,  A,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 3
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  A,  G,  G,  G,  G,  G,  G,  G,  G,  G,  G,  A,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 4  gold band
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  K,  T,  Y,  Y,  G,  Y,  Y,  G,  Y,  Y,  T,  K,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 5
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  K,  O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  K,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 6  visor line
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  V,  J,  D,  P,  E,  P,  P,  P,  P,  E,  P,  D,  J,  V,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 7  face / eyes
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  V,  D,  P,  P,  P,  P,  P,  P,  P,  P,  P,  P,  D,  V,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 8  face
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  D,  P,  P,  P,  D,  D,  D,  D,  P,  P,  P,  D,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 9  mouth area
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  R,  D,  P,  P,  P,  P,  P,  P,  P,  P,  D,  R,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 10 chin
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  O,  R,  D,  D,  D,  D,  D,  D,  R,  O,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 11 neck
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  G,  G,  G,  G,  G,  G,  G,  G,  G,  G,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 12 collar gold
  [ _,  _,  _,  _,  O,  O,  _,  _,  _,  O,  K,  A,  L,  L,  L,  H,  H,  L,  L,  L,  A,  K,  O,  _,  _,  _,  _,  _,  O,  O,  _,  _],  # 13 shoulders
  [ _,  _,  _,  O,  A,  L,  O,  _,  O,  K,  A,  L,  A,  A,  K,  G,  G,  K,  A,  A,  L,  A,  K,  O,  _,  O,  S,  W,  O,  _,  _,  _],  # 14 pauldrons + sword top
  [ _,  _,  O,  Q,  U,  I,  U,  O,  K,  A,  L,  A,  K,  K,  O,  G,  G,  O,  K,  K,  A,  L,  A,  K,  O,  _,  O,  S,  O,  _,  _,  _],  # 15 shield + torso
  [ _,  O,  Q,  U,  I,  I,  U,  Q,  O,  A,  K,  K,  O,  O,  G,  Y,  Y,  G,  O,  O,  K,  K,  A,  O,  _,  _,  O,  S,  O,  _,  _,  _],  # 16 shield + chest emblem
  [ _,  O,  Q,  U,  G,  Y,  G,  U,  O,  A,  K,  K,  O,  G,  Y,  Y,  Y,  Y,  G,  O,  K,  K,  A,  O,  _,  _,  O,  S,  O,  _,  _,  _],  # 17 shield emblem
  [ _,  O,  Q,  U,  G,  Y,  G,  U,  O,  A,  K,  K,  O,  O,  G,  Y,  Y,  G,  O,  O,  K,  K,  A,  O,  _,  _,  O,  S,  O,  _,  _,  _],  # 18
  [ _,  O,  Q,  U,  I,  I,  U,  Q,  O,  A,  K,  A,  K,  K,  O,  G,  G,  O,  K,  K,  A,  K,  A,  O,  _,  _,  O,  S,  O,  _,  _,  _],  # 19
  [ _,  _,  O,  Q,  U,  I,  U,  O,  K,  A,  A,  K,  K,  K,  K,  K,  K,  K,  K,  K,  K,  A,  A,  K,  O,  _,  O,  S,  O,  _,  _,  _],  # 20
  [ _,  _,  _,  O,  Q,  U,  O,  _,  O,  K,  A,  K,  O,  O,  O,  O,  O,  O,  O,  O,  K,  A,  K,  O,  _,  O,  S,  W,  O,  _,  _,  _],  # 21 belt line
  [ _,  _,  _,  _,  O,  O,  _,  _,  _,  O,  G,  T,  G,  G,  Y,  Y,  Y,  Y,  G,  G,  T,  G,  O,  _,  _,  _,  O,  S,  O,  _,  _,  _],  # 22 belt gold
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  C,  B,  C,  C,  C,  C,  C,  C,  B,  C,  O,  _,  _,  _,  _,  O,  S,  O,  _,  _,  _],  # 23 tunic / red
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  C,  C,  F,  F,  C,  C,  F,  F,  C,  C,  O,  _,  _,  _,  O,  G,  T,  O,  _,  _,  _],  # 24 tunic + sword guard
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  B,  C,  C,  F,  C,  C,  F,  C,  C,  B,  O,  _,  _,  _,  O,  J,  V,  O,  _,  _,  _],  # 25 tunic + grip
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  K,  O,  B,  B,  C,  B,  B,  C,  B,  B,  O,  K,  O,  _,  _,  _,  O,  V,  O,  _,  _,  _],  # 26 legs top
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  A,  K,  O,  O,  O,  O,  O,  O,  O,  O,  K,  A,  O,  _,  _,  _,  _,  O,  _,  _,  _,  _],  # 27 legs armor
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  A,  L,  A,  K,  O,  _,  O,  O,  _,  O,  K,  A,  L,  A,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 28 legs
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  K,  A,  K,  O,  _,  _,  _,  _,  _,  _,  O,  K,  A,  K,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 29
  [ _,  _,  _,  _,  _,  _,  _,  O,  G,  T,  G,  O,  _,  _,  _,  _,  _,  _,  _,  _,  O,  G,  T,  G,  O,  _,  _,  _,  _,  _,  _,  _],  # 30 boots gold
  [ _,  _,  _,  _,  _,  _,  O,  O,  K,  K,  O,  O,  _,  _,  _,  _,  _,  _,  _,  _,  O,  O,  K,  K,  O,  O,  _,  _,  _,  _,  _,  _],  # 31 boots
]

W_SIZE = 32
H_SIZE = 32

pixels = []
for row in grid:
    pixel_row = []
    for col in row:
        pixel_row.append(hex_to_rgba(col))
    pixels.append(pixel_row)

filepath = "Assets/Sprites/Warrior.png"
with open(filepath, 'wb') as f:
    f.write(save_png(pixels, W_SIZE, H_SIZE, filepath))

print(f"Saved {filepath}")

# Generate Unity .meta file
guid = ''.join(random.choice('0123456789abcdef') for _ in range(32))
sprite_id = ''.join(random.choice('0123456789abcdef') for _ in range(32))

meta_content = f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
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
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 32
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
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
    spriteID: {sprite_id}
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

with open(filepath + ".meta", 'w') as f:
    f.write(meta_content)

print(f"Saved {filepath}.meta")
