"""Build Nerok's original cyber-shark and anchor in Blender; export rigid skin geometry.
Run: blender --background --python Tools/CyberShark/build_cyber_shark.py
Uses the established +Y-up hero bone contract. No external artwork is embedded.
"""
from pathlib import Path
import math, json, gzip
import numpy as np
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
# Reuse the project's tested geometry primitives, without executing its hero construction.
helper=ROOT/'Tools/UmbralMage/build_umbral_mage.py'
exec(compile(helper.read_text().split('# Pearl armored arcane mech')[0].replace('from PIL import Image, ImageDraw', ''),str(helper),'exec'))
OUT=ROOT/'Assets/Alterverse/Resources/CyberShark'
ART=ROOT/'ArtSource/Heroes/CyberShark'
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
COLORS=[(.028,.045,.060),(.17,.24,.29),(.73,.81,.84),(.02,.67,.94),(.90,.10,.065),(.83,.90,.92)]
shell('armored torso',[(1.02,.16,.11,0),(1.25,.24,.15,0),(1.46,.30,.19,0),(1.57,.18,.13,0)],0,'Chest',20)
shell('abdomen',[(.89,.18,.12,0),(1.04,.20,.13,0),(1.23,.16,.11,0)],1,'Spine',16)
for s in [-1,1]:
 plate('layered breastplate',[(s*.025,1.54),(s*.24,1.52),(s*.285,1.40),(s*.18,1.28),(s*.035,1.33)],.145,.055,2,'Chest')
 tube('chest cyan seam',[(s*.045,1.49,.202),(s*.11,1.41,.209),(s*.18,1.36,.19)],.005,3,'Chest',6)
 for j in range(3):plate('abdomen course',[(s*.018,1.24-j*.075),(s*.16,1.22-j*.075),(s*.15,1.17-j*.075),(s*.018,1.19-j*.075)],.11,.025,1,'Spine')
 # Swept back fins and mechanical gill vents.
 shard('dorsal back fin',(s*.07,1.39,-.15),(s*.10,1.80,-.35),.070,.022,2,'Chest')
 for j in range(4):tube('gill vent',[(s*.20,1.50-j*.035,-.08),(s*.24,1.48-j*.035,.015)],.009,0,'Chest',6)
ell('shark skull',(0,1.735,.055),(.155,.17,.155),1,'Head',20,12)
ell('shark upper snout',(0,1.755,.155),(.145,.093,.16),2,'Head',20,10)
ell('open mouth shadow',(0,1.660,.192),(.123,.070,.070),0,'Head',16,10)
plate('lower armored jaw',[(-.125,1.65),(-.09,1.58),(0,1.565),(.09,1.58),(.125,1.65)],.19,.05,2,'Head')
shard('head dorsal crest',(0,1.79,-.02),(0,2.035,-.105),.08,.025,2,'Head')
for s in [-1,1]:
 shard('side brow fin',(s*.115,1.80,.05),(s*.205,1.955,-.015),.036,.022,2,'Head')
 ell('red eye',(s*.124,1.802,.281),(.026,.014,.015),4,'Head',10,6)
 for j in range(3):tube('cranial vent',[(s*.127,1.79-j*.025,.09),(s*.147,1.78-j*.025,.12)],.004,0,'Head',6)
for j in range(11):
 x=(j-5)*.020
 shard('upper tooth',(x,1.691,.258),(x,1.657-(.011 if j%2 else 0),.27),.007,.007,5,'Head')
 shard('lower tooth',(x,1.617,.254),(x,1.647+(.009 if j%2 else 0),.268),.007,.007,5,'Head')
