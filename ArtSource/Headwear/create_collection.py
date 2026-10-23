"""Build three headwear prototypes and studio thumbnails from the exact Unity mesh.
Run with Blender: --background --python create_collection.py -- --root /repo
No purchases, ownership, rewards or player saves are changed.
"""
import argparse, json, math, sys
from pathlib import Path
import bpy, bmesh
from mathutils import Vector
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--root',type=Path,required=True)
p.add_argument('--render-only',action='store_true')
a=p.parse_args(sys.argv[sys.argv.index('--')+1:])
root=a.root.resolve()
resources=root/'Assets/Alterverse/Resources/Headwear'
art=root/'ArtSource/Headwear'
resources.mkdir(parents=True,exist_ok=True)
parts=[]
def reset():
    global parts
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    parts=[]
def surface(name,rgb,metal=0,rough=.5,glow=0):
    m=bpy.data.materials.new(name);m.use_nodes=True
    s=m.node_tree.nodes['Principled BSDF']
    s.inputs['Base Color'].default_value=(*rgb,1)
    s.inputs['Metallic'].default_value=metal;s.inputs['Roughness'].default_value=rough
    s.inputs['Emission Color'].default_value=(*rgb,1);s.inputs['Emission Strength'].default_value=glow
    m.diffuse_color=(*rgb,1)
    return m
def obj(name,verts,faces,mat):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    item=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(item)
    item.data.materials.append(mat);parts.append(item)
    for f in mesh.polygons:f.use_smooth=True
    return item
def lathe(name,profile,mat,n=48):
    # (radius,height,optional x offset). Closed cross-section retains a genuine head opening.
    v=[]
    for point in profile:
        r,z=point[:2];cx=point[2] if len(point)>2 else 0
        v.extend((cx+r*math.cos(i*math.tau/n),r*math.sin(i*math.tau/n),z) for i in range(n))
    faces=[]
    for row in range(len(profile)):
        nxt=(row+1)%len(profile)
        for i in range(n):
            j=(i+1)%n
            faces.append((row*n+i,row*n+j,nxt*n+j,nxt*n+i))
    return obj(name,v,faces,mat)
def orb(name,pos,scale,mat,n=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=n,ring_count=8,location=pos)
    item=bpy.context.object;item.name=name;item.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    item.data.materials.append(mat);parts.append(item)
    for f in item.data.polygons:f.use_smooth=True
    return item
def rod(name,start,end,r,mat):
    start,end=Vector(start),Vector(end);delta=end-start
    bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=r,depth=delta.length,location=(start+end)/2)
    item=bpy.context.object;item.name=name;item.rotation_euler=delta.to_track_quat('Z','Y').to_euler()
    item.data.materials.append(mat);parts.append(item);return item
def star(name,x,y,z,r,mat,project=None):
    v=[(x,project(x,z) if project else y-.018,z)]
    for i in range(10):
        t=i*math.tau/10+math.pi/2;d=r if i%2==0 else r*.43
        px,pz=x+math.cos(t)*d,z+math.sin(t)*d
        v.append((px,project(px,pz) if project else y,pz))
    return obj(name,v,[(0,1+i,1+(i+1)%10) for i in range(10)],mat)
