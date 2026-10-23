"""Render the actual production C# cap mesh, using Blender studio lighting.

blender --background --python ArtSource/Headwear/BetaCap/render_preview.py -- \
  --source-root . --dotnet-root /path/to/dotnet --output /tmp/BetaCapIcon.png
"""
import argparse, json, math, re, subprocess, sys, tempfile
from pathlib import Path
import bpy
from mathutils import Vector

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--source-root',type=Path,required=True)
p.add_argument('--dotnet-root',type=Path,required=True)
p.add_argument('--output',type=Path,required=True)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:])
root,sdk=a.source_root.resolve(),a.dotnet_root.resolve()
# Geometry comes directly from BetaCapMesh.Build; these substitutes capture its
# vertices/triangles and never replace the cap with an approximation.
fixture=r'''
using System;using System.Collections.Generic;using System.Linq;using System.Text.Json;
namespace UnityEngine {
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
 public struct Matrix4x4 {public Vector3 MultiplyPoint3x4(Vector3 v)=>v;}
 public static class Mathf {public const float PI=(float)Math.PI;public static float Sin(float x)=>(float)Math.Sin(x);public static float Cos(float x)=>(float)Math.Cos(x);public static float Sqrt(float x)=>(float)Math.Sqrt(x);public static float Max(float a,float b)=>Math.Max(a,b);}
 public class Mesh {public string name;public int subMeshCount;public List<Vector3> vertices;public int[][] triangles=new int[3][];public void SetVertices(List<Vector3> v){vertices=v;}public void SetTriangles(List<int> t,int i){triangles[i]=t.ToArray();}public void RecalculateNormals(){}public void RecalculateBounds(){}}
}
class Export {static void Main(){var m=Alterverse.Visuals.BetaCapMesh.Build(new UnityEngine.Matrix4x4());Console.WriteLine(JsonSerializer.Serialize(new {vertices=m.vertices.Select(v=>new[]{v.x,v.y,v.z}).ToArray(),submeshes=m.triangles}));}}
'''
with tempfile.TemporaryDirectory(prefix='df-cap-') as folder:
    tmp=Path(folder);(tmp/'Export.cs').write_text(fixture)
    compiler=sorted((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
    refs=sorted((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
    out=tmp/'Export.dll'
    args=['-nologo','-langversion:9','-target:exe','-out:'+str(out)]+['-r:'+str(f) for f in refs.glob('*.dll')]+[str(tmp/'Export.cs'),str(root/'Assets/Alterverse/Scripts/Visuals/BetaCapMesh.cs')]
    (tmp/'compile.rsp').write_text('\n'.join(args))
    subprocess.run([str(sdk/'dotnet'),'exec',str(compiler),'@'+str(tmp/'compile.rsp')],check=True)
    (tmp/'Export.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'},'rollForward':'LatestPatch'}}))
    result=subprocess.run([str(sdk/'dotnet'),str(out)],check=True,capture_output=True,text=True)
    data=json.loads(result.stdout)

vertices=[(x,-z,y) for x,y,z in data['vertices']]
faces=[];sections=[]
for section,indices in enumerate(data['submeshes']):
    assert len(indices)%3==0
    for n in range(0,len(indices),3):faces.append(tuple(indices[n:n+3]));sections.append(section)
assert len(data['submeshes'])==3 and all(0<=i<len(vertices) for face in faces for i in face)
assert all(math.isfinite(x) for v in vertices for x in v)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
mesh=bpy.data.meshes.new('Production_BetaCapMesh');mesh.from_pydata(vertices,[],faces);mesh.update()
cap=bpy.data.objects.new('BETA_CAPPY_actual_game_mesh',mesh);bpy.context.collection.objects.link(cap)
source=(root/'Assets/Alterverse/Scripts/Visuals/HeadwearPresentation.cs').read_text()
text=source.split('Color[] colors=',1)[1].split(';',1)[0]
colors=[tuple(float(x.rstrip('f')) for x in group.split(',')) for group in re.findall(r'new Color\(([^)]+)\)',text)]
assert len(colors)==3
for i,rgb in enumerate(colors):
    mat=bpy.data.materials.new(('Navy fabric','Cyan stitching','BETA lettering')[i]);mat.use_nodes=True
    surface=mat.node_tree.nodes.get('Principled BSDF');surface.inputs['Base Color'].default_value=(*rgb,1);surface.inputs['Roughness'].default_value=.82 if i==0 else .7
    mesh.materials.append(mat)
for face,section in zip(mesh.polygons,sections):face.material_index=section
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=64;scene.cycles.use_denoising=True
scene.render.resolution_x=768;scene.render.resolution_y=512;scene.render.resolution_percentage=100
scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.image_settings.color_depth='8'
scene.view_settings.view_transform='AgX';scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.15,.2,.28,1);scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.35
def light(name,position,power,color,size):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=size
    obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj);obj.location=position;obj.rotation_euler=(Vector((0,-.1,.3))-obj.location).to_track_quat('-Z','Y').to_euler()
light('Soft key',(-3,-4,5),650,(.86,.93,1),4)
light('Front fill',(3,-4,2),300,(.7,.86,1),3)
light('Cyan rim',(1,2,3),850,(.38,.8,1),3)
camera_data=bpy.data.cameras.new('Cap portrait');camera=bpy.data.objects.new('Cap portrait',camera_data);bpy.context.collection.objects.link(camera)
camera.location=(2.5,-6.5,2.6);camera.rotation_euler=(Vector((0,-.2,.3))-camera.location).to_track_quat('-Z','Y').to_euler();camera_data.type='ORTHO'
rotation=camera.rotation_euler.to_quaternion();points=[rotation.inverted()@Vector(v) for v in vertices]
mins=[min(v[i] for v in points) for i in range(2)];maxs=[max(v[i] for v in points) for i in range(2)]
camera.location=rotation@Vector(((mins[0]+maxs[0])/2,(mins[1]+maxs[1])/2,8))
camera_data.ortho_scale=1
frame=camera_data.view_frame(scene=scene)
frame_width=max(v.x for v in frame)-min(v.x for v in frame)
frame_height=max(v.y for v in frame)-min(v.y for v in frame)
camera_data.ortho_scale=max((maxs[0]-mins[0])/frame_width,(maxs[1]-mins[1])/frame_height)/.88
scene.camera=camera
a.output.parent.mkdir(parents=True,exist_ok=True);scene.render.filepath=str(a.output.resolve());bpy.ops.render.render(write_still=True)
print(json.dumps({'vertices':len(vertices),'triangles':len(faces),'materials':len(colors),'output':str(a.output.resolve())}))