for side,s in [('L',-1),('R',1)]:
 upper='UpperArm'+side;fore='Forearm'+side;hand='Hand'+side
 a=np.array(BONES[upper][0]);e=np.array(BONES[fore][0]);w=np.array(BONES[hand][0])
 limb('upper arm',a,e,.086,1,upper)
 ell('shoulder armor',a+[s*.05,-.025,0],(.135,.11,.125),2,upper,16,10)
 shard('shoulder fin',a+[s*.08,0,-.025],a+[s*.22,.10,-.07],.047,.025,2,upper)
 ell('elbow bearing',e,(.077,.066,.072),0,fore,12,8)
 limb('forearm',e,w,.081,1,fore)
 mid=(e+w)*.5
 plate('forearm plate',[(mid[0]-.07,mid[1]+.105),(mid[0]+.07,mid[1]+.105),(mid[0]+.06,mid[1]-.10),(mid[0]-.06,mid[1]-.10)],.05,.05,2,fore)
 tube('forearm energy',[(mid[0],mid[1]+.06,.10),(mid[0],mid[1]-.06,.10)],.005,3,fore,6)
 ell('palm',w+[s*.008,-.04,.02],(.043,.06,.041),1,hand,12,8)
 for j in range(4):
  x=w[0]+(j-1.5)*.021
  tube('finger',[(x,w[1]-.02,.052),(x,w[1]-.076,.055),(x,w[1]-.079,.018)],.010,2,hand,6)
 tube('thumb',[w+[s*.045,-.025,.01],w+[s*.054,-.05,.045],w+[s*.033,-.067,.05]],.012,2,hand,6)
 thigh='Thigh'+side;shin='Shin'+side;foot='Foot'+side
 h=np.array(BONES[thigh][0]);k=np.array(BONES[shin][0]);f=np.array(BONES[foot][0])
 limb('thigh',h,k,.092,1,thigh)
 x=(h[0]+k[0])*.5
 plate('thigh armor',[(x-.08,.91),(x+.08,.91),(x+.078,.67),(x-.073,.66)],.055,.05,2,thigh)
 ell('knee',k,(.083,.071,.076),2,shin,12,8)
 limb('shin',k,f,.076,1,shin)
 x=(k[0]+f[0])*.5
 plate('shin armor',[(x-.063,.47),(x+.063,.47),(x+.056,.19),(x-.053,.19)],.040,.045,2,shin)
 tube('shin light',[(x,.41,.090),(x,.25,.088)],.006,3,shin,6)
 ell('boot',f+[0,-.050,.060],(.087,.066,.145),1,foot,16,8)
 # Armored cable bundles on the outer thighs.
 tube('leg cable',[h+[s*.10,-.05,-.05],h+[s*.13,-.17,-.07],k+[s*.09,.10,-.04]],.008,0,thigh,8)
# The original standard anchor is the only weapon; Epic recolors this exact mesh.
GROUP=2
g=np.array(BONES['WeaponGripR'][0]);x=g[0]+.022;z=g[2]+.055
limb('anchor shank',(x,g[1]+.16,z),(x,g[1]-.46,z),.034,1,'WeaponGripR')
ring('anchor handle',(x,g[1]+.21,z),.069,.014,2,'WeaponGripR',segments=20)
tube('anchor crossbar',[(x-.17,g[1]-.12,z),(x+.17,g[1]-.12,z)],.029,2,'WeaponGripR',10)
tube('anchor curved arms',[(x-.24,g[1]-.30,z),(x-.18,g[1]-.44,z),(x,g[1]-.49,z),(x+.18,g[1]-.44,z),(x+.24,g[1]-.30,z)],.034,1,'WeaponGripR',12)
for s in [-1,1]:
 shard('anchor fluke',(x+s*.19,g[1]-.43,z),(x+s*.25,g[1]-.25,z),.066,.035,2,'WeaponGripR')
 tube('anchor cyan channel',[(x+s*.11,g[1]-.445,z+.032),(x+s*.20,g[1]-.34,z+.032)],.006,3,'WeaponGripR',6)
for j in range(6):ring('short grip chain',(x+.10,g[1]+.12-j*.048,z-.02),.029,.006,1,'WeaponGripR',normal=(0,0,1) if j%2 else (1,0,0),segments=12)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
mesh=bpy.data.meshes.new('NerokCyberShark');mesh.from_pydata(V,[],F);mesh.update()
obj=bpy.data.objects.new('Nerok Abyss',mesh);bpy.context.collection.objects.link(obj)
for idx,color in enumerate(COLORS):
 mat=bpy.data.materials.new('Nerok slot '+str(idx));mat.diffuse_color=(*color,1);mat.use_nodes=True;bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(*color,1);bsdf.inputs['Metallic'].default_value=.45;bsdf.inputs['Roughness'].default_value=.38;obj.data.materials.append(mat)
