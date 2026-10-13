"""Build the approved ivory/gold/emerald Tahla equipment from editable solids.
Blender -b --python build_equipment.py -- --source ArtSource/Heroes/Mira.blend
"""
import argparse,bpy,bmesh,csv,gzip,hashlib,json,math,sys
from pathlib import Path
from mathutils import Matrix,Vector,Euler

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/Heroes/TahlaEquipment'
RES=ROOT/'Assets/Alterverse/Resources/TahlaEquipment'
ART.mkdir(parents=True,exist_ok=True);RES.mkdir(parents=True,exist_ok=True)
parser=argparse.ArgumentParser();parser.add_argument('--source',required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
source=Path(args.source).resolve();bpy.ops.wm.open_mainfile(filepath=str(source))
arm=bpy.data.objects['DF_Skeleton'];body=bpy.data.objects['DF_Anatomy']
arm.data.pose_position='REST';bpy.context.view_layer.update()
C=Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)));B=C.inverted()
def point(bone):return C@(arm.matrix_world@arm.data.bones[bone].head_local)
def is_skin(name):return 'skin' in name.lower() or name.lower().startswith('flesh')
def influence(vertex,names):
 return sum(g.weight for g in vertex.groups if body.vertex_groups[g.group].name in names)

# Keep the connected textile anatomy, excluding separate old cloth panels.
parent=list(range(len(body.data.vertices)));selected=set()
def find(i):
 while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
 return i
def join(a,b):
 a,b=find(a),find(b)
 if a!=b:parent[b]=a
candidate=set()
for p in body.data.polygons:
 name=body.data.materials[p.material_index].name
 if not (is_skin(name) or name=='Obsidian fabric'):continue
 candidate.add(p.index);selected.update(p.vertices)
 for i in p.vertices[1:]:join(p.vertices[0],i)
seams={}
for i in selected:
 key=tuple(round(x*100000) for x in body.data.vertices[i].co)
 if key in seams:join(i,seams[key])
 else:seams[key]=i
counts={}
for i in selected:counts[find(i)]=counts.get(find(i),0)+1
main=max(counts,key=counts.get)
mainFaces={p.index for p in body.data.polygons if p.index in candidate and find(p.vertices[0])==main}
# Fitting uses retained anatomy and footwear; no old armor or detached vestments.
body.data.calc_loop_triangles()
bodyTriangles=[(tuple(C@(body.matrix_world@body.data.vertices[i].co) for i in t.vertices),
                {body.vertex_groups[g.group].name for i in t.vertices for g in body.data.vertices[i].groups if g.weight>=.10})
 for t in body.data.loop_triangles if t.polygon_index in mainFaces or
 all(influence(body.data.vertices[i],{'FootL','FootR'})>=.95 for i in t.vertices)]
palette=[('Ivory armor',(.84,.83,.73),.32,.32),('Warm gold',(.79,.56,.23),.78,.25),
 ('Deep teal',(.025,.083,.072),.40,.38),('Emerald crystal',(.07,.92,.50),.12,.23),
 ('Dark backing',(.012,.033,.032),.04,.55)]
mats=[]
for name,color,metal,rough in palette:
 m=bpy.data.materials.new('TAH '+name);m.diffuse_color=(*color,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1)
 bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 if name=='Emerald crystal':bs.inputs['Emission Color'].default_value=(*color,1);bs.inputs['Emission Strength'].default_value=.8
 mats.append(m)
collection=bpy.data.collections.new('Tahla approved equipment');bpy.context.scene.collection.children.link(collection)
parts=[];modules=[]

def finish(ob,name,mat,bone,frame,assembly,group,layer=0):
 bpy.context.view_layer.objects.active=ob
 for modifier in list(ob.modifiers):bpy.ops.object.modifier_apply(modifier=modifier.name)
 bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
 for v in ob.data.vertices:v.co=B@(frame@v.co)
 ob.data.update();ob.name='TAH_'+name;ob.data.materials.append(mats[mat])
 for p in ob.data.polygons:p.use_smooth=False
 ob['part_id']=name;ob['material_slot']=mat;ob['rig_bone']=bone;ob['assembly']=assembly;ob['module']=group;ob['layer']=layer
 for col in list(ob.users_collection):col.objects.unlink(ob)
 collection.objects.link(ob)
 vg=ob.vertex_groups.new(name=bone);vg.add(list(range(len(ob.data.vertices))),1,'REPLACE')
 mod=ob.modifiers.new('Existing rigid bone binding','ARMATURE');mod.object=arm
 ob.parent=arm;ob.matrix_parent_inverse=arm.matrix_world.inverted();parts.append(ob)
 return ob

