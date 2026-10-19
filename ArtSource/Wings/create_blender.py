"""Blender: Text Editor > Open > Run Script. Creates the two approved wing scenes.

Authoring source and native exports for the runtime PremiumWingMeshes asset.
Uses only Blender's included Python modules. Existing scenes are preserved.
"""
import bpy
import bmesh
import json
import math
import os

# BUILD_DATA is embedded by the packaging script for a standalone deliverable.
from pathlib import Path
DATA = json.loads((Path(__file__).resolve().parent / "PremiumWingParts.json").read_text())


def make_material(name, definition, scene):
    rgb, metallic, roughness, emission = definition
    material = bpy.data.materials.new('WS_' + name)
    material.diffuse_color = (*rgb, 1)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    bsdf = nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    emission_color = bsdf.inputs.get('Emission Color') or bsdf.inputs.get('Emission')
    emission_strength = bsdf.inputs.get('Emission Strength')
    if emission_color:
        emission_color.default_value = (*rgb, 1)
    if emission_strength:
        emission_strength.default_value = emission
    if (name.startswith('RGB_') or name.startswith('Spectrum_')):
        hue = nodes.new('ShaderNodeHueSaturation')
        hue.name = 'RGB_ColorCycle_6s'
        hue.inputs['Color'].default_value = (*rgb, 1)
        hue.inputs['Saturation'].default_value = 1
        hue.inputs['Value'].default_value = 1
        if name == 'RGB_Channel':
            vertex = nodes.new('ShaderNodeVertexColor')
            vertex.layer_name = 'WingColor'
            material.node_tree.links.new(vertex.outputs['Color'], hue.inputs['Color'])
        driver = hue.inputs['Hue'].driver_add('default_value').driver
        # Hue=.5 is neutral. Current scene uses 30 fps.
        driver.expression = '(0.5 + frame / 180.0) % 1.0'
        material.node_tree.links.new(hue.outputs['Color'], bsdf.inputs['Base Color'])
        if emission_color:
            material.node_tree.links.new(hue.outputs['Color'], emission_color)
    return material


def make_rig(scene, collection, key):
    data = bpy.data.armatures.new('WS_' + key + '_Rig')
    rig = bpy.data.objects.new('WS_' + key + '_Rig', data)
    collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    root = data.edit_bones.new('Root')
    root.head = (0, 0, 0)
    root.tail = (0, 0, .25)
    for side, sign in [('R', 1), ('L', -1)]:
        wing = data.edit_bones.new('Wing.' + side)
        wing.head = (sign * .2, .07, .13)
        wing.tail = (sign * .76, .07, .60)
        wing.parent = root
        tip = data.edit_bones.new('Tip.' + side)
        tip.head = wing.tail
        tip.tail = (sign * 1.3, .07, .72)
        tip.parent = wing
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.select_set(False)
    rig.show_in_front = True
    rig['Attachment'] = 'Root at upper back, example height 1.35m on a 1.80m character'
    rig['Forward'] = '+Y for character; decorated rear is -Y; up is +Z'
    rig['Explosion'] = 0.0
    rig.id_properties_ui('Explosion').update(min=0, max=1, description='0 assembled / 1 exploded construction view; reset to 0 before export')
    return rig


