"""Replace Tahla's joined robe islands with articulated light armor. Blender 4.5."""
import bpy, bmesh, json, sys
from pathlib import Path
from mathutils import Vector

def replace(arm, body):
    if body.get('light_leg_armor_v1'): return
    bm=bmesh.new();bm.from_mesh(body.data);seen=set();remove=[];islands=0
    for vertex in bm.verts:
        if vertex in seen: continue
        stack=[vertex];seen.add(vertex);component=[]
        while stack:
            v=stack.pop();component.append(v)
            for edge in v.link_edges:
                other=edge.other_vert(v)
                if other not in seen: seen.add(other);stack.append(other)
        lo=[min(v.co[k] for v in component) for k in range(3)]
        hi=[max(v.co[k] for v in component) for k in range(3)]
        # Separate fabric panels and their gold embroidery, never the connected anatomy.
        long_panel=len(component)<1500 and lo[2]<.4 and hi[2]>1.0
        front_decoration=len(component)<200 and hi[2]<1.12 and hi[1]<-.14
        if long_panel or front_decoration: remove.extend(component);islands+=1
    assert islands>=15, 'Expected authored skirt/hem islands were not found'
    bmesh.ops.delete(bm,geom=remove,context='VERTS');bm.to_mesh(body.data);bm.free()
    gold=bpy.data.materials.get('Tahla engraved warm gold')
    ivory=bpy.data.materials.get('Tahla warm ivory silk')
    parts=[]
    for side in ['L','R']:
        for bone_name,width,depth in [('Thigh'+side,.064,.018),('Shin'+side,.052,.016)]:
            bone=arm.data.bones[bone_name];a=bone.head_local;b=bone.tail_local
            center=(a+b)*.5;length=(b-a).length*.68
            z=center.z;x=center.x;y=-.095 if bone_name.startswith('Thigh') else -.086
            outline=[(-width,length/2),(width,length/2),(width*.85,-length*.40),(0,-length*.52),(-width*.85,-length*.40)]
            for inset,material,offset in [(1,gold,0),(.85,ivory,-.006)]:
                verts=[(x+dx*inset,y+offset+dz,z+zz*inset) for dz in [-depth/2,depth/2] for dx,zz in outline]
                faces=[tuple(reversed(range(5))),tuple(range(5,10))]+[(i,(i+1)%5,(i+1)%5+5,i+5) for i in range(5)]
                mesh=bpy.data.meshes.new('Light leg plate');mesh.from_pydata(verts,[],faces);mesh.update()
                obj=bpy.data.objects.new('Tahla light '+bone_name,mesh);bpy.context.collection.objects.link(obj);obj.data.materials.append(material)
                group=obj.vertex_groups.new(name=bone_name);group.add(list(range(10)),1,'REPLACE')
                mod=obj.modifiers.new('Armature','ARMATURE');mod.object=arm;obj.parent=arm
                parts.append(obj)
    bpy.ops.object.select_all(action='DESELECT');body.select_set(True)
    for obj in parts:obj.select_set(True)
    bpy.context.view_layer.objects.active=body;bpy.ops.object.join();body['light_leg_armor_v1']=True
    print('TAHLA_LIGHT_ARMOR',json.dumps({'removed_islands':islands,'removed_vertices':len(remove),'plates':4}))

if __name__=='__main__':
    root=Path(__file__).resolve().parents[2]
    arm=bpy.data.objects['DF_Skeleton'];body=bpy.data.objects['DF_Anatomy'];replace(arm,body)
    out=root/'Assets/Alterverse/Resources/AuthoredHeroes';out.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.data.objects:
        if obj.name.startswith('DF_'):obj.select_set(True)
    bpy.context.view_layer.objects.active=arm
    bpy.ops.export_scene.fbx(filepath=str(out/'Mira.fbx'),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=.5,path_mode='AUTO')
    art=root/'ArtSource/Heroes';art.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(art/'Mira.blend'),compress=True)
    scene=bpy.context.scene;scene.render.resolution_percentage=70;scene.cycles.samples=16
    scene.render.filepath=str(art/'Mira-light-armor.png');bpy.ops.render.render(write_still=True)
