"""Export each plate module and every belt part into standalone, meter-scale Blender scenes."""
import bpy,json,math
from pathlib import Path
from mathutils import Matrix,Vector
ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'ArtSource/Heroes/BrakkArmor';OUT=ART/'models';OUT.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ART/'BrakkArmor-concept.blend'))
C=Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)));B=C.inverted()
meta=json.loads((ART/'catalogue.json').read_text());source=bpy.context.scene;index=[]

def export(name,objects,transform,role):
 scene=bpy.data.scenes.new(name);scene.world=source.world;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
 scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=False
 for layer in scene.view_layers:layer.cycles.use_denoising=False
 scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
 scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
 points=[]
 for ob in objects:
  copy=ob.copy();copy.data=ob.data.copy();copy.parent=None;copy.matrix_world=Matrix.Identity(4)
  for mod in list(copy.modifiers):copy.modifiers.remove(mod)
  copy.data.transform(transform);copy.data.update();scene.collection.objects.link(copy)
  points.extend(v.co for v in copy.data.vertices)
 low=Vector(tuple(min(p[k] for p in points) for k in range(3)));high=Vector(tuple(max(p[k] for p in points) for k in range(3)))
 centre=(low+high)*.5;size=high-low;extent=max(size)
 cameraData=bpy.data.cameras.new(name+'_Camera');camera=bpy.data.objects.new(name+'_Camera',cameraData);scene.collection.objects.link(camera)
 camera.location=centre+Vector((1.0,-1.6,1.1))*extent;camera.rotation_euler=(centre-camera.location).to_track_quat('-Z','Y').to_euler()
 cameraData.type='ORTHO';cameraData.ortho_scale=extent*1.8;scene.camera=camera
 for label,loc,power in [('Key',(1,-2,3),100),('Fill',(-2,-1,1),60)]:
  lightData=bpy.data.lights.new(name+'_'+label,'AREA');lightData.energy=power*extent*extent;lightData.size=extent*2
  light=bpy.data.objects.new(name+'_'+label,lightData);scene.collection.objects.link(light);light.location=centre+Vector(loc)*extent
  light.rotation_euler=(centre-light.location).to_track_quat('-Z','Y').to_euler()
 path=OUT/(name+'.blend');bpy.data.libraries.write(str(path),{scene},fake_user=True,compress=True)
 index.append({'file':str(path.relative_to(ART)),'role':role,'objects':[o.name for o in objects],
               'dimensionsBlenderMetres':list(size),'unitScale':1,'standalone':True})
 bpy.data.scenes.remove(scene)

for plate in meta['plates']:
 objects=[o for o in source.objects if o.get('assembly_group')==plate['id']]
 assert len(objects)==7
 transform=B@Matrix(plate['frameModelUnity']).inverted()@C
 export(plate['id']+'-'+plate['name'].replace(' ','_'),objects,transform,'armor_plate_module')
belt=[o for o in source.objects if o.get('assembly_group')=='BELT'];assert len(belt)==34
export('UtilityBelt-assembled',belt,Matrix.Translation((0,0,-1.065)),'utility_belt')
for ob in belt:
 points=[v.co for v in ob.data.vertices];centre=Vector(tuple((min(p[k] for p in points)+max(p[k] for p in points))*.5 for k in range(3)))
 export(ob['part_id'],[ob],Matrix.Translation(-centre),'belt_part')
(ART/'individual-models.json').write_text(json.dumps(index,indent=2)+'\n')
assert len(index)==53
print('INDIVIDUAL_MODELS_VALIDATED',len(index),'standalone Blender files',flush=True)
