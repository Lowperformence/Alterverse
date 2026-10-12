"""Buildable surface detail and exact model-derived part plans. Metres, game +Y up."""
from pathlib import Path
import json, math, html
import numpy as np

def optimize_parts(label,p,names,anchors,vertices,bones,groups=None):
    v=np.asarray(vertices).reshape(-1,3);bi=np.asarray(bones);g=np.zeros(len(v),int) if groups is None else np.asarray(groups)
    metal={'Sivra':2,'Ione':2,'Auren':2,'Traveler':2,'Nerok':4,'Tahla':2,'Zhaev':2,'Brakk':4,'Timmy':2}[label]
    shell={'Sivra':1,'Ione':2,'Auren':1,'Traveler':1,'Nerok':2,'Tahla':1,'Zhaev':1,'Brakk':2,'Timmy':1}[label]
    dark=0;light=3
    def points(name):return v[(bi==names.index(name))&(g==0)]
    def face_z(x,y,name,back=False):
        q=points(name);near=q[(abs(q[:,0]-x)<.04)&(abs(q[:,1]-y)<.06)]
        if len(near)==0:near=q
        return float((near[:,2].min()-.006) if back else (near[:,2].max()+.006))
    def bolt(name,c,bone,mat=metal):
        p.ellipsoid(name+' countersunk fastener',c,[.0033,.0033,.0016],mat,bone,8,5)
    # Form separate longitudinal guards; keep the first/last 22% of each joint span clear.
    for side in ['L','R']:
        for segment,next_segment in [('UpperArm','Forearm'),('Forearm','Hand'),('Thigh','Shin'),('Shin','Foot')]:
            name=segment+side;bone=names.index(name);a=np.array(anchors[bone]['head']);b=np.array(anchors[names.index(next_segment+side)]['head']);axis=b-a
            q=points(name)
            if len(q)<6:continue
            length=np.linalg.norm(axis)
            if length<.05:continue
            width=min(float(np.ptp(q[:,0]))*.23,.065)
            if label=='Brakk':width=min(width,.09)
            c=(a+b)*.5;upper=a+axis*.24;lower=a+axis*.76
            z=face_z(c[0],c[1],name)
            # Leaf profiles match Tahla; industrial stepped faces match the mechanical heroes.
            outline=[(upper[0]-width*.55,upper[1]),(upper[0]+width*.55,upper[1]),(c[0]+width,c[1]+length*.09),(lower[0]+width*.55,lower[1]),(lower[0]-width*.55,lower[1]),(c[0]-width,c[1]+length*.09)]
            guard_material=0 if label=='Nerok' and segment!='Thigh' else shell
            p.plate(label+' '+name+' separate formed guard',outline,z,.008,guard_material,bone)
            p.plate(label+' '+name+' inset service panel',[(c[0]-width*.38,c[1]+length*.15),(c[0]+width*.38,c[1]+length*.15),(c[0]+width*.34,c[1]-length*.13),(c[0]-width*.34,c[1]-length*.13)],z+.009,.004,dark,bone)
            p.tube(label+' '+name+' protected luminous channel',[[c[0],c[1]+length*.12,z+.015],[c[0],c[1]-length*.10,z+.015]],.0015,light,bone,6)
            for dx in [-width*.62,width*.62]:bolt(label+' '+name,[c[0]+dx,c[1],z+.014],bone)
            for dy in [-length*.10,0,length*.10]:
                p.tube(label+' '+name+' inset vent',[[c[0]-width*.20,c[1]+dy,z+.016],[c[0]+width*.20,c[1]+dy,z+.016]],.0009,metal,bone,5)
    chest=names.index('Chest');cy=float(anchors[chest]['head'][1]);q=points('Chest');w=min(float(np.ptp(q[:,0]))*.25,.105)
    # Two rear service shells with a central spine clearance, not one rigid torso bridge.
    for sign in [-1,1]:
        x=sign*w;z=face_z(x,cy+.095,'Chest',True)
        p.plate(label+' back service shell',[(x-w*.65,cy+.205),(x+w*.65,cy+.205),(x+w*.54,cy+.035),(x,cy+.005),(x-w*.54,cy+.035)],z,-.009,shell,chest)
        for j in range(3):p.tube(label+' back exhaust vent',[[x-w*.35,cy+.145-j*.018,z-.012],[x+w*.35,cy+.145-j*.018,z-.012]],.0018,dark,chest,6)
        for y in [cy+.185,cy+.045]:bolt(label+' rear panel',[x,y,z-.014],chest)
    if label in ('Sivra','Ione','Auren','Traveler','Zhaev'):
        head=names.index('Head');q=points('Head');lo=q.min(0);hi=q.max(0);c=(lo+hi)*.5
        # Low-profile occipital cassette, with no change to face identity or helmet envelope.
        z=float(lo[2]-.006);hw=min(float(hi[0]-lo[0])*.20,.047);hh=float(hi[1]-lo[1])*.19
        p.plate(label+' occipital helmet cassette',[(c[0]-hw,c[1]+hh),(c[0]+hw,c[1]+hh),(c[0]+hw,c[1]-hh),(c[0],c[1]-hh*1.25),(c[0]-hw,c[1]-hh)],z,-.007,shell,head)
        for j in range(3):p.tube(label+' helmet cooling slot',[[-hw*.62,c[1]+hh*.5-j*.014,z-.011],[hw*.62,c[1]+hh*.5-j*.014,z-.011]],.0014,dark,head,6)
        for dx in [-hw*.7,hw*.7]:bolt(label+' helmet',[dx,c[1]-hh*.6,z-.013],head)
    elif label=='Nerok':
        # Genuine cloth reinforcement, no new metal mask covering shark anatomy.
        for sign in [-1,1]:
            for j in range(6):
                x=sign*.18;y=cy+.05+j*.018;z=face_z(x,y,'Chest')+.01
                p.tube('Nerok jacket lock stitch',[[x-.003,y-.003,z],[x+.003,y+.003,z]],.0008,metal,chest,5)
    return p