def prism(name,outline,z0,z1,mat,bone,frame,assembly,group,layer=0,bevel=.0015):
 n=len(outline);vertices=[(x,y,z) for z in [z0,z1] for x,y in outline]
 faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
 ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob)
 if bevel:
  modifier=ob.modifiers.new('Manufactured chamfer','BEVEL');modifier.width=bevel;modifier.segments=2
 return finish(ob,name,mat,bone,frame,assembly,group,layer)

def shield(w,h):return [(-w*.34,-h*.38),(0,-h*.5),(w*.34,-h*.38),(w*.5,-h*.12),(w*.5,h*.27),(w*.29,h*.5),(-w*.29,h*.5),(-w*.5,h*.27),(-w*.5,-h*.12)]
def plate(pid,bone,centre,w,h,rotation=(0,0,0),frame=None):
 frame=frame or Matrix.Translation(Vector(centre))@Euler(rotation).to_matrix().to_4x4()
 inv=frame.inverted();depth=[]
 region={bone,'Spine','Chest'} if bone in ('Chest','Spine') else {bone}
 if bone.startswith('Shin'):region|={'Thigh'+bone[-1],'Foot'+bone[-1]}
 if bone.startswith('Forearm'):region|={'UpperArm'+bone[-1],'Hand'+bone[-1]}
 for triangle,names in bodyTriangles:
  if not names&region:continue
  pp=[inv@p for p in triangle]
  if max(p.x for p in pp)<-w*.5 or min(p.x for p in pp)>w*.5:continue
  if max(p.y for p in pp)<-h*.5 or min(p.y for p in pp)>h*.5:continue
  depth.append(max(p.z for p in pp))
 if depth:frame.translation+=frame.to_3x3()@Vector((0,0,max(depth)+.010))
 start=len(parts)
 prism(pid+'_backing',shield(w*.94,h*.94),-.006,-.002,4,bone,frame,'Armor',pid,0,.001)
 prism(pid+'_gold_border',shield(w,h),-.002,.006,1,bone,frame,'Armor',pid,1)
 prism(pid+'_ivory_face',shield(w*.84,h*.84),.006,.010,0,bone,frame,'Armor',pid,2)
 for i,x in enumerate([-w*.32,w*.32]):
  cylinder(pid+'_hex_'+str(i),frame@Vector((x,-h*.22,.011)),.003,.003,1,bone,'Armor',pid,3,
           rotation=frame.to_euler(),segments=6)
 modules.append({'id':pid,'bone':bone,'widthMetres':w,'heightMetres':h,
                 'frame':[v for row in frame for v in row],'parts':[o['part_id'] for o in parts[start:]]})
 return frame

def cylinder(name,centre,radius,length,mat,bone,assembly,group,layer=0,rotation=(0,0,0),segments=12):
 bpy.ops.mesh.primitive_cylinder_add(vertices=segments,radius=radius,depth=length)
 ob=bpy.context.object;mod=ob.modifiers.new('Cylinder bevel','BEVEL');mod.width=min(.001,radius*.1);mod.segments=1
 return finish(ob,name,mat,bone,Matrix.Translation(Vector(centre))@Euler(rotation).to_matrix().to_4x4(),assembly,group,layer)

def crystal(name,centre,w,h,d,bone,assembly,group,frame=None):
 vertices=[(0,-h*.5,0),(-w*.5,0,0),(0,0,-d*.5),(w*.5,0,0),(0,0,d*.5),(0,h*.5,0)]
 faces=[(0,2,1),(0,3,2),(0,4,3),(0,1,4),(5,1,2),(5,2,3),(5,3,4),(5,4,1)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update();ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob)
 return finish(ob,name,3,bone,frame or Matrix.Translation(Vector(centre)),assembly,group)

