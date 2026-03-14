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

# Color palette - heavy armored tank, blue-steel theme
_ = None           # transparent
O = "#1a1a2e"      # outline
K = "#2a2a3e"      # darkest armor
A = "#3e4460"      # armor dark
M = "#586080"      # armor mid
L = "#7080a0"      # armor light
H = "#90a0c0"      # armor highlight
W = "#b0c0e0"      # bright highlight
P = "#d8b890"      # skin
D = "#b89870"      # skin shadow
R = "#987850"      # skin dark
E = "#2040a0"      # eye
G = "#c8a020"      # gold
Y = "#e0c040"      # gold highlight
T = "#a08018"      # gold dark
S = "#c0c8d8"      # steel
V = "#505868"      # dark steel

# 32x32 Tank hero - bulky, heavy armor, huge shield, mace
grid = [
#   0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30 31
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 0  helm top
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  A,  M,  L,  L,  H,  H,  L,  L,  M,  A,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 1
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  A,  M,  L,  H,  H,  W,  W,  H,  H,  L,  M,  A,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 2
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  A,  L,  H,  H,  W,  W,  W,  W,  H,  H,  L,  A,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 3
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  G,  G,  G,  G,  G,  G,  G,  G,  G,  G,  G,  G,  G,  G,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 4  gold crown band
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  T,  Y,  Y,  G,  Y,  G,  Y,  Y,  G,  Y,  G,  Y,  Y,  T,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 5
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  K,  O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  K,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 6  visor slit
  [ _,  _,  _,  _,  _,  _,  _,  O,  K,  V,  D,  P,  P,  E,  P,  P,  P,  P,  E,  P,  P,  D,  V,  K,  O,  _,  _,  _,  _,  _,  _,  _],  # 7  face
  [ _,  _,  _,  _,  _,  _,  _,  O,  K,  R,  D,  P,  P,  P,  P,  P,  P,  P,  P,  P,  P,  D,  R,  K,  O,  _,  _,  _,  _,  _,  _,  _],  # 8
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  R,  D,  P,  P,  D,  D,  D,  D,  D,  D,  P,  P,  D,  R,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 9
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  O,  R,  D,  P,  P,  P,  P,  P,  P,  P,  P,  D,  R,  O,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 10 chin guard
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  O,  O,  R,  D,  D,  D,  D,  D,  D,  R,  O,  O,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 11 neck
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  G,  G,  G,  G,  G,  G,  G,  G,  G,  G,  G,  G,  G,  G,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 12 gorget gold
  [ _,  _,  _,  O,  O,  O,  O,  O,  K,  A,  M,  L,  L,  H,  H,  H,  H,  H,  H,  L,  L,  M,  A,  K,  O,  O,  O,  O,  O,  _,  _,  _],  # 13 massive pauldrons
  [ _,  _,  O,  K,  A,  M,  L,  A,  K,  A,  M,  A,  K,  K,  G,  G,  G,  G,  K,  K,  A,  M,  A,  K,  A,  L,  M,  A,  K,  O,  _,  _],  # 14
  [ _,  O,  K,  A,  M,  L,  M,  A,  O,  K,  A,  K,  K,  O,  G,  Y,  Y,  G,  O,  K,  K,  A,  K,  O,  A,  M,  L,  M,  A,  K,  O,  _],  # 15 chest
  [ O,  K,  A,  M,  L,  H,  L,  M,  O,  K,  A,  K,  O,  G,  Y,  Y,  Y,  Y,  G,  O,  K,  A,  K,  O,  M,  L,  H,  L,  M,  A,  K,  O],  # 16 shield arm wide + chest
  [ O,  K,  A,  M,  G,  Y,  G,  M,  O,  K,  A,  K,  O,  G,  Y,  W,  W,  Y,  G,  O,  K,  A,  K,  O,  M,  L,  V,  S,  M,  A,  K,  O],  # 17 shield emblem + mace
  [ O,  K,  A,  M,  G,  Y,  G,  M,  O,  K,  A,  K,  O,  G,  Y,  Y,  Y,  Y,  G,  O,  K,  A,  K,  O,  M,  L,  V,  S,  M,  A,  K,  O],  # 18
  [ O,  K,  A,  M,  L,  H,  L,  M,  O,  K,  A,  K,  K,  O,  G,  Y,  Y,  G,  O,  K,  K,  A,  K,  O,  M,  V,  S,  W,  M,  A,  K,  O],  # 19 mace head
  [ _,  O,  K,  A,  M,  L,  M,  A,  O,  K,  A,  A,  K,  K,  O,  O,  O,  O,  K,  K,  A,  A,  K,  O,  A,  V,  S,  W,  A,  K,  O,  _],  # 20
  [ _,  _,  O,  K,  A,  M,  A,  K,  O,  K,  A,  K,  K,  K,  K,  K,  K,  K,  K,  K,  K,  A,  K,  O,  K,  O,  V,  O,  K,  O,  _,  _],  # 21
  [ _,  _,  _,  O,  O,  O,  O,  O,  _,  O,  G,  G,  G,  G,  Y,  Y,  Y,  Y,  G,  G,  G,  G,  O,  _,  _,  O,  V,  O,  _,  _,  _,  _],  # 22 belt
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  K,  A,  M,  M,  L,  L,  L,  L,  M,  M,  A,  K,  O,  _,  _,  _,  O,  _,  _,  _,  _,  _],  # 23 tasset
  [ _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  K,  A,  M,  L,  L,  M,  M,  L,  L,  M,  A,  K,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _],  # 24
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  K,  A,  K,  O,  O,  O,  K,  K,  O,  O,  O,  K,  A,  K,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 25 leg armor
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  A,  M,  A,  K,  O,  _,  O,  O,  _,  O,  K,  A,  M,  A,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 26
  [ _,  _,  _,  _,  _,  _,  _,  _,  O,  K,  A,  K,  O,  _,  _,  _,  _,  _,  _,  O,  K,  A,  K,  O,  _,  _,  _,  _,  _,  _,  _,  _],  # 27
  [ _,  _,  _,  _,  _,  _,  _,  O,  A,  M,  L,  A,  O,  _,  _,  _,  _,  _,  _,  O,  A,  L,  M,  A,  O,  _,  _,  _,  _,  _,  _,  _],  # 28 greaves
  [ _,  _,  _,  _,  _,  _,  _,  O,  K,  A,  K,  O,  _,  _,  _,  _,  _,  _,  _,  _,  O,  K,  A,  K,  O,  _,  _,  _,  _,  _,  _,  _],  # 29
  [ _,  _,  _,  _,  _,  _,  O,  G,  T,  G,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  G,  T,  G,  O,  _,  _,  _,  _,  _,  _],  # 30 boots gold
  [ _,  _,  _,  _,  _,  O,  O,  K,  K,  O,  O,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  O,  O,  K,  K,  O,  O,  _,  _,  _,  _,  _],  # 31 boots
]

W_SIZE = 32
H_SIZE = 32

pixels = []
for row in grid:
    pixel_row = []
    for col in row:
        pixel_row.append(hex_to_rgba(col))
    pixels.append(pixel_row)

filepath = "Assets/Sprites/Tank.png"
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
