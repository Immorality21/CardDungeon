import struct, zlib, uuid, os

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

def hex_to_rgba(h):
    if h is None:
        return (0, 0, 0, 0)
    h = h.lstrip('#')
    return (int(h[0:2],16), int(h[2:4],16), int(h[4:6],16), 255)

# Palette
_ = None           # transparent
O = "#1a0a0a"      # outline dark
# Eyeball whites (yellowish-sickly)
W1 = "#8a7a60"     # white darkest (shadow edge)
W2 = "#b8a880"     # white dark
W3 = "#d4c8a0"     # white mid
W4 = "#e8ddb8"     # white light
W5 = "#f0eacc"     # white highlight
# Red veins
V1 = "#8b1a1a"     # vein dark
V2 = "#bb2222"     # vein mid
V3 = "#dd3333"     # vein bright
# Iris (sickly green/yellow)
I1 = "#2a4a10"     # iris darkest
I2 = "#4a7a18"     # iris dark
I3 = "#6aaa22"     # iris mid
I4 = "#88cc33"     # iris bright
I5 = "#aaee55"     # iris highlight
# Pupil
P1 = "#0a0a0a"     # pupil black
P2 = "#1a1a1a"     # pupil dark
P3 = "#cc2200"     # pupil red glint
# Eyelid / fleshy rim
L1 = "#3a1818"     # lid darkest
L2 = "#5a2828"     # lid dark
L3 = "#7a3838"     # lid mid
L4 = "#994848"     # lid light
L5 = "#aa5555"     # lid highlight
# Tendrils / tentacles hanging below
T1 = "#4a1a1a"     # tendril dark
T2 = "#6a2a2a"     # tendril mid
T3 = "#883838"     # tendril light