def bone_frame(bone,t):
 a=point(bone);b=C@(arm.matrix_world@arm.data.bones[bone].tail_local);up=(a-b).normalized()
 normal=Vector((0,0,1));normal=(normal-up*normal.dot(up)).normalized();right=up.cross(normal).normalized()
 frame=Matrix.Identity(4)
 for i in range(3):frame[i][0]=right[i];frame[i][1]=up[i];frame[i][2]=normal[i]
 frame.translation=a+(b-a)*t
 return frame

# Compact mirrored shields; free joints and front-only leg plates.
plate('C01','Chest',(-.078,1.333,.12),.137,.187,rotation=(0,-.12,-.08))
plate('C02','Chest',(.078,1.333,.12),.137,.187,rotation=(0,.12,.08))
for i,y in enumerate([1.179,1.112,1.050],1):plate('A0'+str(i),'Spine',(0,y,.12),.188-i*.01,.071)
back=plate('B01','Chest',(0,1.283,-.10),.228,.211,rotation=(0,math.pi,0))
crystal('B01_emerald',(0,0,0),.041,.081,.028,'Chest','Armor','B01',back@Matrix.Translation(Vector((0,.032,.033))))
chest=next(m for m in modules if m['id']=='C01');chestZ=max(Matrix([chest['frame'][i:i+4] for i in range(0,16,4)]).translation.z,.13)
crystal('C03_emerald',(0,1.365,chestZ+.024),.043,.087,.029,'Chest','Armor','C03')
for side,sign in [('L',-1),('R',1)]:
 shoulder=plate('S_'+side,'UpperArm'+side,(sign*.220,1.423,.020),.157,.132,rotation=(-.78,sign*.32,sign*-.22))
 prism('S_'+side+'_mount',[(-.031,-.022),(.031,-.022),(.031,.022),(-.031,.022)],-.030,-.005,2,'UpperArm'+side,shoulder,'Armor','S_'+side,bevel=.002)
 plate('R_'+side,'Spine',(sign*.120,1.167,.065),.068,.128,rotation=(0,sign*.70,0))
 plate('H_'+side,'Thigh'+side,(sign*.155,.866,.10),.137,.227,rotation=(0,sign*.36,sign*-.07))
 plate('F_'+side,'Forearm'+side,(0,0,0),.082,.149,frame=bone_frame('Forearm'+side,.50))
 plate('K_'+side,'Shin'+side,(sign*.153,.503,.066),.093,.088)
 plate('L_'+side,'Shin'+side,(sign*.177,.299,.055),.091,.266)
 plate('P_'+side,'Foot'+side,(sign*.196,.096,.087),.098,.104,rotation=(-.42,0,0))

# An open golden brow/temple assembly. The original white hair and elf face are retained.
head=point('Head');skull=[]
skinVertices={i for p in body.data.polygons if body.data.materials[p.material_index].name=='Skin' for i in p.vertices}
for v in body.data.vertices:
 if influence(v,{'Head'})<.5:continue
 if v.index not in skinVertices:continue
 p=C@(body.matrix_world@v.co)
 if abs(p.x-head.x)<.095 and head.y+.10<p.y<head.y+.43:skull.append(p)
browY=max((p.y for p in skull if p.z>.03),default=head.y+.32)-.105
frontZ=max((p.z for p in skull if abs(p.y-browY)<.025),default=.082)+.009
for side,sign in [('L',-1),('R',1)]:
 frame=Matrix.Translation(Vector((sign*.078,browY,frontZ)))@Euler((0,sign*.30,sign*-.20)).to_matrix().to_4x4()
 prism('HE_'+side+'_brow',[(-.043,-.013),(.040,-.013),(.049,.010),(.026,.023),(-.026,.022),(-.043,.004)],-.004,.005,1,'Head',frame,'Armor','Helmet')
 prism('HE_'+side+'_temple',[(-.019,-.070),(.019,-.053),(.023,.025),(-.020,.035)],-.004,.004,1,'Head',
       Matrix.Translation(Vector((sign*.098,browY-.035,frontZ-.038)))@Euler((0,sign*1.10,0)).to_matrix().to_4x4(),'Armor','Helmet')