def category(bone):
    if bone.startswith('Weapon'):return 'Weapons'
    if bone=='Head':return 'Head_Helmet'
    if bone.startswith(('UpperArm','Forearm','Hand')):return 'Arm_'+bone[-1]
    if bone.startswith(('Thigh','Shin','Foot')):return 'Leg_'+bone[-1]
    if bone=='Tail':return 'Tail'
    return 'Torso_Armor'

def optimize_weapon_parts(label,p,names,vertices,bones,groups,faces):
    """Place shallow fittings INSIDE actual surface triangles, never on bounding-box planes."""
    v=np.asarray(vertices).reshape(-1,3);bi=np.asarray(bones);groups=np.asarray(groups);faces=np.asarray(faces).reshape(-1,3);p.weapon_ranges=[]
    metal=4 if label=='Nerok' else 2;light=3
    for group in sorted(set(groups)-{0}):
        selected=faces[(groups[faces]==group).all(1)]
        if not len(selected):continue
        tri=v[selected];cross=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);area=np.linalg.norm(cross,axis=1)
        order=np.argsort(area)[::-1];used=[]
        for i in order:
            if area[i]<.0001:break
            center=tri[i].mean(0)
            if any(np.linalg.norm(center-c)<.045 for c in used):continue
            used.append(center);normal=cross[i]/area[i];bone=int(np.bincount(bi[selected[i]]).argmax())
            edge=tri[i,1]-tri[i,0];edge/=max(np.linalg.norm(edge),1e-9)
            radius=min(.014,math.sqrt(area[i])*.07)
            a=center-edge*radius+normal*.0018;b=center+edge*radius+normal*.0018
            first=len(p.v);p.tube(label+' weapon seated surface fitting '+str(group)+'-'+str(len(used)),[a,b],.0012,metal if len(used)!=2 else light,bone,6);p.weapon_ranges.append((first,len(p.v),int(group)))
            if len(used)==3:break

