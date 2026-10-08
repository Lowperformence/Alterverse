"""Nerok's broad shark head, in the existing +Y-up / +Z-forward rig frame.

Executed by build_cyber_shark.py with the project's geometry primitives in scope.
The supplied front-view reference informs silhouette and anatomy only.
"""
MOUTH_SOURCE_VERTICES=set()
HEAD_SOURCE_VERTICES=set()

def head_part(name,points,faces,material,mouth=False):
 start=len(V)
 add(name,points,faces,material,'Head')
 HEAD_SOURCE_VERTICES.update(range(start,len(V)))
 if mouth:MOUTH_SOURCE_VERTICES.update(range(start,len(V)))

def head_primitive(function,*args,mouth=False,**kwargs):
 start=len(V)
 function(*args,**kwargs)
 HEAD_SOURCE_VERTICES.update(range(start,len(V)))
 if mouth:MOUTH_SOURCE_VERTICES.update(range(start,len(V)))

def build_reference_shark_head():
 segments=64
 # Smooth longitudinal sections: rounded muzzle, wide cheeks and substantial skull.
 # The mouth shares its exact boundary with the outer skin and inner cavity.
 controls=np.array([
  (.171,.050,1.687,.284,.030),(.184,.076,1.687,.275,.041),
  (.212,.149,1.747,.199,.102),(.211,.193,1.757,.064,.015),
  (.172,.167,1.754,-.065,0),(.089,.098,1.754,-.120,0),
  (.018,.026,1.754,-.137,0)])
 sections=[]
 for i in range(len(controls)-1):
  p0=controls[max(0,i-1)];p1=controls[i];p2=controls[i+1];p3=controls[min(len(controls)-1,i+2)]
  # Retain the lip's thickness; sample the wider skull more densely.
  for t in np.linspace(0,1,2 if i==0 else 4,endpoint=False):
   sections.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t))
 sections.append(controls[-1])
 vertices=[];faces=[];materials=[]
 def sculpt(point):
  x,y,z=point
  forward=float(np.clip((z-.05)/.16,0,1))
  # Broad compressed brow and real concave eye sockets in the skin.
  for sign in (-1,1):
   socket=math.exp(-((x-sign*.171)/.030)**2-((y-1.834)/.029)**2)
   brow=math.exp(-((x-sign*.159)/.047)**2-((y-1.874)/.021)**2)
   z+=forward*(-.014*socket+.011*brow)
  # The lip rises at the corners without pulling the skull or nose upward.
  y+=.063*(x/.171)**2*max(0,1-abs(y-1.687)/.107)*float(np.clip((z-.215)/.067,0,1))
  return (x,y,z)
 for rx,ry,cy,z,nose in sections:
  for a in np.linspace(0,math.tau,segments,endpoint=False):
   sine=math.sin(a)
   # A rounded, wide upper muzzle replaces the old tapered pointed nose.
   width=1-.11*max(0,sine)**2 if nose>.07 else 1
   vertices.append(sculpt((rx*math.copysign(abs(math.cos(a))**.82,math.cos(a))*width,cy+ry*sine,z+nose*max(0,sine))))
 for row in range(len(sections)-1):
  for j in range(segments):
   a=row*segments+j;b=row*segments+(j+1)%segments
   faces.extend(((a,b+segments,b),(a,a+segments,b+segments)))
   materials.extend((1,1))
 back=len(vertices);vertices.append((0,1.754,-.143))
 for j in range(segments):
  faces.append(((len(sections)-1)*segments+j,back,(len(sections)-1)*segments+(j+1)%segments));materials.append(1)
 exterior_vertices=len(vertices);exterior_faces=len(faces)
 last=0
 for rx,ry,cy,z in [(.159,.044,1.687,.258),(.130,.041,1.687,.211),(.055,.027,1.689,.173)]:
  row=len(vertices)
  for a in np.linspace(0,math.tau,segments,endpoint=False):
   vertices.append(sculpt((rx*math.cos(a),cy+ry*math.sin(a),z)))
  for j in range(segments):
   a=last+j;b=last+(j+1)%segments
   faces.extend(((a,b,row+(j+1)%segments),(a,row+(j+1)%segments,row+j)));materials.extend((0,0))
  last=row
 inside=len(vertices);vertices.append((0,1.689,.164))
 for j in range(segments):faces.append((last+j,last+(j+1)%segments,inside));materials.append(0)
 start=len(V);material_start=len(MI)
 head_part('reference broad unified skull muzzle jaw and mouth',vertices,faces,1)
 MI[material_start:]=materials
 MOUTH_SOURCE_VERTICES.update(start+i for face in faces[exterior_faces:] for i in face)
 # Ray-test the sculpted skin so nostrils, eyes and scars sit on the actual surface.
 triangles=np.array([[vertices[i] for i in f] for f in faces[:exterior_faces]])
 def skin_depth(x,y):
  p=triangles[:,0];q=triangles[:,1];r=triangles[:,2]
  den=(q[:,1]-r[:,1])*(p[:,0]-r[:,0])+(r[:,0]-q[:,0])*(p[:,1]-r[:,1])
  valid=abs(den)>1e-10
  safe=np.where(valid,den,1)
  u=((q[:,1]-r[:,1])*(x-r[:,0])+(r[:,0]-q[:,0])*(y-r[:,1]))/safe
  v=((r[:,1]-p[:,1])*(x-r[:,0])+(p[:,0]-r[:,0])*(y-r[:,1]))/safe
  valid&=(u>=-1e-7)&(v>=-1e-7)&(u+v<=1+1e-7)
  if not np.any(valid):raise ValueError('Head detail outside sculpted skin: '+str((x,y)))
  return float(np.max((u*p[:,2]+v*q[:,2]+(1-u-v)*r[:,2])[valid]))
 def projected(points,offset=0):
  return [(x,y,skin_depth(x,y)+offset) for x,y in points]
 def facial_patch(name,center,rx,ry,material,slant=0,bulge=.002,offset=.001):
  cx,cy=center;segments=32;points=[]
  for scale in (1,.65,.28):
   for a in np.linspace(0,math.tau,segments,endpoint=False):
    x=cx+rx*math.cos(a)*scale;y=cy+ry*math.sin(a)*scale+slant*(x-cx)
    points.append((x,y,skin_depth(x,y)+offset+bulge*(1-scale*scale)))
  pole=len(points);points.append((cx,cy,skin_depth(cx,cy)+offset+bulge));faces=[]
  for row in range(2):
   for j in range(segments):
    k=(j+1)%segments;a=row*segments+j;b=row*segments+k
    faces.extend(((a,b,b+segments),(a,b+segments,a+segments)))
  for j in range(segments):faces.append((2*segments+j,2*segments+(j+1)%segments,pole))
  head_part(name,points,faces,material)
 for sign in (-1,1):
  x=sign*.161;y=1.834
  # An almond-shaped dark eye is fitted to the curved socket, with a sloped brow.
  facial_patch('recessed almond shark eye',(x,y),.026,.016,0,sign*.38,.007)
  facial_patch('subtle bronze shark iris',(x-sign*.006,y-.002),.007,.009,4,sign*.38,.002,.009)
  facial_patch('shark pupil',(x-sign*.006,y-.002),.004,.006,0,sign*.38,.002,.011)
  upper=[];lower=[]
  for a in np.linspace(0,math.pi,13):
   ex=x+.027*math.cos(a);ey=y+.0165*math.sin(a)+sign*.38*(ex-x)
   upper.append((ex,ey))
  for a in np.linspace(math.pi,math.tau,13):
   ex=x+.027*math.cos(a);ey=y+.0165*math.sin(a)+sign*.38*(ex-x)
   lower.append((ex,ey))
  head_primitive(tube,'integrated sloped upper eyelid',projected(upper,.001),.0045,1,'Head',8)
  head_primitive(tube,'integrated lower eyelid',projected(lower,.0005),.0025,1,'Head',8)
  nx=sign*.068;ny=1.801
  facial_patch('inlaid angled shark nostril',(nx,ny),.007,.003,0,-sign*.28,.0005)
  ridge=[(sign*.058,1.804),(sign*.066,1.807),(sign*.076,1.804)]
  head_primitive(tube,'nostril skin rim',projected(ridge,.0003),.0015,1,'Head',6)
  brow=[(sign*.107,1.850),(sign*.127,1.855),(sign*.147,1.865),(sign*.170,1.877)]
  head_primitive(tube,'compressed shark brow ridge',projected(brow,.0005),[.002,.005,.006,.001],1,'Head',8)
  for j in range(2):
   crease=[(sign*.041,1.873+j*.012),(sign*.065,1.879+j*.010),(sign*.091,1.880+j*.011),(sign*.115,1.876+j*.012)]
   head_primitive(tube,'fine forehead tension fold',projected(crease,-.0003),[.0007,.0012,.0011,.0005],1,'Head',6)
  # Curved cheek folds frame the smile, integrated at the jaw corners.
  fold=[(sign*.164,1.740),(sign*.178,1.729),(sign*.188,1.705),(sign*.184,1.682)]
  head_primitive(tube,'shark smile cheek fold',projected(fold,.0005),[.002,.004,.005,.002],1,'Head',8)
  for j in range(3):
   gx=.180+j*.009
   path=[(sign*(gx-.002),1.785-j*.009),(sign*(gx+.005),1.769-j*.008),(sign*gx,1.750-j*.007)]
   smooth_path=[]
   for t in np.linspace(0,1,9):
    point=(1-t)**2*np.array(path[0])+2*(1-t)*t*np.array(path[1])+t*t*np.array(path[2]);smooth_path.append(tuple(point))
   head_primitive(tube,'recessed cheek gill slit',projected(smooth_path,-.0005),.0014,0,'Head',6)
 # Thick ivory crowns have actual depth and a narrow cutting edge.
 for row,count in [('upper',13),('lower',11),('upper secondary',11),('lower secondary',9)]:
  secondary='secondary' in row;upper=row.startswith('upper')
  for tooth_x in np.linspace(-.155 if not secondary else -.132,.155 if not secondary else .132,count):
   rx=.164 if not secondary else .144;ry=.045 if not secondary else .039
   a=math.acos(tooth_x/rx)*(1 if upper else -1)
   base=np.array(sculpt((rx*math.cos(a),1.687+ry*math.sin(a),.280 if not secondary else .247)))
   inward=unit((-math.cos(a)*.20,-1 if upper else 1,-.10))
   tangent=unit((-inward[1],inward[0],0))
   width=(.009+.003*abs(math.sin(a)))*(.72 if secondary else 1)
   length=(.010+(.030 if upper else .025)*abs(math.sin(a)))*(.70 if secondary else 1)
   points=[]
   for t,w,d in [(0,1,.006),(.20,.97,.0065),(.72,.46,.004),(1,.025,.0005)]:
    center=base+inward*length*t
    points.extend((center-tangent*width*w+[0,0,d],center+tangent*width*w+[0,0,d],
                   center-tangent*width*w+[0,0,-d],center+tangent*width*w+[0,0,-d]))
   tooth_faces=[]
   for k in range(3):
    for q,r in [(0,1),(1,3),(3,2),(2,0)]:
     tooth_faces.extend(((4*k+q,4*k+r,4*(k+1)+r),(4*k+q,4*(k+1)+r,4*(k+1)+q)))
   tooth_faces.extend(((0,2,3),(0,3,1),(12,13,15),(12,15,14)))
   center=np.mean(points,axis=0)
   tooth_faces=[tuple(reversed(f)) if np.dot(np.cross(np.array(points[f[1]])-points[f[0]],np.array(points[f[2]])-points[f[0]]),np.mean([points[i] for i in f],axis=0)-center)<0 else f for f in tooth_faces]
   head_part(row+' layered shark tooth',points,tooth_faces,5)
 for upper in (True,False):
  angles=np.linspace(.11,math.pi-.11,35)*(1 if upper else -1)
  points=[sculpt((.165*math.cos(a),1.687+.046*math.sin(a),.267)) for a in angles]
  head_primitive(tube,'natural upper gum' if upper else 'natural lower gum',points,.0045,0,'Head',8,mouth=True)
 head_primitive(ell,'recessed shark tongue',(0,1.654,.211),(.092,.017,.026),0,'Head',24,8,mouth=True)
 # Preserve the existing healed scar, now projected onto the revised muzzle.
 scar=[(-.080,1.863),(-.060,1.872),(-.035,1.878),(-.012,1.879)]
 head_primitive(tube,'flush healed snout scar',projected(scar,-.0005),[.001,.0018,.0016,.0006],1,'Head',6)
 for j in range(3):
  sx=-.061+j*.017;sy=1.872+j*.003
  head_primitive(tube,'fine scar stitch',projected([(sx-.002,sy-.004),(sx+.002,sy+.004)],0),.0008,1,'Head',5)

