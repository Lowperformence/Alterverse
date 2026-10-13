import bpy,json
from pathlib import Path
from mathutils.bvhtree import BVHTree
bpy.ops.wm.open_mainfile(filepath=str(Path(__file__).resolve().parents[2]/'ArtSource/Heroes/BrakkArmor/BrakkArmor-concept.blend'))
body=bpy.data.objects['DF_Anatomy'];body.data.calc_loop_triangles()
verts=[body.matrix_world@v.co for v in body.data.vertices]
tris=[list(t.vertices) for t in body.data.loop_triangles]
tree=BVHTree.FromPolygons(verts,tris,all_triangles=True)
result={}
for ob in bpy.data.objects:
 if not ob.name.startswith('BRK_') or ob.get('assembly_group')=='BELT' or ob.get('layer')==4:continue
 ob.data.calc_loop_triangles();v=[ob.matrix_world@p.co for p in ob.data.vertices];t=[list(p.vertices) for p in ob.data.loop_triangles]
 other=BVHTree.FromPolygons(v,t,all_triangles=True)
 overlaps=tree.overlap(other)
 if overlaps:result[ob.name]=len(overlaps)
print('PLATE_BODY_INTERSECTIONS',json.dumps(result),flush=True)
assert not result, result
