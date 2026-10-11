"""Bake owned, periodic game finishes with Blender 4.5, Node Wrangler and TexTools 1.6.2.

Run Blender in background with both add-ons enabled and pass this file with --python.
The nine shared 256px finishes augment existing palettes/geology, never repaint models.
Packed data: RG signed height slope, B perceptual roughness, A neutral value.
Normal maps: OpenGL +Y, imported by Unity as NormalMap; all data is mipmapped.
"""
from pathlib import Path
import hashlib
import json
import math
import uuid

import addon_utils
import bpy
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Alterverse/Resources/MaterialFinishes'
ART = ROOT / 'ArtSource/MaterialFinishes'
SIZE = 256
OUT.mkdir(parents=True, exist_ok=True)
ART.mkdir(parents=True, exist_ok=True)
for addon in ('node_wrangler', 'textools'):
    if not addon_utils.check(addon)[1]:
        raise RuntimeError(f'Enable {addon} before baking')
prefs = bpy.context.preferences.addons['textools'].preferences
prefs.bake_color_space_def = 'PBR'
prefs.swizzle_y_coordinate = 'Y+'
prefs.bake_device = 'CPU'

v, u = np.mgrid[:SIZE, :SIZE] / SIZE
tau = math.tau

def periodic(cells, seed):
    rng = np.random.default_rng(seed)
    grid = rng.uniform(-.5, .5, (cells, cells))
    px, py = u * cells, v * cells
    ix, iy = np.floor(px).astype(int), np.floor(py).astype(int)
    fx, fy = px - ix, py - iy
    fx = fx**3 * (fx * (fx * 6 - 15) + 10)
    fy = fy**3 * (fy * (fy * 6 - 15) + 10)
    a = grid[iy % cells, ix % cells] * (1 - fx) + grid[iy % cells, (ix + 1) % cells] * fx
    b = grid[(iy + 1) % cells, ix % cells] * (1 - fx) + grid[(iy + 1) % cells, (ix + 1) % cells] * fx
    return a * (1 - fy) + b * fy

def cell_edges(count, seed):
    rng = np.random.default_rng(seed)
    sites = rng.uniform(.15, .85, (count, count, 2))
    px, py = u * count, v * count
    ix, iy = np.floor(px).astype(int), np.floor(py).astype(int)
    nearest, second = np.full_like(u, 100.), np.full_like(u, 100.)
    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            offset = sites[(iy + oy) % count, (ix + ox) % count]
            distance = np.sqrt((ix + ox + offset[..., 0] - px)**2 + (iy + oy + offset[..., 1] - py)**2)
            second = np.minimum(second, np.maximum(nearest, distance))
            nearest = np.minimum(nearest, distance)
    return nearest, second - nearest

def source_fields():
    grain, edge = cell_edges(13, 720)
    brush = np.sin(tau * u * 46 + periodic(5, 22) * .28)
    metal = periodic(7, 19) * .018 + brush * .008 + periodic(51, 21) * .004
    stone = periodic(11, 31) * .12 + periodic(29, 37) * .035 - np.exp(-(edge / .035)**2) * .035
    concrete = periodic(8, 43) * .025 + periodic(37, 47) * .014 - np.exp(-(grain / .075)**2) * .026
    clods = periodic(12, 53)
    soil = clods * .15 + periodic(31, 59) * .035
    sand = np.exp(-(cell_edges(33, 61)[0] / .28)**2) * .030 + periodic(47, 67) * .006
    bark_ridge = np.cos(tau * u * 9 + periodic(5, 71) * .8)
    bark = bark_ridge * .07 - np.exp(-((bark_ridge + .96) / .12)**2) * .045 + periodic(23, 73) * .020
    vein = np.exp(-(np.sin(tau * u * 2) / .14)**2)
    leaf = vein * .025 + np.exp(-(np.sin(tau * (u * 8 + v * 7)) / .2)**2) * .010 + periodic(23, 79) * .004
    ice_edge = np.exp(-(cell_edges(7, 83)[1] / .025)**2)
    ice = -ice_edge * .016 + periodic(19, 89) * .003
    threads = np.sin(tau * u * 38) * np.cos(tau * v * 38)
    fabric = threads * .008 + periodic(11, 97) * .004
    return {
        'Metal': (metal, .29 + periodic(7, 101) * .10 + brush * .025, (.36, .43, .51)),
        'Stone': (stone, .79 + periodic(13, 103) * .15, (.40, .35, .29)),
        'Concrete': (concrete, .83 + periodic(17, 107) * .11, (.43, .46, .47)),
        'Soil': (soil, .86 + clods * .13, (.22, .15, .09)),
        'Sand': (sand, .91 + periodic(9, 109) * .05, (.57, .42, .23)),
        'Bark': (bark, .82 + periodic(13, 113) * .10, (.23, .11, .055)),
        'Leaf': (leaf, .62 - vein * .09, (.10, .34, .075)),
        'Ice': (ice, .18 + ice_edge * .29, (.46, .67, .75)),
        'Fabric': (fabric, .86 + threads * .035, (.12, .16, .20)),
    }