def build():
    start_scene = bpy.context.scene
    for key, parts in DATA['models'].items():
        scene = bpy.data.scenes.new('WingShop_' + key)
        bpy.context.window.scene = scene
        scene.unit_settings.system = 'METRIC'
        scene.unit_settings.scale_length = 1
        scene.render.fps = 30
        scene.frame_end = 180
        scene.world = bpy.data.worlds.new('WS_' + key + '_World')
        scene.world.use_nodes = True
        scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.035, .05, .08, 1)
        scene.world.node_tree.nodes['Background'].inputs[1].default_value = .5
        collection = bpy.data.collections.new(key + '_Export')
        scene.collection.children.link(collection)
        rig = make_rig(scene, collection, key)
        mats = {n: make_material(n, d, scene) for n, d in DATA['materials'].items()}
        for p in parts:
            mesh = bpy.data.meshes.new(p['name'])
            mesh.from_pydata(p['verts'], [], p['faces'])
            mesh.update()
            if 'vertexColors' in p or p['material']=='RGB_Channel':
                attribute=mesh.color_attributes.new(name='WingColor',type='FLOAT_COLOR',domain='CORNER')
                for loop in mesh.loops:
                    color=p.get('vertexColors',[DATA['materials'][p['material']][0]]*len(mesh.vertices))[loop.vertex_index]
                    attribute.data[loop.index].color=(*color,1)
            bm = bmesh.new()
            bm.from_mesh(mesh)
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
            bm.to_mesh(mesh)
            bm.free()
            obj = bpy.data.objects.new(p['name'], mesh)
            collection.objects.link(obj)
            names = list(dict.fromkeys(p.get('faceMaterials', [p['material']])))
            for material_name in names:
                mesh.materials.append(mats[material_name])
            for index, polygon in enumerate(mesh.polygons):
                polygon.material_index = names.index(p.get('faceMaterials', [p['material']] * len(mesh.polygons))[index])
            obj.parent = rig
            group = obj.vertex_groups.new(name=p['bone'])
            group.add(list(range(len(mesh.vertices))), 1, 'REPLACE')
            modifier = obj.modifiers.new('Rigid wing rig', 'ARMATURE')
            modifier.object = rig
            obj['PartGroup'] = p['group']
            obj['SourceMaterial'] = p['material']
            obj['GameCosmeticMesh'] = True
            g, s = int(p['group']), p['side']
            shift = (s * (.17 + .075 * g), -(g - 3) * .14, (g - 4) * .06) if s else (0, -(g - 1) * .24, (g - 1) * .11)
            for axis, offset in enumerate(shift):
                driver = obj.driver_add('location', axis).driver
                var = driver.variables.new()
                var.name = 'explode'
                var.targets[0].id = rig
                var.targets[0].data_path = '["Explosion"]'
                driver.expression = repr(offset) + ' * explode'
        # Orthographic inspection camera; no background geometry in export collection.
        camera_data = bpy.data.cameras.new(key + '_Camera')
        camera = bpy.data.objects.new(key + '_Camera', camera_data)
        scene.collection.objects.link(camera)
        camera.location = (3.0, -7.0, 2.2)
        from mathutils import Vector
        camera.rotation_euler = (Vector((0, 0, .15)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
        camera_data.type = 'ORTHO'
        camera_data.ortho_scale = 4.3
        scene.camera = camera
        for name, loc, energy, size in [('Key', (0, -4, 4), 950, 5), ('Rim', (3, 2, 2), 1200, 3)]:
            light_data = bpy.data.lights.new(key + '_' + name, 'AREA')
            light_data.energy = energy
            light_data.shape = 'DISK'
            light_data.size = size
            light = bpy.data.objects.new(key + '_' + name, light_data)
            scene.collection.objects.link(light)
            light.location = loc
            light.rotation_euler = (Vector((0, 0, .2)) - light.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.resolution_x = 1600
        scene.render.resolution_y = 1200
        scene.render.resolution_percentage = 100
        # Named rigid wing bones and a seamless idle clip. Runtime animates equivalent
        # side transforms directly; FBX retains this editable source animation.
        for side,sign in [('R',1),('L',-1)]:
            bone=rig.pose.bones['Wing.'+side]
            bone.rotation_mode='XYZ'
            for frame,angle in [(1,0),(46,1),(91,0),(136,-1),(181,0)]:
                bone.rotation_euler[2]=sign*math.radians(angle*(3 if key=='Angel' else 2))
                bone.keyframe_insert(data_path='rotation_euler',frame=frame)
        scene.frame_set(1)
        # Named but intentionally unkeyed rig: manual animation starts from a neutral pose.
        scene['Readme'] = 'Select rig, Custom Properties > Explosion. Reset 0 for export. RGB material animation: play frames 1-180. Save as .blend.'
        bpy.context.view_layer.objects.active = rig
        rig.select_set(True)
        for area in bpy.context.screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.region_3d.view_distance = 5
                area.spaces.active.region_3d.view_location = (0, 0, .1)
                area.spaces.active.clip_end = 100
        print('WingShop created:', key, len(parts), 'mesh objects')
    bpy.context.window.scene = bpy.data.scenes.get('WingShop_Angel') or bpy.context.scene
    print('Done. File > Save As: WingShop.blend. Existing scenes were preserved.')


if __name__ == '__main__':
    if DATA is None:
        raise RuntimeError('Missing embedded model data; use the packaged script.')
    build()

# Explicit CLI opt-in. Interactive use leaves the user's current file unsaved.
import sys
if '--write-blend' in sys.argv:
    target = Path(__file__).resolve().parent / 'ApprovedWings.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(target), compress=True)

if '--export-game' in sys.argv:
    for key in DATA['models']:
        scene=bpy.data.scenes['WingShop_'+key]
        bpy.context.window.scene=scene
        bpy.ops.object.select_all(action='DESELECT')
        for obj in bpy.data.collections[key+'_Export'].objects:obj.select_set(True)
        bpy.ops.export_scene.fbx(filepath=str(Path(__file__).resolve().parent/(key+'Wings.fbx')),
            use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',
            add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False)

if '--render' in sys.argv:
    for key in DATA['models']:
        scene=bpy.data.scenes['WingShop_'+key]
        bpy.context.window.scene=scene
        scene.render.engine='BLENDER_EEVEE'
        scene.eevee.use_bloom=True
        scene.eevee.bloom_intensity=.12
        scene.view_settings.view_transform='AgX'
        scene.render.resolution_x=1600;scene.render.resolution_y=1000
        scene.camera.location=(.5,-7,1.9)
        from mathutils import Vector
        scene.camera.rotation_euler=(Vector((0,0,.2))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
        scene.camera.data.ortho_scale=4.35
        for frame in ([1,61,121] if key=='RGB' else [1]):
            scene.frame_set(frame)
            scene.render.filepath=str(Path(__file__).resolve().parent/(key+'_GamePreview_'+str(frame)+'.png'))
            bpy.ops.render.render(write_still=True)
