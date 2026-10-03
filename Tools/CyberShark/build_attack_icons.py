"""Render Nerok's five attack illustrations entirely in Blender.
Run: blender --background --python Tools/CyberShark/build_attack_icons.py
Slots: Q anchor pull, W pressure hull, E tidal wave, R abyssal vortex, basic anchor strike.
Illustrations use real shaded meshes rather than thin HUD glyphs. No external artwork.
"""
from pathlib import Path
import math, random, os
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=Path(os.environ.get('NEROK_ICON_OUTPUT',str(ROOT/'Assets/Alterverse/Resources/CyberShark/Icons')))
ART=ROOT/'ArtSource/Heroes/CyberShark'
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
random.seed(7402)
def mat(name,color,metal=0,rough=.3,glow=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=glow
 return m
navy=mat('Anchor gunmetal',(.032,.092,.16),.82,.22)
steel=mat('Silver worn bevels',(.32,.53,.69),.88,.22)
gold=mat('Brass anchor trim',(.63,.29,.055),.8,.27)
water=mat('Ocean water',(.009,.16,.38),.4,.18,.18)
cyan=mat('Bright water crests',(.025,.60,.95),.3,.17,.7)
foam=mat('White sea foam',(.57,.91,1),.15,.2,1.5)
rock=mat('Impact stone',(.045,.075,.13),.15,.8)
amber=mat('Impact sparks',(1,.44,.065),.2,.25,3)
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE_NEXT'
scene.eevee.taa_render_samples=48;scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
scene.world.color=(.004,.009,.025);scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=.35
scene.use_nodes=True;n=scene.node_tree.nodes;n.clear();rl=n.new('CompositorNodeRLayers');gl=n.new('CompositorNodeGlare');gl.glare_type='FOG_GLOW';gl.quality='HIGH';gl.threshold=1.6;gl.size=7;gl.mix=-.88
comp=n.new('CompositorNodeComposite');scene.node_tree.links.new(rl.outputs['Image'],gl.inputs['Image']);scene.node_tree.links.new(gl.outputs['Image'],comp.inputs['Image'])
root=None

def finish(ob,material,parent=True):
 ob.data.materials.append(material)
 if parent and root:ob.parent=root
 if ob.type=='MESH':
  for p in ob.data.polygons:p.use_smooth=True
 return ob

def sphere(name,p,r,material,scale=None):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=r,location=p);ob=bpy.context.object;ob.name=name
 if scale:ob.scale=scale
 return finish(ob,material)

def tube(name,points,r,material):
 c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.resolution_u=12;c.bevel_depth=r;c.bevel_resolution=3
 sp=c.splines.new('POLY');sp.points.add(len(points)-1)
 for q,p in zip(sp.points,points):q.co=(*p,1)
 ob=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(ob);return finish(ob,material)

def ring(name,c,r,thick,material,plane='xz',ellipse=1):
 pts=[]
 for i in range(65):
  a=i*math.tau/64
  d=(r*math.cos(a),0,r*math.sin(a)*ellipse) if plane=='xz' else (r*math.cos(a),r*math.sin(a)*ellipse,0)
  pts.append(tuple(c[j]+d[j] for j in range(3)))
 return tube(name,pts,thick,material)

def poly(name,coords,depth,material,y=0):
 vertices=[(x,y+dy,z) for dy in [-depth/2,depth/2] for x,z in coords];l=len(coords)
 faces=[tuple(range(l-1,-1,-1)),tuple(range(l,2*l))]+[(i,(i+1)%l,(i+1)%l+l,i+l) for i in range(l)]
 m=bpy.data.meshes.new(name);m.from_pydata(vertices,[],faces);m.update();ob=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(ob);finish(ob,material)
 be=ob.modifiers.new('Worn rounded edges','BEVEL');be.width=.025;be.segments=3
 ob.modifiers.new('Weighted face normals','WEIGHTED_NORMAL');return ob