def image_field(name, values):
    values = np.clip(values, .01, .99)
    rgba = np.stack((values, values, values, np.ones_like(values)), -1)
    image = bpy.data.images.new(name, SIZE, SIZE, float_buffer=True)
    image.colorspace_settings.name = 'Non-Color'
    image.pixels.foreach_set(rgba.astype(np.float32).ravel())
    image.filepath_raw = str(ART / name)
    image.file_format = 'OPEN_EXR'
    image.save()
    return image

def save_png(name, values):
    image = bpy.data.images.new(name, SIZE, SIZE, alpha=True)
    image.colorspace_settings.name = 'Non-Color'
    image.pixels.foreach_set(values.astype(np.float32).ravel())
    image.filepath_raw = str(OUT / (name + '.png'))
    image.file_format = 'PNG'
    image.save()
    bpy.data.images.remove(image)

def importer(name, normal=False):
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'dimensions-front/material-finishes/' + name).hex
    return f'''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: {1 if normal else 0}
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 1
  streamingMipmapsPriority: 0
  maxTextureSize: 256
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: 4
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 0
  alphaSource: 1
  alphaIsTransparency: 0
  textureType: {1 if normal else 0}
  textureShape: 1
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 256
    textureFormat: 4
    textureCompression: 0
    overridden: 0
  - serializedVersion: 3
    buildTarget: Android
    maxTextureSize: 256
    textureFormat: 4
    textureCompression: 0
    overridden: 1
  userData: {'TexTools tangent normal +Y. Unity NormalMap packing.' if normal else 'TexTools baked linear RG height slope B roughness A neutral value. No transparency.'}
  assetBundleName:
  assetBundleVariant:
'''

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.scene.render.engine = 'CYCLES'
bpy.context.scene.cycles.device = 'CPU'
bpy.context.scene.texToolsSettings.size = (SIZE, SIZE)
bpy.context.scene.texToolsSettings.bake_samples = 8
node_area = next(area for area in bpy.context.screen.areas if area.type == 'VIEW_3D')
node_area.type = 'NODE_EDITOR'
node_area.ui_type = 'ShaderNodeTree'
node_area.spaces.active.shader_type = 'OBJECT'
from textools import settings, utilities_bake