crystal('HE_brow_emerald',(0,browY+.019,frontZ+.014),.035,.048,.024,'Head','Armor','Helmet')
# A closed thin circlet joins brow and temple plates around the retained head/hair.
n=24;verts=[]
for y in [browY+.010,browY+.023]:
 for rx,rz in [(.104,.094),(.099,.089)]:
  for i in range(n):
   a=i*2*math.pi/n;verts.append((rx*math.cos(a),y,frontZ-.094+rz*math.sin(a)))
faces=[]
for i in range(n):
 j=(i+1)%n
 faces.extend([(i,j,2*n+j,2*n+i),(n+j,n+i,3*n+i,3*n+j),(j,i,n+i,n+j),(2*n+i,2*n+j,3*n+j,3*n+i)])
mesh=bpy.data.meshes.new('Helmet circlet');mesh.from_pydata(verts,[],faces);mesh.update()
ob=bpy.data.objects.new('Helmet circlet',mesh);bpy.context.collection.objects.link(ob)
finish(ob,'HE_circlet',1,'Head',Matrix.Identity(4),'Armor','Helmet')

# The staff uses the ORIGINAL authored handle-to-muzzle axis, rather than guessed hand placement.
anchor=C@bpy.data.objects['DF_HandleContactR'].matrix_world.translation
oldMuzzle=C@bpy.data.objects['DF_Muzzle'].matrix_world.translation
up=(oldMuzzle-anchor).normalized();right=(Vector((1,0,0))-up*up.x).normalized();normal=right.cross(up).normalized()
staff=Matrix.Identity(4)
for i in range(3):staff[i][0]=right[i];staff[i][1]=up[i];staff[i][2]=normal[i]
staff.translation=anchor
def staff_cylinder(name,y,r,length,mat,segments=12):
 centre=staff@Vector((0,y,0));rot=(staff.to_3x3()@Euler((math.pi*.5,0,0)).to_matrix()).to_euler()
 return cylinder(name,centre,r,length,mat,'WeaponGripR','Staff','Staff',rotation=rot,segments=segments)
staff_cylinder('W01_hex_shaft',-.192,.017,1.612,2,6)
for i,y in enumerate([-.660,.265],1):staff_cylinder('W02_ivory_grip_'+str(i),y,.020,.215,0,8)
for i,y in enumerate([-.998,-.775,-.545,.154,.377,.614],1):staff_cylinder('W03_gold_collar_'+str(i),y,.025,.025,1)
staff_cylinder('W04_head_socket',.632,.032,.079,1)
for side,sign in [('L',-1),('R',1)]:
 outline=[(0,.658),(sign*.116,.756),(sign*.130,.953),(sign*.111,.974),(sign*.093,.769),(0,.690)]
 prism('W05_'+side+'_fork',outline,-.018,.018,1,'WeaponGripR',staff,'Staff','Staff',bevel=.002)
prism('W05_rear_prong',[(-.009,.676),(.009,.676),(.011,.944),(0,.983),(-.011,.944)],-.034,-.020,1,'WeaponGripR',staff,'Staff','Staff')
crystal('W06_staff_emerald',(0,0,0),.096,.203,.070,'WeaponGripR','Staff','Staff',staff@Matrix.Translation(Vector((0,.811,0))))
crystal('W07_end_cap',(0,0,0),.035,.056,.035,'WeaponGripR','Staff','Staff',staff@Matrix.Translation(Vector((0,-1.034,0))))
muzzle=list(staff@Vector((0,.811,0)))

# Palm focus is oriented for the existing idle pose, then baked back into rest coordinates.
arm.data.pose_position='POSE'
for track in arm.animation_data.nla_tracks:track.mute=True
arm.animation_data.action=bpy.data.actions.get('Idle');bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
pose=arm.pose.bones['WeaponGripL'];delta=C.to_3x3()@(pose.matrix@pose.bone.matrix_local.inverted()).to_3x3()@B.to_3x3()
focus=Matrix.Identity(4);focusRotation=delta.inverted()
for i in range(3):
 for j in range(3):focus[i][j]=focusRotation[i][j]