def optimize_native_weapons(label,arm):
    import bpy
    from build_parts import Parts
    from blender_export import mesh_object,material
    results=[]
    for weapon in list(bpy.context.scene.objects):
        if weapon.type!='MESH' or not weapon.name.startswith('DF_Weapon'):continue
        v=np.array([tuple(a.co) for a in weapon.data.vertices]);p=Parts();names=[g.name for g in weapon.vertex_groups]
        dominant=max(((sum(g.weight for a in weapon.data.vertices for g in a.groups if g.group==i),i) for i in range(len(names))),default=(0,0))[1]
        weapon.data.calc_loop_triangles();faces=[list(t.vertices) for t in weapon.data.loop_triangles]
        optimize_weapon_parts(label,p,['Weapon'],v,[0]*len(v),[1]*len(v),faces)
        if not p.v:continue
        palette=[(.025,.035,.05),(.15,.18,.20),(.5,.55,.56),(.02,.65,.36),(.48,.29,.12)]
        if label=='Zhaev':palette[3]=(.54,.075,.83)
        if label=='Brakk':palette[3]=(1,.38,.05)
        if label=='Tahla':palette[3]=(.18,.75,.37)
        if label=='Timmy':palette[3]=(.025,.61,.95)
        mats=[material(label+' weapon authored '+str(i),color,.5,.38,i==3) for i,color in enumerate(palette)]
        obj=mesh_object('AuthoredWeaponServiceDetails',p.v,p.f,p.m,mats,p.normals().tolist());obj.matrix_world=weapon.matrix_world.copy()
        if names:obj.vertex_groups.new(name=names[dominant]).add(list(range(len(p.v))),1,'REPLACE')
        bpy.ops.object.select_all(action='DESELECT');weapon.select_set(True);obj.select_set(True);bpy.context.view_layer.objects.active=weapon;bpy.ops.object.join()
        results.append({'object':weapon.name,'addedVertices':len(p.v),'addedTriangles':len(p.f),'originalVerticesExact':all(tuple(a.co)==tuple(b) for a,b in zip(weapon.data.vertices,v))})
        assert results[-1]['originalVerticesExact']
    return results

def build_plans(label,body,arm,directory):
    """Split actual rest geometry into editable parts and draw exact orthographic projections."""
    import bpy
    from mathutils import Vector
    directory=Path(directory);directory.mkdir(parents=True,exist_ok=True)
    parts={};sources=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('DF_')]
    for obj in sources:
        mesh=obj.data;mesh.calc_loop_triangles();world=np.array([tuple(obj.matrix_world@v.co) for v in mesh.vertices]);assigned=[]
        for vertex in mesh.vertices:
            weights=[(g.weight,obj.vertex_groups[g.group].name) for g in vertex.groups if obj.vertex_groups[g.group].name in arm.data.bones]
            bone=max(weights)[1] if weights else 'Chest'
            assigned.append('Weapons' if obj.name.startswith('DF_Weapon') else category(bone))
        for tri in mesh.loop_triangles:
            ids=list(tri.vertices);cats=[assigned[i] for i in ids];cat=max(set(cats),key=lambda k:(cats.count(k),k))
            material_name=mesh.materials[tri.material_index].name if len(mesh.materials)>tri.material_index else ''
            if label=='Brakk' and all(obj.vertex_groups[g.group].name.startswith('Hand') for i in ids for g in mesh.vertices[i].groups if g.weight>.5):cat='Weapons'
            if label=='Timmy' and cat.startswith('Arm') and ('Energy' in material_name or 'claw' in material_name.lower()):cat='Weapons'
            entry=parts.setdefault(cat,{'v':[],'f':[],'m':[],'materials':[]})
            first=len(entry['v']);entry['v'].extend(world[ids].tolist());entry['f'].append([first,first+1,first+2]);mat=mesh.materials[tri.material_index] if len(mesh.materials)>tri.material_index else None
            if mat not in entry['materials']:entry['materials'].append(mat)
            entry['m'].append(entry['materials'].index(mat))
    made=[];records=[]
    for cat,e in parts.items():
        v=np.array(e['v']);f=np.array(e['f']);unique,inverse=np.unique(np.round(v,8),axis=0,return_inverse=True);faces=inverse[f]
        me=bpy.data.meshes.new(label+'_'+cat);me.from_pydata(unique.tolist(),[],faces.tolist());me.update()
        for mat in e['materials']:
            if mat:me.materials.append(mat)
        me.polygons.foreach_set('material_index',e['m']);o=bpy.data.objects.new('PLAN_'+cat,me);bpy.context.collection.objects.link(o);made.append(o)
        o['component']=cat;o['units']='metres';o['source']='Actual authored rest mesh; extracted by dominant bone ownership.'
        bounds=[unique.min(0).tolist(),unique.max(0).tolist()];records.append({'part':cat,'vertices':len(unique),'triangles':len(faces),'boundsBlenderMetres':bounds,'dimensionsMm':((unique.max(0)-unique.min(0))*1000).round(2).tolist()})
        obj_lines=['# Actual rest-mesh part. Blender coordinates: X right, Y back, Z up; metres.']+['v '+' '.join(f'{c:.8f}' for c in a) for a in unique]+['f '+' '.join(str(int(i)+1) for i in t) for t in faces]
        (directory/(cat+'.obj')).write_text('\n'.join(obj_lines)+'\n')
        draw_svg(label,cat,unique,faces,directory/(cat+'-orthographic.svg'))
    for obj in sources:obj.hide_set(True);obj.hide_render=True
    bpy.ops.object.select_all(action='DESELECT')
    for obj in made:obj.select_set(True)
    if made:bpy.context.view_layer.objects.active=made[0]
    bpy.ops.wm.save_as_mainfile(filepath=str(directory/(label+'-parts.blend')),compress=True)
    for o in made:
        cat=o['component'];offset={'Head_Helmet':(0,0,.28),'Arm_L':(-.35,0,0),'Arm_R':(.35,0,0),'Leg_L':(-.18,0,-.20),'Leg_R':(.18,0,-.20),'Weapons':(.6,.25,.15),'Tail':(0,.35,0)}.get(cat,(0,0,0));o.location=offset
    bpy.ops.wm.save_as_mainfile(filepath=str(directory/(label+'-exploded.blend')),compress=True)
    for obj in sources:obj.hide_set(False);obj.hide_render=False
    for obj in made:bpy.data.objects.remove(obj,do_unlink=True)
    report={'hero':label,'units':'metres','coordinateSystem':'Blender X right, Y back, Z up; game X right, Y up, Z forward','derivation':'Actual rest-pose triangles; boundary faces assigned to dominant bone category. Assembly overlaps intentional; OBJ components are surface meshes, not manufacturing solids.','components':records,'checks':{'sourceTriangles':sum(len(o.data.loop_triangles) for o in sources),'partTriangles':sum(r['triangles'] for r in records)}}
    assert report['checks']['sourceTriangles']==report['checks']['partTriangles']
    (directory/'assembly.json').write_text(json.dumps(report,indent=2)+'\n')
    return report