def wizard():
    reset()
    violet=surface('Velvet violet',(.15,.045,.34),rough=.83)
    midnight=surface('Inner velvet',(.035,.025,.09),rough=.9)
    gold=surface('Antique gold',(.95,.54,.12),.5,.3)
    ivory=surface('Moon ivory',(.96,.88,.63),.12,.4)
    cyan=surface('Moonstone',(.10,.7,.87),.2,.25,.18)
    ink=surface('Buckle inset',(.025,.018,.045),rough=.6)
    lathe('Soft asymmetric brim',[(1,0),(1,.10),(1.5,.16),(1.8,.08),(1.86,.025),(1.82,-.025),(1.48,.015),(1,-.045)],violet)
    # Bent cone, with a second interior surface rather than a solid plug.
    lathe('Bent wizard crown',[(1,.08),(.95,.42),(.78,1.10),(.58,1.85,.05),
        (.35,2.48,.16),(.19,2.88,.35),(.08,3.04,.62),(.015,2.96,.88),
        (.018,2.89,.88),(.06,2.95,.62),(.15,2.80,.35),(.30,2.42,.16),
        (.52,1.82,.05),(.71,1.08),(.89,.4),(.94,.08)],violet,36)
    orb('Soft closed tip',(.88,0,2.94),(.04,.035,.065),violet)
    outer=[(.08,1,0),(.42,.95,0),(1.1,.78,0),(1.85,.58,.05),(2.48,.35,.16),(2.88,.19,.35)]
    def front(x,z):
        for (za,ra,xa),(zb,rb,xb) in zip(outer,outer[1:]):
            if za<=z<=zb:
                t=(z-za)/(zb-za);r=ra+(rb-ra)*t;cx=xa+(xb-xa)*t
                return -math.sqrt(max(.001,r*r-(x-cx)*(x-cx)))-.018
        raise ValueError('Wizard embroidery outside crown')
    lathe('Leather band',[(1.015,.14),(.97,.43),(.99,.44),(1.035,.15)],midnight)
    for z,r in [(.15,1.035),(.43,.993)]:
        lathe('Band gold piping',[(r,z),(r+.009,z+.016),(r+.009,z+.033),(r,z+.017)],gold,40)
    # Open buckle frame, centered on the front.
    for x in (-.19,.19):rod('Buckle side',(x,-1.02,.16),(x,-1.01,.43),.031,gold)
    for z in (.16,.43):rod('Buckle crossbar',(-.19,-1.02,z),(.19,-1.02,z),.031,gold)
    orb('Moonstone buckle',(0,-1.055,.295),(.09,.035,.10),cyan)
    for x,z,r in [(-.28,.85,.13),(.30,1.18,.11),(-.26,1.98,.11),(.20,2.35,.085)]:
        star('Embroidered star',x,0,z,r,gold,front)
    # Crescent sewn onto the upper front, built as a real ribbon.
    verts=[];faces=[]
    for i in range(19):
        t=.95+i*(math.tau-1.9)/18;u=1.39+i*(math.tau-2.78)/18
        x,z=.23*math.cos(t),1.52+.23*math.sin(t)
        ix,iz=.10+.19*math.cos(u),1.52+.19*math.sin(u)
        verts += [(x,front(x,z)-.008,z),(ix,front(ix,iz)-.008,iz)]
    for i in range(18):faces.append((i*2,i*2+1,i*2+3,i*2+2))
    obj('Ivory crescent',verts,faces,ivory)
    return [violet,midnight,gold,ivory,cyan,ink]
def propeller():
    reset()
    blue=surface('Cobalt fabric',(.025,.22,.65),rough=.72)
    red=surface('Coral fabric',(.85,.09,.075),rough=.72)
    yellow=surface('Sunshine fabric',(1,.61,.045),rough=.7)
    ivory=surface('Ivory stitching',(.95,.94,.82),rough=.66)
    cyan=surface('Mint rotor',(.04,.76,.64),.15,.3)
    ink=surface('Navy band',(.025,.055,.12),rough=.65)
    n,rows=32,8;v=[];faces=[]
    for row in range(rows+1):
        t=row/(rows+1)*math.pi/2
        v.extend((math.cos(i*math.tau/n)*math.cos(t),
                  math.sin(i*math.tau/n)*math.cos(t),math.sin(t)) for i in range(n))
    top=len(v);v.append((0,0,1))
    for row in range(rows):
        for i in range(n):j=(i+1)%n;faces.append((row*n+i,row*n+j,(row+1)*n+j,(row+1)*n+i))
    for i in range(n):faces.append((rows*n+i,rows*n+(i+1)%n,top))
    dome=obj('Four panel propeller cap',v,faces,blue)
    for m in (red,yellow,ivory):dome.data.materials.append(m)
    for face in dome.data.polygons:face.material_index=(face.index%n)//8%4
    lathe('Soft fitted band',[(.98,-.035),(1,.04),(1.02,.08),(1.035,.015),(1.01,-.04)],ink,40)
    # Short cap visor.
    v=[];faces=[]
    for i in range(19):
        t=-math.pi/3+i*math.pi*2/3/18
        for r,z in [(1,-.015),(1.76,-.10),(1.76,-.15),(1,-.06)]:
            v.append((math.sin(t)*(.95 if r==1 else 1.05),-math.cos(t)*r,z))
    for i in range(18):
        for side in range(4):faces.append((i*4+side,(i+1)*4+side,(i+1)*4+(side+1)%4,i*4+(side+1)%4))
    obj('Coral curved visor',v,faces,red)
    rod('Rotor axle',(0,0,.97),(0,0,1.37),.055,ink)
    orb('Top button',(0,0,1.02),(.15,.15,.08),yellow)
    # Wide rounded paddles read clearly at gameplay scale. Rigid geometry; no new animation loop.
    for i,mat in enumerate((cyan,red,yellow)):
        angle=i*math.tau/3
        c,s=math.cos(angle),math.sin(angle)
        points=[(.08,-.10),(.85,-.17),(1.25,-.02),(1.16,.18),(.46,.21),(.08,.09)]
        v=[(x*c-y*s,x*s+y*c,z) for z in (1.35,1.405) for x,y in points]
        faces=[tuple(range(5,-1,-1)),tuple(range(6,12))]
        faces += [(j,(j+1)%6,(j+1)%6+6,j+6) for j in range(6)]
        obj('Rotor paddle '+str(i),v,faces,mat)
    orb('Rotor hub',(0,0,1.39),(.16,.16,.09),ivory)
    return [blue,red,yellow,ivory,cyan,ink]