grid = [
    #0  1  2  3  4  5  6  7  8  9  10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30 31
    [ _,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_ ],  # 0
    [ _,_,_,_,_,_,_,_,_,_,O, O, O, O, O, O, O, O, O, O, O, O,_,_,_,_,_,_,_,_,_,_ ],  # 1
    [ _,_,_,_,_,_,_,_,O, O,L2,L3,L3,L4,L4,L5,L5,L4,L4,L3,L3,L2,O, O,_,_,_,_,_,_,_,_ ],  # 2
    [ _,_,_,_,_,_,_,O,L1,L2,L3,L4,L4,L5,L5,L5,L5,L5,L5,L4,L4,L3,L2,L1,O,_,_,_,_,_,_,_ ],  # 3  upper lid
    [ _,_,_,_,_,_,O,L1,L2,O, O, O, O, O, O, O, O, O, O, O, O, O, O,L2,L1,O,_,_,_,_,_,_ ],  # 4  lid-eye border
    [ _,_,_,_,_,O,L1,O,W1,W2,W3,W3,V2,W4,W4,W5,W4,W4,V2,W3,W3,W2,W1,O,L1,O,_,_,_,_,_,_ ],  # 5  eyeball top
    [ _,_,_,_,O,L1,O,W1,W2,W3,V1,W4,W4,W5,W5,W5,W5,W5,W4,W4,V1,W3,W2,W1,O,L1,O,_,_,_,_,_ ],  # 6
    [ _,_,_,_,O,L1,O,W1,W3,V2,W4,W4,I1,I2,I2,I3,I2,I2,I1,W4,W4,V2,W3,W1,O,L1,O,_,_,_,_,_ ],  # 7  iris top
    [ _,_,_,O,L1,O,W1,W2,V1,W4,W4,I1,I2,I3,I4,I4,I4,I3,I2,I1,W4,W4,V1,W2,W1,O,L1,O,_,_,_,_ ],  # 8
    [ _,_,_,O,L1,O,W1,W3,W4,W4,I1,I2,I3,I4,P1,P1,I4,I3,I2,I1,W4,W4,W3,W3,O,L1,O,_,_,_,_,_ ],  # 9  pupil row
    [ _,_,_,O,L1,O,W1,W3,V3,W4,I1,I2,I3,P1,P1,P3,I3,I3,I2,I1,W4,V3,W3,W2,O,L1,O,_,_,_,_,_ ],  # 10 pupil + glint
    [ _,_,_,O,L1,O,W1,W3,W4,W4,I1,I2,I3,I4,P1,P1,I4,I3,I2,I1,W4,W4,W3,W2,O,L1,O,_,_,_,_,_ ],  # 11 pupil row
    [ _,_,_,O,L1,O,W1,W2,V1,W4,W4,I1,I2,I3,I4,I5,I4,I3,I2,I1,W4,W4,V1,W2,O,L1,O,_,_,_,_,_ ],  # 12
    [ _,_,_,_,O,L1,O,W1,W3,V2,W4,W4,I1,I2,I2,I3,I2,I2,I1,W4,W4,V2,W3,W1,O,L1,O,_,_,_,_,_ ],  # 13 iris bottom
    [ _,_,_,_,O,L1,O,W1,W2,W3,V1,W4,W4,W4,W5,W4,W4,W4,W4,V1,W3,W3,W2,W1,O,L1,O,_,_,_,_,_ ],  # 14
    [ _,_,_,_,_,O,L1,O,W1,W2,W3,W3,V2,W3,W4,W4,W4,W3,V2,W3,W3,W2,W1,O,L1,O,_,_,_,_,_,_ ],  # 15 eyeball bottom
    [ _,_,_,_,_,_,O,L1,L2,O, O, O, O, O, O, O, O, O, O, O, O, O, O,L2,L1,O,_,_,_,_,_,_ ],  # 16 lid-eye border lower
    [ _,_,_,_,_,_,_,O,L1,L2,L3,L3,L4,L4,L4,L5,L4,L4,L4,L3,L3,L2,L1,O,_,_,_,_,_,_,_,_ ],  # 17 lower lid
    [ _,_,_,_,_,_,_,_,O, O,L2,L2,L3,L3,L3,L3,L3,L3,L3,L2,L2,O, O,_,_,_,_,_,_,_,_,_ ],  # 18
    [ _,_,_,_,_,_,_,_,_,_,O, O, O, O, O, O, O, O, O, O, O, O,_,_,_,_,_,_,_,_,_,_ ],  # 19 bottom lid edge
    [ _,_,_,_,_,_,_,_,_,_,_,O,T1,_,O,T2,T1,O,_,T1,O,_,_,_,_,_,_,_,_,_,_,_ ],  # 20 tendrils start
    [ _,_,_,_,_,_,_,_,_,_,O,T1,T2,_,_,O,T2,_,_,T2,T1,O,_,_,_,_,_,_,_,_,_,_ ],  # 21
    [ _,_,_,_,_,_,_,_,_,O,T1,T2,T3,O,_,_,O,_,O,T3,T2,T1,O,_,_,_,_,_,_,_,_,_ ],  # 22
    [ _,_,_,_,_,_,_,_,_,_,O,T2,O,T1,O,_,_,_,T1,O,T2,O,_,_,_,_,_,_,_,_,_,_ ],  # 23
    [ _,_,_,_,_,_,_,_,_,_,_,O,_,T2,T1,O,_,O,T2,_,O,_,_,_,_,_,_,_,_,_,_,_ ],  # 24
    [ _,_,_,_,_,_,_,_,_,_,_,_,_,O,T2,O,_,T1,O,_,_,_,_,_,_,_,_,_,_,_,_,_ ],  # 25
    [ _,_,_,_,_,_,_,_,_,_,_,_,_,_,O,_,_,O,_,_,_,_,_,_,_,_,_,_,_,_,_,_ ],  # 26
    [ _,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_ ],  # 27
    [ _,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_ ],  # 28
    [ _,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_ ],  # 29
    [ _,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_ ],  # 30
    [ _,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_ ],  # 31
]

W_SIZE = 32
H_SIZE = 32

for i in range(len(grid)):
    while len(grid[i]) < W_SIZE:
        grid[i].append(None)
    grid[i] = grid[i][:W_SIZE]

pixels = [[hex_to_rgba(grid[y][x]) for x in range(W_SIZE)] for y in range(H_SIZE)]

filepath = os.path.join("Assets", "Sprites", "evil_eye.png")
os.makedirs(os.path.dirname(filepath), exist_ok=True)

with open(filepath, 'wb') as f:
    f.write(save_png(pixels, W_SIZE, H_SIZE, filepath))

save_meta(filepath, W_SIZE, H_SIZE)

print(f"Saved {filepath} and {filepath}.meta")