def draw_svg(label,cat,v,f,path):
    width=1200;height=620;out=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">','<rect width="1200" height="620" fill="#f5f3ed"/>',f'<text x="30" y="36" font-family="sans-serif" font-size="24">{html.escape(label+" / "+cat+" / actual rest-mesh plan")}</text>']
    # Feature edges preserve physical seams without displaying every triangulation diagonal.
    edges={};normals=np.cross(v[f[:,1]]-v[f[:,0]],v[f[:,2]]-v[f[:,0]]);lens=np.linalg.norm(normals,axis=1);normals/=np.maximum(lens[:,None],1e-12)
    for i,t in enumerate(f):
        for a,b in [(t[0],t[1]),(t[1],t[2]),(t[2],t[0])]:edges.setdefault(tuple(sorted((int(a),int(b)))),[]).append(i)
    features=[edge for edge,indices in edges.items() if len(indices)!=2 or abs(float(np.dot(normals[indices[0]],normals[indices[1]])))<.91]
    for panel,(axes,sign,title) in enumerate([((0,2),1,'FRONT'),((1,2),-1,'SIDE'),((0,2),-1,'BACK')]):
        q=v[:,axes].copy();q[:,0]*=sign;span=np.ptp(q,axis=0);scale=min(335/max(span[0],.001),435/max(span[1],.001));center=(q.min(0)+q.max(0))*.5;q=(q-center)*[scale,-scale]+[panel*400+200,300]
        out.append(f'<text x="{panel*400+30}" y="75" font-family="sans-serif" font-size="18">{title}</text>')
        paths=' '.join(f'M{q[a,0]:.1f},{q[a,1]:.1f}L{q[b,0]:.1f},{q[b,1]:.1f}' for a,b in features)
        out.append(f'<path d="{paths}" fill="none" stroke="#263b4b" stroke-width=".6" opacity=".65"/>')
        out.append(f'<text x="{panel*400+30}" y="550" font-family="sans-serif" font-size="14">Width {span[0]*1000:.2f} mm / Height {span[1]*1000:.2f} mm</text>')
    out.append('<text x="30" y="590" font-family="sans-serif" font-size="13">Measured authored geometry. Orthographic projections include rear edges. No perspective. Units: mm. Fit rig before assembly.</text></svg>');path.write_text('\n'.join(out)+'\n')