for face,m in zip(mesh.polygons,MI):face.material_index=m;face.use_smooth=True
for idx,name in enumerate(BN):
 group=obj.vertex_groups.new(name=name);indices=[i for i,b in enumerate(BI) if b==idx]
 if indices:group.add(indices,1,'REPLACE')
mesh.calc_loop_triangles()
vertices=[];normals=[];bones=[];groups=[];subs=[{'triangles':[]} for _ in COLORS]
for tri in mesh.loop_triangles:
 for loop in tri.loops:
  vi=mesh.loops[loop].vertex_index;idx=len(bones)
  vertices.extend(mesh.vertices[vi].co);normals.extend(mesh.corner_normals[loop].vector)
  bones.append(BI[vi]);groups.append(GI[vi]);subs[tri.material_index]['triangles'].append(idx)
data={'version':1,'boneNames':BN,'anchors':[{'head':BONES[b][0],'tail':BONES[b][1]} for b in BN],
 'vertices':vertices,'normals':normals,'boneIndices':bones,'groups':groups,'submeshes':subs,'markers':[]}
with gzip.GzipFile(filename=str(OUT/'Model.bytes'),mode='wb',mtime=0) as f:f.write(json.dumps(data,separators=(',',':')).encode())
# Blender-authored effect meshes are exported once and reused by the runtime pool.
def export_effect(name,verts,faces,alpha):
 m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update();m.calc_loop_triangles()
 ob=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(ob);ob.hide_render=True
 color=m.color_attributes.new(name='EffectAlpha',type='FLOAT_COLOR',domain='POINT')
 for i,a in enumerate(alpha):color.data[i].color=(1,1,1,a)
 indices=[v for t in m.loop_triangles for v in t.vertices]
 payload={'vertices':[float(c) for v in m.vertices for c in v.co], 'triangles':indices,'alpha':alpha}
 (OUT/(name+'.bytes')).write_text(json.dumps(payload,separators=(',',':')))
 ob.rotation_euler.x=math.pi/2
# Three feathered spiral ribbons, visibly descending into the vortex center.
v=[];f=[];a=[]
for arm in range(3):
 base=len(v)
 for i in range(65):
  t=i/64;angle=arm*2*math.pi/3+t*math.pi*2.3;r=.12+.82*t
  for edge in [-1,0,1]:
   rr=r+edge*.045*(.35+t);v.append((rr*math.cos(angle),.035+.19*(1-t),rr*math.sin(angle)))
   a.append(0 if edge else .80*math.sin(math.pi*t)**.35)
 for i in range(64):
  for edge in range(2):
   j=base+i*3+edge;f.extend([(j,j+1,j+4),(j,j+4,j+3)])
export_effect('WaterSpiral',v,f,a)
# A low curved wave crest, with a transparent upper rim and translucent lower margin.
v=[];f=[];a=[]
for i in range(49):
 angle=-1.2+2.4*i/48
 for j in range(4):
  height=[0,.08,.18,.25][j];r=[.82,.94,1,.99][j]
  v.append((r*math.sin(angle),height,r*math.cos(angle)));a.append([0,.65,.8,0][j]*math.sin(math.pi*i/48)**.4)
for i in range(48):
 for j in range(3):
  k=i*4+j;f.extend([(k,k+1,k+5),(k,k+5,k+4)])
export_effect('WaveCrest',v,f,a)
# The flying anchor uses the actual standard anchor geometry, normalized about its grip.
indices=[i for i,g in enumerate(GI) if g==2];remap={old:new for new,old in enumerate(indices)}
v=[((V[i][0]-x)*1.4,(V[i][1]-g[1])*1.4,(V[i][2]-z)*1.4) for i in indices]
f=[tuple(remap[i] for i in face) for face in F if all(i in remap for i in face)]
export_effect('AnchorProjectile',v,f,[.9]*len(v))
# Keep Blender's preview upright while exported gameplay geometry remains +Y up.
obj.rotation_euler.x=math.pi/2
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'NerokCyberShark.blend'))
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=700;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.world.color=(.025,.025,.025)
def aim(ob,point):ob.rotation_euler=(Vector(point)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2.4,-4.0,1.8));cam=bpy.context.object;aim(cam,(0,0,1.02));scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.4
for pos,power,size in [((2,-4,3),900,3),((-3,-1,2),1100,3),((0,3,2),700,2)]:
 bpy.ops.object.light_add(type='AREA',location=pos);light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size;aim(light,(0,0,1))