def anchor(scale=1,angle=0,position=(0,0,0)):
 global root
 saved=root;gr=bpy.data.objects.new('Heavy detailed anchor',None);bpy.context.collection.objects.link(gr);gr.parent=root;root=gr
 tube('Anchor shaft',[(0,0,-.38),(0,0,.53)],.075,navy)
 tube('Shaft polished center',[(0,-.069,-.27),(0,-.069,.42)],.014,cyan)
 ring('Anchor head eye',(0,0,.61),.145,.043,gold)
 tube('Anchor crossbar',[(-.34,0,.31),(.34,0,.31)],.065,steel)
 for x in [-.34,.34]:sphere('Crossbar end cap',(x,0,.31),.079,gold)
 # Crescent flukes are thick, readable silhouettes, with metal cutting bevels.
 for s in [-1,1]:
  pts=[(0,-.44),(s*.17,-.39),(s*.32,-.29),(s*.43,-.09),(s*.43,.02),(s*.60,-.17),(s*.45,-.19),(s*.35,-.45),(s*.12,-.58),(0,-.59)]
  poly('Curved forged fluke',pts,.13,navy)
  tube('Fluke cutting edge',[(0,-.082,-.52),(s*.21,-.082,-.44),(s*.34,-.082,-.30),(s*.41,-.082,-.15)],.019,steel)
  for x,z in [(s*.20,-.44),(s*.34,-.28)]:sphere('Anchor rivet',(x,-.086,z),.021,gold,scale=(1,.45,1))
  tube('Grip wrapping',[(s*.065,-.075,.02),(s*.065,-.075,.25)],.016,gold)
 ring('Shaft pressure cuff',(0,0,-.22),.088,.017,gold,plane='xy')
 gr.scale=(scale,scale,scale);gr.rotation_euler.y=angle;gr.location=position;root=saved
 return gr

def chain(points):
 for i,p in enumerate(points):
  ob=ring('Individual chain link',p,.074,.022,steel,ellipse=1.4)
  if i%2:ob.rotation_euler.z=math.pi/2

def droplets(count,area=(-.8,.8,-.7,.8),material=foam):
 for i in range(count):
  x=random.uniform(area[0],area[1]);z=random.uniform(area[2],area[3]);sphere('Water droplet',(x,random.uniform(-.1,.2),z),random.uniform(.012,.030),material,scale=(.65,.65,1.3))

def ribbon(name,pts,width,material):
 vs=[]
 for x,y,z in pts:vs.extend([(x,y,z-width/2),(x,y,z+width/2)])
 fs=[(2*i,2*i+1,2*i+3,2*i+2) for i in range(len(pts)-1)]
 m=bpy.data.meshes.new(name);m.from_pydata(vs,[],fs);m.update();ob=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(ob);finish(ob,material)
 sol=ob.modifiers.new('Water sheet thickness','SOLIDIFY');sol.thickness=.018

def aim(ob,p):ob.rotation_euler=(Vector(p)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(0,-5,1.3));cam=bpy.context.object;cam.name='Icon portrait camera';aim(cam,(0,0,0));cam.data.type='ORTHO';cam.data.ortho_scale=2.04;scene.camera=cam
for pos,power,color,size in [((-2,-4,4),500,(.40,.74,1),4),((2,-1,1),320,(1,.68,.27),3),((0,2,3),650,(.05,.65,1),2)]:
 bpy.ops.object.light_add(type='AREA',location=pos);ob=bpy.context.object;ob.data.energy=power;ob.data.color=color;ob.data.shape='DISK';ob.data.size=size;aim(ob,(0,0,0))
