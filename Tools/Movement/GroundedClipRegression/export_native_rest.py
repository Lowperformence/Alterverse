"""Run in Blender: derive native rest rigs from build_heroes.py without meshes or animations.

The canonical model coordinates use X right, Y up, Z forward: (x,z,-y).
World bone matrices receive this basis transform; child locals follow from the converted
parent matrix, retaining their native bone-local axes. This is a mathematical canonical
fixture, not an assertion about Unity's FBX importer transform conventions.
"""
import ast
import csv
import json
import math
from pathlib import Path
import bpy
from mathutils import Vector, Matrix

repo = next(p for p in Path(__file__).resolve().parents if (p / 'Assets/Alterverse').is_dir())
source = repo / 'Tools/HeroRebuild/build_heroes.py'
syntax = ast.parse(source.read_text())
wanted = {'arm_points', 'rig', 'proportion', 'mesh', 'tube', 'rounded_boot'}
selected = []
for node in syntax.body:
    if isinstance(node, ast.FunctionDef) and node.name in wanted:
        selected.append(node)
    elif isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == 'PROFILE' for t in node.targets):
        selected.append(node)
namespace = {'bpy': bpy, 'Vector': Vector, 'math': math, 'parts': [], 'sin': math.sin, 'cos': math.cos, 'pi': math.pi}
exec(compile(ast.Module(body=selected, type_ignores=[]), str(source), 'exec'), namespace)
canonical = Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)))
output = Path(__file__).parent / 'fixtures'
output.mkdir(exist_ok=True)
summary = {'source': 'Tools/HeroRebuild/build_heroes.py', 'coordinateConversion': '(x,z,-y)',
           'rootPosition': [0,.32,0], 'rootScale': [1.22,1.32,1.22],
           'modelPosition': [0,-.96,0], 'modelScale': [1.10,1,1.10],
           'NerokRig': 'Rook', 'heroes': {}}
for hero in namespace['PROFILE']:
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    arm = namespace['rig'](hero)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for bone in arm.data.edit_bones:
        bone.head = namespace['proportion'](hero, bone.head)
        bone.tail = namespace['proportion'](hero, bone.tail)
    bpy.ops.object.mode_set(mode='OBJECT')
    converted = {b.name: canonical @ b.matrix_local for b in arm.data.bones}
    with (output / (hero + '-rest-rig.csv')).open('w', newline='') as file:
        writer = csv.writer(file)
        writer.writerow(['bone','parent','px','py','pz','qx','qy','qz','qw'])
        for bone in arm.data.bones:
            local = converted[bone.parent.name].inverted() @ converted[bone.name] if bone.parent else converted[bone.name]
            pos, rot, scale = local.decompose()
            writer.writerow([bone.name, bone.parent.name if bone.parent else '', *pos, rot.x,rot.y,rot.z,rot.w])
    # Only the native boots are generated, using their exact existing mesh function.
    material = bpy.data.materials.new('FixtureBoot')
    with (output / (hero + '-foot-sole.csv')).open('w', newline='') as file:
        writer = csv.writer(file); writer.writerow(['side','x','y','z'])
        for side_index, sign in enumerate((-1,1)):
            boot = namespace['rounded_boot'](sign, material, material)
            for vertex in boot.data.vertices:
                point = vertex.co.copy()
                if hero in ['Veyr','Nyra','Elyra','Mira']:
                    pivot = Vector((sign*.18,-.015,.08))
                    delta = point-pivot
                    point = pivot+Vector((delta.x*.80,delta.y*.80,delta.z*.90))
                point = canonical @ namespace['proportion'](hero, point)
                writer.writerow([side_index, *point])
    bones = {b.name: {'head': list(canonical @ b.head_local), 'tail': list(canonical @ b.tail_local)} for b in arm.data.bones}
    summary['heroes'][hero] = {'profile': namespace['PROFILE'][hero], 'bones': bones}
(output / 'native-rest-geometry.json').write_text(json.dumps(summary, indent=2) + '\n')
print('EXPORTED_NATIVE_REST_FIXTURES', ', '.join(namespace['PROFILE']), '; Nerok uses Rook')