focus.translation=C@bpy.data.objects['DF_PalmGripL'].matrix_world.translation
# Socket matrices are evaluated in pose, so recover the anatomical rest palm explicitly.
focus.translation=C@(pose.matrix@pose.bone.matrix_local.inverted()).inverted()@bpy.data.objects['DF_PalmGripL'].matrix_world.translation
prism('V01_palm_mount',[(-.045,-.018),(.045,-.018),(.058,.008),(.045,.027),(-.045,.027),(-.058,.008)],-.041,.041,2,'WeaponGripL',focus,'Focus','Focus')
prism('V02_gold_base',[(-.049,.008),(.049,.008),(.041,.031),(-.041,.031)],-.040,.040,1,'WeaponGripL',focus,'Focus','Focus')
for side,sign in [('L',-1),('R',1)]:
 prism('V03_'+side+'_support',[(sign*.032,.019),(sign*.047,.022),(sign*.069,.065),(sign*.046,.111),(sign*.038,.104),(sign*.057,.063)],-.007,.007,1,'WeaponGripL',focus,'Focus','Focus')
crystal('V04_focus_emerald',(0,0,0),.067,.112,.050,'WeaponGripL','Focus','Focus',focus@Matrix.Translation(Vector((0,.088,0))))
arm.data.pose_position='REST';bpy.context.view_layer.update()

# Shipping meshes are exact triangles from these named, editable native solids.
boneNames=sorted({o['rig_bone'] for o in parts})
assemblies=[]
for group,target in [('Armor','DF_TahlaArmor'),('Staff','DF_WeaponR'),('Focus','DF_WeaponL')]:
 vv=[];nn=[];uv=[];bones=[];slots=[{'triangles':[]} for _ in mats];table=[]
 for ob in [o for o in parts if o['assembly']==group]:
  ob.data.calc_loop_triangles();cache={};first=len(vv)//3
  for tri in ob.data.loop_triangles:
   pp=[C@ob.data.vertices[i].co for i in tri.vertices]
   if (pp[1]-pp[0]).cross(pp[2]-pp[0]).length<2e-11:continue
   normal=C.to_3x3()@ob.data.polygons[tri.polygon_index].normal;indices=[]
   for index in tri.vertices:
    p=C@ob.data.vertices[index].co;key=tuple(round(x,8) for x in (*p,*normal))
    if key not in cache:
     cache[key]=len(vv)//3;vv.extend(round(x,8) for x in p);nn.extend(round(x,8) for x in normal)
     uv.extend([round(p.x*4,6),round(p.y*4,6)]);bones.append(boneNames.index(ob['rig_bone']))
    indices.append(cache[key])
   slots[ob['material_slot']]['triangles'].extend(indices)
  table.append({'id':ob['part_id'],'bone':ob['rig_bone'],'module':ob['module'],'material':ob['material_slot'],
                'firstVertex':first,'vertexCount':len(vv)//3-first})
 assemblies.append({'name':target,'vertices':vv,'normals':nn,'uv':uv,'boneIndices':bones,'submeshes':slots,'parts':table})
data={'version':1,'revision':'tahla-approved-equipment-20261013','boneNames':boneNames,
      'pivot':list(point('Hips')),'referenceRigSpan':(point('Spine')-point('Hips')).length,
      'materials':[{'name':n,'color':list(col),'metallic':metal,'smoothness':1-rough} for n,col,metal,rough in palette],
      'assemblies':assemblies,'muzzle':muzzle,'staffHandle':list(anchor),'plateModules':modules}
resource=RES/'Model.bytes';resource.write_bytes(gzip.compress(json.dumps(data,separators=(',',':')).encode(),mtime=0))

# Preview cleanup uses the same preserve/remove categories as the Unity component.
under=bpy.data.materials.new('Tahla ivory undersuit');under.use_nodes=True
under.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.77,.80,.76,1)
under.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.60
body.data.materials.append(under);underIndex=len(body.data.materials)-1
bm=bmesh.new();bm.from_mesh(body.data);bm.verts.ensure_lookup_table();delete=[]
for face in bm.faces:
 name=body.data.materials[face.material_index].name.lower()
 headOnly=all(influence(body.data.vertices[v.index],{'Head'})>=.5 for v in face.verts)
 feetOnly=all(influence(body.data.vertices[v.index],{'FootL','FootR'})>=.95 for v in face.verts)
 headKeep=headOnly and (name.startswith('tahla white swept hair') or 'skin' in name or 'eye' in name or 'lip' in name or name=='energy')
 fabric=name=='obsidian fabric' and find(face.verts[0].index)==main
 if not (is_skin(name) or fabric or headKeep or feetOnly):delete.append(face);continue
 clothed=all(influence(body.data.vertices[v.index],{'Head','UpperArmL','UpperArmR','ForearmL','ForearmR','HandL','HandR'})<.15 for v in face.verts)
 if clothed or feetOnly:face.material_index=underIndex
bmesh.ops.delete(bm,geom=delete,context='FACES');bm.to_mesh(body.data);bm.free()
for ob in list(bpy.data.objects):
 if ob.type=='MESH' and ob not in parts and ob!=body:bpy.data.objects.remove(ob,do_unlink=True)
body.hide_set(False);body.hide_render=False;arm.hide_set(False)
# The in-game halo is retained by TahlaHalo; include a matching reference in the native preview.
retained={i for p in body.data.polygons for i in p.vertices}
headSkin=[C@(body.matrix_world@v.co) for v in body.data.vertices if v.index in retained and influence(v,{'Head'})>=.5]
haloY=max(p.y for p in headSkin)+.034
bpy.ops.mesh.primitive_torus_add(major_segments=32,minor_segments=8,location=tuple(B@Vector((0,haloY,0))),
                               major_radius=.083,minor_radius=.0035)
halo=bpy.context.object;halo.name='DF_TahlaHaloReference';halo.data.materials.append(mats[3])
for v in halo.data.vertices:v.co+=halo.location
halo.location=(0,0,0);vg=halo.vertex_groups.new(name='Head');vg.add(list(range(len(halo.data.vertices))),1,'REPLACE')
mod=halo.modifiers.new('Retained Head binding','ARMATURE');mod.object=arm
for material in body.data.materials:
 if material and is_skin(material.name) and material.use_nodes:
  material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.59,.77,.80,1)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=False;scene.use_nodes=False