report = {}
for index, (name, (height, roughness, tint)) in enumerate(source_fields().items()):
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.mesh.primitive_plane_add(size=1)
    obj = bpy.context.object
    obj.name = name + '_low'
    obj.location.x = index * 1.3
    material = bpy.data.materials.new('DF_Baked_' + name)
    material.use_nodes = True
    obj.data.materials.append(material)
    bsdf = material.node_tree.nodes.get('Principled BSDF')
    material.node_tree.nodes.active = bsdf
    for node in material.node_tree.nodes:
        node.select = node == bsdf
    height = height - height.mean() + .5
    image_field(name + '_basecolor.exr', height)
    image_field(name + '_bump.exr', height)
    image_field(name + '_roughness.exr', roughness)
    node_area.type = 'NODE_EDITOR'
    node_area.ui_type = 'ShaderNodeTree'
    region = next(r for r in node_area.regions if r.type == 'WINDOW')
    with bpy.context.temp_override(area=node_area, region=region):
        result = bpy.ops.node.nw_add_textures_for_principled(directory=str(ART) + '/',
            files=[{'name': name + suffix + '.exr'} for suffix in ('_basecolor', '_bump', '_roughness')], relative_path=False)
    assert result == {'FINISHED'}
    bump = next(node for node in material.node_tree.nodes if node.type == 'BUMP')
    bump.inputs['Distance'].default_value = .012
    bump.inputs['Strength'].default_value = 1
    # TexTools needs a real high/low pair to capture relief in its normal bake.
    # Pad the high surface by sixteen texels so baked edge normals see adjacent
    # periodic relief. A tile-sized mesh would have one-sided derivatives at its rim.
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=289, y_subdivisions=289, size=1.125)
    high = bpy.context.object
    high.name = name + '_high'
    high.location = obj.location
    high_material = material.copy()
    next(n for n in high_material.node_tree.nodes if n.type == 'BUMP').inputs['Strength'].default_value = 0
    high.data.materials.append(high_material)
    for vertex in high.data.vertices:
        x = int(round((vertex.co.x + .5) * SIZE)) % SIZE
        y = int(round((vertex.co.y + .5) * SIZE)) % SIZE
        vertex.co.z = float(height[y, x] - .5) * .06
    for polygon in high.data.polygons:
        polygon.use_smooth = True
    for loop in high.data.loops:
        co=high.data.vertices[loop.vertex_index].co
        high.data.uv_layers.active.data[loop.index].uv=(co.x+.5,co.y+.5)
    high.data.update()
    obj.select_set(True)
    high.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.context.scene.texToolsSettings.bake_cage_extrusion = .02
    settings.sets = utilities_bake.get_bake_sets()
    assert len(settings.sets) == 1 and len(settings.sets[0].objects_high) == 1
    node_area.type = 'IMAGE_EDITOR'
    node_area.ui_type = 'UV'
    region = next(r for r in node_area.regions if r.type == 'WINDOW')
    baked = {}
    for mode in ('base_color', 'roughness', 'normal_tangent'):
        bpy.context.scene.TT_bake_mode = mode + '.bip'
        with bpy.context.temp_override(area=node_area, region=region):
            assert bpy.ops.uv.textools_bake() == {'FINISHED'}
        image = next(image for image in bpy.data.images if image.name == name.lower() + '_' + mode)
        values = np.asarray(image.pixels[:], dtype=np.float32).reshape(SIZE, SIZE, 4)
        assert np.isfinite(values).all()
        baked[mode] = values
    normal = baked['normal_tangent'][..., :3] * 2 - 1
    assert normal[..., :2].std() > .0008, (name, 'normal bake lost surface relief')
    slope = -normal[..., :2] / np.maximum(normal[..., 2:3], .25)
    bound = max(float(np.quantile(abs(slope), .995)), .001)
    value = baked['base_color'][..., 0]
    value = .5 + (value - value.mean()) * min(12., .105 / max(float(value.std()), .001))
    packed = np.dstack((.5 + np.clip(slope / bound, -1, 1) * .47,
                        np.clip(baked['roughness'][..., 0], .03, .97), np.clip(value, .03, .97)))
    save_png(name, packed)
    save_png(name + '_Normal', baked['normal_tangent'])
    (OUT / (name + '.png.meta')).write_text(importer(name))
    (OUT / (name + '_Normal.png.meta')).write_text(importer(name + '_Normal', normal=True))
    # Keep actual authoring materials, original height/roughness maps packed in the .blend.
    color = material.node_tree.nodes.new('ShaderNodeRGB')
    color.outputs[0].default_value = (*tint, 1)
    material.node_tree.links.new(color.outputs[0], bsdf.inputs['Base Color'])
    bsdf.inputs['Metallic'].default_value = .9 if name == 'Metal' else 0
    material.use_fake_user = True
    report[name] = {'bakes': list(baked), 'normalY': '+', 'size': [SIZE, SIZE],
                    'roughnessRange': [float(packed[..., 2].min()), float(packed[..., 2].max())],
                    'valueStd': float(packed[..., 3].std()), 'slopeBound': bound,
                    'sha256': hashlib.sha256((OUT / (name + '.png')).read_bytes()).hexdigest()}
    bpy.data.objects.remove(high, do_unlink=True)
    for node in list(material.node_tree.nodes):
        if node.type == 'TEX_IMAGE' and not any(output.is_linked for output in node.outputs):
            material.node_tree.nodes.remove(node)
    print('FINISH_BAKED', name, flush=True)

for _ in range(3):
    bpy.data.orphans_purge(do_recursive=True)
bpy.context.scene.render.image_settings.file_format = 'OPEN_EXR'
bpy.context.scene.render.image_settings.color_depth = '16'
bpy.context.scene.render.image_settings.exr_codec = 'ZIP'
for image in bpy.data.images:
    if image.source == 'FILE':
        image.save_render(image.filepath, scene=bpy.context.scene)
        image.reload()
        image.pack()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(ART / 'GameFinishes.blend'), compress=True)
# Source image files are embedded in the authoring .blend; do not ship redundant EXRs.
for path in ART.glob('*.exr'):
    path.unlink()
for path in ART.glob('*.blend1'):
    path.unlink()
report['_pipeline'] = {'blender': bpy.app.version_string, 'addons': ['Node Wrangler 3.55', 'TexTools 1.6.2'],
    'textures': 18, 'rgba8MipMemoryMiB': 18 * SIZE * SIZE * 4 * 4 / 3 / 1024**2,
    'scope': 'Texture authoring/baking, not a Unity or Android render test.'}
(ART / 'bake-report.json').write_text(json.dumps(report, indent=2) + '\n')
print('BAKE_COMPLETE', json.dumps(report['_pipeline']), flush=True)