def mushroom():
    reset()
    red=surface('Berry cap',(.72,.035,.055),rough=.56)
    ivory=surface('Cream spots',(.96,.86,.65),rough=.7)
    peach=surface('Soft gills',(.57,.27,.16),rough=.85)
    gold=surface('Golden spore',(.94,.58,.12),.12,.45)
    teal=surface('Tiny moss',(.15,.34,.20),rough=.9)
    ink=surface('Inner shadow',(.12,.045,.035),rough=.9)
    cap=lathe('Mushroom velvet canopy',[(1,0),(1,.13),(1.42,.13),(1.94,.26),
        (1.90,.55),(1.64,.93),(1.18,1.28),(.58,1.51),(.015,1.57),
        (.015,1.43),(.55,1.37),(1.10,1.15),(1.48,.82),(1.70,.49),(1.71,.31),(1.38,.22),(1,.22)],red,48)
    lathe('Cream underside',[(1.01,.02),(1.01,.12),(1.40,.16),(1.92,.26),
        (1.88,.30),(1.38,.24),(1,.20)],ivory,48)
    lathe('Comfort headband',[(.98,-.035),(.98,.07),(1.025,.07),(1.025,-.035)],ink,32)
    for i in range(28):
        t=i*math.tau/28;c,s=math.cos(t),math.sin(t)
        rod('Radial gill',(1.03*c,1.03*s,.13),(1.82*c,1.82*s,.25),.013,peach)
    outer=[(1.94,.26),(1.90,.55),(1.64,.93),(1.18,1.28),(.58,1.51),(.015,1.57)]
    def radius_at(z):
        for (ra,za),(rb,zb) in zip(outer,outer[1:]):
            if za<=z<=zb:return ra+(rb-ra)*(z-za)/(zb-za)
        return .015
    def height_at(radius):
        for (ra,za),(rb,zb) in zip(outer,outer[1:]):
            if rb<=radius<=ra:return za+(zb-za)*(ra-radius)/(ra-rb)
        return 1.57 if radius<.015 else .26
    # Curved cream inlays conform to the serialized canopy surface at every vertex.
    for i,(theta,elev,size) in enumerate([(-1.57,.35,.25),(-2.3,.52,.30),(-.75,.63,.25),
        (-1.7,1.00,.24),(.1,.48,.28),(1,.72,.22),(2.4,.5,.31),(-.4,1.18,.20),(2.2,1.1,.18)]):
        z=.25+1.33*math.sin(elev);radius=radius_at(z)
        center=Vector((radius*math.cos(theta),radius*math.sin(theta),z))
        tangent=Vector((-math.sin(theta),math.cos(theta),0))
        meridian=Vector((math.cos(theta)*math.sin(elev),math.sin(theta)*math.sin(elev),-math.cos(elev)))
        def project(point):
            point.z=height_at(math.hypot(point.x,point.y))
            ok,hit,normal,_=cap.closest_point_on_mesh(point)
            assert ok
            if normal.z<0:normal=-normal
            return tuple(hit+normal*.018)
        verts=[project(center.copy())];faces=[]
        for ring in range(1,5):
            for side in range(24):
                angle=side*math.tau/24
                point=center+tangent*(math.cos(angle)*size*ring/4)+meridian*(math.sin(angle)*size*ring/4)
                verts.append(project(point))
        for side in range(24):faces.append((0,side+1,(side+1)%24+1))
        for ring in range(3):
            for side in range(24):
                nxt=(side+1)%24;lo=1+ring*24;hi=lo+24
                faces.append((lo+side,hi+side,hi+nxt,lo+nxt))
        obj('Cream inlay '+str(i),verts,faces,ivory)
    return [red,ivory,peach,gold,teal,ink]