for layer in scene.view_layers:layer.cycles.use_denoising=False
scene.view_settings.view_transform='AgX';scene.render.resolution_x=900;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
for ob in list(bpy.data.objects):
 if ob.type in ('CAMERA','LIGHT'):bpy.data.objects.remove(ob,do_unlink=True)
scene.world=bpy.data.worlds.new('Tahla studio');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.018,.027,.043,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.4
def aim(ob,p):ob.rotation_euler=(Vector(p)-ob.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size in [('Key',(-3,-4,4),600,4),('Fill',(3,-2,3),450,3),('Rim',(1,3,3),600,2)]:
 bpy.ops.object.light_add(type='AREA',location=loc);ob=bpy.context.object;ob.name=name;ob.data.energy=power;ob.data.size=size;aim(ob,(0,0,1))
bpy.ops.object.camera_add(location=(2.6,-6,2.8));camera=bpy.context.object;camera.data.type='ORTHO';camera.data.ortho_scale=2.36;scene.camera=camera;aim(camera,(0,0,1))
bpy.context.preferences.filepaths.save_version=0
arm.data.pose_position='POSE';arm.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1);bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'Tahla-equipment.blend'),compress=True)
for side,loc in [('front',(2.6,-6,2.8)),('back',(-2.6,6,2.8))]:
 camera.location=loc;aim(camera,(0,0,1));scene.render.filepath=str(ART/('Tahla-'+side+'.png'));bpy.ops.render.render(write_still=True)
metadata={'revision':data['revision'],'heroId':'Mira','armorModules':len(modules),'parts':len(parts),
 'assemblies':[{'name':a['name'],'vertices':len(a['vertices'])//3,'triangles':sum(len(s['triangles'])//3 for s in a['submeshes'])} for a in assemblies],
 'originalStaffHandle':list(anchor),'newStaffHandle':list(anchor),'sourceSnapshotSha256':hashlib.sha256(source.read_bytes()).hexdigest(),
 'runtimeSha256':hashlib.sha256(resource.read_bytes()).hexdigest(),'unityEditorExecuted':False}
(ART/'model.json').write_text(json.dumps(metadata,indent=2)+'\n')
print('TAHLA_EQUIPMENT_BUILT',json.dumps(metadata),flush=True)