scene.render.filepath=str(ART/'preview.png');bpy.ops.render.render(write_still=True)
print('NEROK_EXPORT',len(bones),'vertices',len(bones)//3,'triangles',len(PARTS),'parts')
# Five dedicated HUD images, rendered from the Blender anchor/wave/vortex geometry.
icons=OUT/'Icons';icons.mkdir(exist_ok=True)
scene.render.resolution_x=256;scene.render.resolution_y=256;scene.eevee.taa_render_samples=24
fxmat=bpy.data.materials.new('HUD cyan luminous metal');fxmat.use_nodes=True
bsdf=fxmat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(.025,.65,.95,1)
bsdf.inputs['Emission Color'].default_value=(.01,.35,.7,1);bsdf.inputs['Emission Strength'].default_value=.8
shieldmesh=bpy.data.meshes.new('PressureHullIcon');shieldmesh.from_pydata([(-.4,0,.4),(.4,0,.4),(.34,0,-.10),(0,0,-.48),(-.34,0,-.1)],[],[(0,1,2),(0,2,3),(0,3,4)]);shieldmesh.update()
shield=bpy.data.objects.new('PressureHullIcon',shieldmesh);bpy.context.collection.objects.link(shield);shield.hide_render=True
for ob in [bpy.data.objects['AnchorProjectile'],bpy.data.objects['WaveCrest'],bpy.data.objects['WaterSpiral'],shield]:ob.data.materials.clear();ob.data.materials.append(fxmat)
for slot,name in enumerate(['AnchorProjectile','PressureHullIcon','WaveCrest','WaterSpiral','AnchorProjectile']):
 for ob in bpy.data.objects:
  if ob.type=='MESH':ob.hide_render=True
 target=bpy.data.objects[name];target.hide_render=False
 if name in ['AnchorProjectile','PressureHullIcon']:
  cam.location=(0,-3,.1);aim(cam,(0,0,-.12 if name=='AnchorProjectile' else 0));cam.data.ortho_scale=1.3
 else:
  cam.location=(0,-.3,3);aim(cam,(0,0,0));cam.data.ortho_scale=2.3
 scene.render.filepath=str(icons/(str(slot)+'.png'));bpy.ops.render.render(write_still=True)
# Keep an editable armature in the source .blend, using the same bones as the game exporter.
armdata=bpy.data.armatures.new('NerokArmature');arm=bpy.data.objects.new('NerokArmature',armdata);bpy.context.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for name in BN:
 bone=armdata.edit_bones.new(name);bone.head=BONES[name][0];bone.tail=BONES[name][1]
for name in BN:
 parent='Hips' if name=='Spine' else 'Spine' if name=='Chest' else 'Chest' if name=='Head' or name.startswith('UpperArm') else 'Hips' if name.startswith('Thigh') else 'UpperArm'+name[-1] if name.startswith('Forearm') else 'Forearm'+name[-1] if name.startswith('Hand') else 'Thigh'+name[-1] if name.startswith('Shin') else 'Shin'+name[-1] if name.startswith('Foot') else 'Hand'+name[-1] if name.startswith('WeaponGrip') else None
 if parent:armdata.edit_bones[name].parent=armdata.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT');arm.rotation_euler.x=math.pi/2
modifier=obj.modifiers.new('Rigid hero skin','ARMATURE');modifier.object=arm
for ob in bpy.data.objects:
 if ob.type=='MESH':ob.hide_render=ob!=obj
cam.location=(2.4,-4,1.8);aim(cam,(0,0,1.02));cam.data.ortho_scale=2.4
scene.render.resolution_x=700;scene.render.resolution_y=900
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'NerokCyberShark.blend'))