# Backdrop has a physical dark ocean disk; all important details stay within the HUD's circle.
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,1.0,0),rotation=(math.pi/2,0,0));finish(bpy.context.object,mat('Deep abyss backdrop',(.002,.013,.034),0,.8),False)
background=bpy.context.object.data.materials[0];nodes=background.node_tree.nodes;nodes.clear();em=nodes.new('ShaderNodeEmission');em.inputs['Color'].default_value=(.003,.017,.041,1);em.inputs['Strength'].default_value=.8;output=nodes.new('ShaderNodeOutputMaterial');background.node_tree.links.new(em.outputs[0],output.inputs['Surface'])
roots=[]
for slot in range(5):
 root=bpy.data.objects.new(['Q Anchor pull','W Pressure hull','E Tidal wave','R Abyssal vortex','Basic Anchor strike'][slot],None);bpy.context.collection.objects.link(root);roots.append(root)
 if slot==0:
  anchor(.92,-.48,(.07,-.1,-.04))
  chain([(-.56+i*.11,.12,.68-.06*i) for i in range(8)])
  for j in range(3):
   pts=[]
   for i in range(36):
    t=i/35;pts.append((-.76+1.44*t,.19,-.5+j*.17+.18*math.sin(t*math.pi)))
   tube('Pulling water streak',pts,.027 if j==1 else .012,cyan)
  droplets(18)
 elif slot==1:
  outline=[(-.47,.55),(0,.69),(.47,.55),(.43,-.08),(.25,-.42),(0,-.65),(-.25,-.42),(-.43,-.08)]
  poly('Heavy pressure shield',outline,.20,steel)
  poly('Shield inner navy armor',[(x*.84,z*.84) for x,z in outline],.05,navy,y=-.14)
  tube('Glowing shield rim',[(x*.88,-.182,z*.88) for x,z in outline+[outline[0]]],.024,cyan)
  poly('Shield raised armored spine',[(-.06,.47),(.06,.47),(.085,-.18),(0,-.43),(-.085,-.18)],.04,gold,y=-.19)
  ring('Pressure reactor',(0,-.246,.09),.14,.028,steel)
  sphere('Water reactor glass',(0,-.26,.09),.115,cyan,scale=(1,.35,1))
  for s in [-1,1]:
   for z in [-.08,.15,.38]:sphere('Shield securing bolt',(s*.31,-.197,z),.023,gold)
   tube('Deflected water barrier',[(s*(.58+.05*math.sin(i/30*math.pi)),.04,-.52+i/30*1.10) for i in range(31)],.027,cyan)
  droplets(22)
 elif slot==2:
  # Thick breaking wave: a curling wall plus foam on the lip, not a flat ring.
  pts=[]
  for i in range(70):
   t=i/69;a=-1.05+t*4.5;r=.50-.31*t
   pts.append((-.12+r*math.cos(a),-.12,.10+r*math.sin(a)))
  ribbon('Curling wave body',pts,.21,water)
  tube('Foaming wave curl',[(x,y-.035,z+.08) for x,y,z in pts],.043,foam)
  for layer in range(4):
   pts=[(-.80+1.6*i/60,.14+layer*.035,-.48+layer*.105+.1*math.sin(i/60*math.pi*1.5)) for i in range(61)]
   ribbon('Deep rolling wave',pts,.17,water if layer%2==0 else cyan)
   tube('Wave surface highlights',[(x,y-.10,z+.085) for x,y,z in pts],.015,foam)
  for i in range(24):
   a=random.uniform(-.1,math.pi);r=random.uniform(.51,.72);sphere('Crest foam spray',(-.12+r*math.cos(a),-.15,.1+r*math.sin(a)),random.uniform(.012,.031),foam)
 elif slot==3:
  # Face-on oblique whirlpool funnel, broad bright lips surrounding a dark core.
  for arm in range(5):
   pts=[]
   for i in range(105):
    t=i/104;a=arm*math.tau/5+t*math.tau*1.25;r=.10+.70*t
    pts.append((r*math.cos(a),.12+.17*(1-t),r*math.sin(a)*.72))
   ribbon('Spiral funnel arm',pts,.055+.025*arm,water if arm%2 else cyan)
   tube('Spiral white foam ridge',[(x,y-.04,z+.019) for x,y,z in pts],.012,foam)
  sphere('Dark abyss vortex core',(0,-.01,0),.12,navy,scale=(1,.25,.75))
  ring('Vortex core luminous rim',(0,-.075,0),.13,.018,cyan,ellipse=.75)
  droplets(24,(-.76,.76,-.57,.6),cyan)
 elif slot==4:
  anchor(.92,.68,(-.12,-.08,.06))
  # Strike rubble and a fan of glowing shock splinters make this distinct from Q.
  for i in range(11):
   p=(random.uniform(-.58,.65),random.uniform(-.10,.25),random.uniform(-.65,-.36))
   bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=random.uniform(.07,.15),location=p);ob=bpy.context.object;ob.scale=(1,.7,.5);finish(ob,rock)
  impact=(.29,-.20,-.48)
  for i in range(11):
   a=.10+i*math.pi/11;length=random.uniform(.20,.40)
   tube('Impact spark',[impact,(impact[0]+length*math.cos(a),-.18,impact[2]+length*math.sin(a))],.014,amber if i%3==0 else foam)
  for j in range(2):
   tube('Swing motion wake',[(.7*math.cos(.1+i/50*2.1),.19,-.01+.7*math.sin(.1+i/50*2.1)) for i in range(51)],.027-j*.011,cyan if j else foam)
  ring('Water impact shock ring',(.21,-.15,-.47),.30,.022,cyan,ellipse=.24)
 # Save each image with only that ability's mesh hierarchy visible.
 for gr in roots:
  for ob in gr.children_recursive:ob.hide_render=(gr!=root)
 scene.render.filepath=str(OUT/f'{slot}.png');bpy.ops.render.render(write_still=True)
# Source opens on Q illustration. Rendered textures retain their established GUIDs/import settings.
for gr in roots:
 for ob in gr.children_recursive:ob.hide_render=gr!=roots[0]
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'NerokAttackIcons.blend'),compress=True)
print('NEROK_ATTACK_ICONS',OUT)