def export(name,mats):
    bpy.ops.object.select_all(action='DESELECT')
    for item in parts:item.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join()
    hat=bpy.context.object;hat.name='DF_'+name
    scene=bpy.context.scene;scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    bm=bmesh.new();bm.from_mesh(hat.data)
    bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.00001)
    bmesh.ops.triangulate(bm,faces=bm.faces)
    # Remove the zero-area triangles where a closed lathe approaches its axis.
    bad=[f for f in bm.faces if f.calc_area()<1e-10]
    if bad:bmesh.ops.delete(bm,geom=bad,context='FACES_ONLY')
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(hat.data);bm.free()
    previous=list(hat.data.materials);assignments=[mats.index(previous[f.material_index]) for f in hat.data.polygons]
    hat.data.materials.clear()
    for mat in mats:hat.data.materials.append(mat)
    for face,slot in zip(hat.data.polygons,assignments):face.material_index=slot
    data={'vertices':[round(c,7) for v in hat.data.vertices for c in (v.co.x,v.co.z,-v.co.y)],
          'triangles':[i for f in hat.data.polygons for i in f.vertices],
          'submeshes':[f.material_index for f in hat.data.polygons],'materials':[]}
    for mat in mats:
        s=mat.node_tree.nodes['Principled BSDF'];rgb=s.inputs['Base Color'].default_value[:3]
        data['materials'].append(dict(zip(('r','g','b'),rgb),metallic=s.inputs['Metallic'].default_value,
            smoothness=1-s.inputs['Roughness'].default_value,emission=s.inputs['Emission Strength'].default_value))
    assert len(hat.data.vertices)<6000 and len(hat.data.polygons)<6000
    assert len(data['materials'])==6
    (resources/(name+'.json')).write_text(json.dumps(data,separators=(',',':')))
    folder=art/name;folder.mkdir(parents=True,exist_ok=True)
    hat['attachment']='Rigid headwear; origin=head-seat center; Z up / -Y front'
    hat['status']='Prototype; no price, ownership grant or production reward assigned.'
    bpy.ops.export_scene.gltf(filepath=str(folder/(name+'.glb')),export_format='GLB',use_selection=True,
        export_yup=True,export_extras=True,export_animations=False,export_cameras=False,export_lights=False)
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/(name+'.blend')),compress=True)
    print(json.dumps({'hat':name,'vertices':len(hat.data.vertices),'triangles':len(hat.data.polygons)}),flush=True)
def render(name):
    # Import the serialized runtime geometry again. This is the exact mesh Unity consumes.
    data=json.loads((resources/(name+'.json')).read_text())
    reset();v=[(data['vertices'][i],-data['vertices'][i+2],data['vertices'][i+1]) for i in range(0,len(data['vertices']),3)]
    faces=[tuple(data['triangles'][i:i+3]) for i in range(0,len(data['triangles']),3)]
    mats=[surface(name+'_'+str(i),(m['r'],m['g'],m['b']),m['metallic'],1-m['smoothness'],m['emission']) for i,m in enumerate(data['materials'])]
    hat=obj('Runtime_'+name,v,faces,mats[0])
    for mat in mats[1:]:hat.data.materials.append(mat)
    for f,index in zip(hat.data.polygons,data['submeshes']):f.material_index=index
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
    scene.render.resolution_x=768;scene.render.resolution_y=640;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.film_transparent=True
    scene.view_settings.view_transform='AgX';scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.12,.17,.25,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.35
    target=Vector((0,0,(min(p[2] for p in v)+max(p[2] for p in v))*.5))
    for label,position,energy,color,size in [
        ('Key',(-4,-5,7),850,(.86,.93,1),5),
        ('Fill',(4,-3,3),450,(1,.85,.7),4),
        ('Rim',(1,3,5),1000,(.4,.82,1),4)]:
        light=bpy.data.lights.new(label,'AREA');light.energy=energy;light.color=color;light.shape='DISK';light.size=size
        item=bpy.data.objects.new(label,light);bpy.context.collection.objects.link(item);item.location=position
        item.rotation_euler=(target-item.location).to_track_quat('-Z','Y').to_euler()
    camera_data=bpy.data.cameras.new('Studio');camera=bpy.data.objects.new('Studio',camera_data);bpy.context.collection.objects.link(camera)
    camera.location=(3,-7,3.7);camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera_data.type='ORTHO';scene.camera=camera
    rot=camera.rotation_euler.to_quaternion();points=[rot.inverted()@Vector(p) for p in v]
    lo=[min(p[i] for p in points) for i in range(2)];hi=[max(p[i] for p in points) for i in range(2)]
    camera.location=rot@Vector(((lo[0]+hi[0])/2,(lo[1]+hi[1])/2,10))
    camera_data.ortho_scale=1;frame=camera_data.view_frame(scene=scene)
    fw=max(p.x for p in frame)-min(p.x for p in frame);fh=max(p.y for p in frame)-min(p.y for p in frame)
    camera_data.ortho_scale=max((hi[0]-lo[0])/fw,(hi[1]-lo[1])/fh)/.85
    scene.render.filepath=str(resources/(name+'Icon.png'));bpy.ops.render.render(write_still=True)
if not a.render_only:
    for name,build in [('WizardHat',wizard),('PropellerCap',propeller),('MushroomHat',mushroom)]:export(name,build())
for name in ('UfoTestHat','WizardHat','PropellerCap','MushroomHat'):render(name)