def build_reference_cranial_fin():
 # A swept, thin anatomical fin; the previously enlarged height is retained.
 outline=np.array([(0,1.921,.041),(0,1.966,.015),(0,2.029,-.044),
                   (0,2.158,-.123),(0,2.126,-.141),(0,2.025,-.140),(0,1.926,-.075)])
 outline[:,1]=1.926+(outline[:,1]-1.926)*(.149/.232)
 center=np.mean(outline,axis=0);points=outline.tolist();faces=[];n=len(outline)
 for sign in (-1,1):
  ring_start=len(points)
  for p in outline:points.append((center+(p-center)*.78+np.array([sign*.010,0,0])).tolist())
  pole=len(points);points.append((center+np.array([sign*.014,0,0])).tolist())
  for j in range(n):
   k=(j+1)%n
   faces.extend(((j,k,ring_start+k),(j,ring_start+k,ring_start+j),(ring_start+j,ring_start+k,pole)))
 for i,f in enumerate(faces):
  p,q,r=np.array([points[j] for j in f]);c=(p+q+r)/3
  if np.dot(np.cross(q-p,r-p),c-center)<0:faces[i]=tuple(reversed(f))
 head_part('swept anatomical cranial fin',points,faces,1)

def configure_reference_head_materials(obj):
 """Mirror the runtime head palette and relief in the editable Blender source."""
 def material(name,color,roughness):
  mat=bpy.data.materials.new(name);mat.diffuse_color=(*color,1);mat.use_nodes=True
  bsdf=mat.node_tree.nodes.get('Principled BSDF')
  bsdf.inputs['Base Color'].default_value=(*color,1)
  bsdf.inputs['Metallic'].default_value=0
  bsdf.inputs['Roughness'].default_value=roughness
  obj.data.materials.append(mat)
  return len(obj.data.materials)-1,mat,bsdf
 skin,mat,bsdf=material('Nerok slate skin and ivory jaw',(.22,.255,.28),.46)
 nodes=mat.node_tree.nodes;links=mat.node_tree.links
 coordinates=nodes.new('ShaderNodeTexCoord')
 xyz=nodes.new('ShaderNodeSeparateXYZ');links.new(coordinates.outputs['Object'],xyz.inputs[0])
 absolute=nodes.new('ShaderNodeMath');absolute.operation='ABSOLUTE';links.new(xyz.outputs['X'],absolute.inputs[0])
 scale=nodes.new('ShaderNodeMath');scale.operation='MULTIPLY';scale.inputs[1].default_value=.12;links.new(absolute.outputs[0],scale.inputs[0])
 pigment_y=nodes.new('ShaderNodeMath');pigment_y.operation='ADD';links.new(xyz.outputs['Y'],pigment_y.inputs[0]);links.new(scale.outputs[0],pigment_y.inputs[1])
 pigment=nodes.new('ShaderNodeMapRange');pigment.clamp=True
 pigment.inputs['From Min'].default_value=1.728;pigment.inputs['From Max'].default_value=1.820
 pigment.inputs['To Min'].default_value=1;pigment.inputs['To Max'].default_value=0
 links.new(pigment_y.outputs[0],pigment.inputs['Value'])
 palette=nodes.new('ShaderNodeMixRGB');palette.blend_type='MIX'
 palette.inputs[1].default_value=(.22,.255,.28,1);palette.inputs[2].default_value=(.76,.78,.75,1)
 links.new(pigment.outputs['Result'],palette.inputs[0]);links.new(palette.outputs[0],bsdf.inputs['Base Color'])
 noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=650;noise.inputs['Detail'].default_value=2
 links.new(coordinates.outputs['Object'],noise.inputs['Vector'])
 bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.24;bump.inputs['Distance'].default_value=.0007
 links.new(noise.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs['Normal'],bsdf.inputs['Normal'])
 mouth,_,_=material('Nerok natural dark gums and mouth',(.10,.028,.032),.52)
 eye,_,_=material('Nerok wet black shark eyes',(.003,.004,.006),.14)
 iris,_,_=material('Nerok subtle bronze iris',(.12,.068,.027),.30)
 teeth,_,_=material('Nerok ivory enamel',(.87,.865,.79),.36)
 for face in obj.data.polygons:
  if not all(BI[i]==BN.index('Head') for i in face.vertices):continue
  slot=face.material_index
  if slot==0:face.material_index=mouth if all(i in MOUTH_SOURCE_VERTICES for i in face.vertices) else eye
  elif slot==1:face.material_index=skin
  elif slot==4:face.material_index=iris
  elif slot==5:face.material_index=teeth

def render_reference_head_views(scene,obj,art):
 camera=scene.camera
 original=(camera.location.copy(),camera.rotation_euler.copy(),camera.data.ortho_scale,
           scene.render.resolution_x,scene.render.resolution_y,scene.render.filepath)
 scene.render.resolution_x=960;scene.render.resolution_y=1000
 camera.data.ortho_scale=.70
 for name,position in [('head-front',(0,-3,1.85)),('head-three-quarter',(1.6,-3,1.87)),('head-side',(3,-.15,1.83))]:
  camera.location=position;aim(camera,(0,-.075,1.82))
  scene.render.filepath=str(art/(name+'.png'));bpy.ops.render.render(write_still=True)
 camera.location=original[0];camera.rotation_euler=original[1];camera.data.ortho_scale=original[2]
 scene.render.resolution_x=original[3];scene.render.resolution_y=original[4];scene.render.filepath=original[5]
