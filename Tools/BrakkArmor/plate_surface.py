"""Closed layered plates clipped to one shared contour triangulation per module.

Every layer uses the SAME surface triangles. Independently triangulated outlines
span different chords through a curved surface and can bury the smaller inset.
"""
import numpy as np
from scipy.spatial import Delaunay, ConvexHull
from scipy.interpolate import RBFInterpolator

REVISION = 'layered-height-field-v2'
GRID_STEP = .04


class HeightField:
    def __init__(self, samples):
        self.samples = np.asarray(samples, dtype=float)
        self.xy = self.samples[:, :2]
        self.tri = Delaunay(self.xy)
        self.hull = ConvexHull(self.xy).vertices
        self.smooth = RBFInterpolator(self.xy, self.samples[:, 2], kernel='thin_plate_spline', smoothing=1e-4)

    def __call__(self, point):
        p = np.asarray(point, dtype=float)
        if self.tri.find_simplex(p, tol=1e-8) >= 0:
            return float(self.smooth(p.reshape(1, 2))[0])
        # Continuous extension from the nearest hull edge: preserve the metal
        # footprint instead of collapsing an overhanging corner onto one vertex.
        best = None
        for i, a in enumerate(self.hull):
            b = self.hull[(i + 1) % len(self.hull)]
            edge = self.xy[b] - self.xy[a]
            t = float(np.clip((p - self.xy[a]) @ edge / (edge @ edge), 0, 1))
            position = self.xy[a] + edge * t
            distance = float(np.sum((p-position)**2))
            if best is None or distance < best[0]: best = distance, position
        return float(self.smooth(best[1].reshape(1, 2))[0])


def clip(polygon, triangle):
    """Clip a 3D face against a vertical triangular prism, preserving winding.

    This includes bevels and vertical side walls, so all cut edges coincide and
    the exported solids remain closed rather than becoming disconnected caps.
    """
    result = polygon
    for a,b in zip(triangle, np.roll(triangle,-1,axis=0)):
        edge = b-a
        def distance(p): return edge[0]*(p[1]-a[1])-edge[1]*(p[0]-a[0])
        source = result; result = []
        if not source: break
        previous = source[-1]; dp = distance(previous)
        for current in source:
            dc = distance(current)
            if (dc >= -1e-12) != (dp >= -1e-12):
                result.append(previous+(current-previous)*(dp/(dp-dc)))
            if dc >= -1e-12: result.append(current)
            previous,dp = current,dc
        unique = []
        for p in result:
            if not unique or np.linalg.norm(p-unique[-1]) > 1e-10: unique.append(p)
        if len(unique)>1 and np.linalg.norm(unique[0]-unique[-1]) < 1e-10: unique.pop()
        result = unique
    return result


def surface_cells(diagram, field):
    points = np.vstack([p['vertices'] for p in diagram['parts']])[:,:2]
    low = points.min(axis=0)-.001; high=points.max(axis=0)+.001
    axes = [np.linspace(low[i],high[i],int(np.ceil((high[i]-low[i])/GRID_STEP))+1) for i in range(2)]
    grid = np.array([(x,y) for x in axes[0] for y in axes[1]])
    heights = np.array([field(p) for p in grid]); cells=[]; gradients=[[] for _ in grid]
    for indices in Delaunay(grid).simplices:
        xy=grid[indices]
        cross=(xy[1,0]-xy[0,0])*(xy[2,1]-xy[0,1])-(xy[1,1]-xy[0,1])*(xy[2,0]-xy[0,0])
        if cross<0: indices=indices[::-1];xy=grid[indices]
        plane=np.linalg.solve(np.c_[xy,np.ones(3)],heights[indices])
        for index in indices:gradients[index].append(plane[:2])
        cells.append((indices,xy,plane))
    gradient=np.array([np.mean(g,axis=0) for g in gradients])
    return [(xy,plane,np.linalg.solve(np.c_[xy,np.ones(3)],gradient[indices]),xy.min(axis=0),xy.max(axis=0)) for indices,xy,plane in cells]


def rebuild(data, diagrams, profiles):
    vertices,normals,uv,bones,parts=[],[],[],[],[]
    submeshes=[{'triangles':[]} for _ in data['materials']]
    for diagram in sorted(diagrams,key=lambda d:d['plate']['id']):
        plate=diagram['plate'];frame=np.asarray(plate['frameModelUnity'],dtype=float)
        cells=surface_cells(diagram,HeightField(profiles[plate['id']]))
        for source in sorted(diagram['parts'],key=lambda p:p['id']):
            source_points=np.asarray(source['vertices'],dtype=float)
            first=len(vertices)//3;cache={};seen_triangles=set()
            for face in source['faces']:
                polygon=[source_points[i] for i in face]
                source_normal=sum((np.cross(polygon[i]-polygon[0],polygon[i+1]-polygon[0]) for i in range(1,len(polygon)-1)),np.zeros(3))
                if np.linalg.norm(source_normal)<1e-12:continue
                source_normal/=np.linalg.norm(source_normal)
                low=np.min(polygon,axis=0)[:2];high=np.max(polygon,axis=0)[:2]
                for xy,plane,gradient,cell_low,cell_high in cells:
                    if np.any(high<cell_low-1e-10) or np.any(low>cell_high+1e-10):continue
                    cut=clip(polygon,xy)
                    if len(cut)<3:continue
                    bent=[frame[:3,:3]@np.array([p[0],p[1],plane@[p[0],p[1],1]+.019+p[2]])+frame[:3,3] for p in cut]
                    shaded=[]
                    for point in cut:
                        slope=np.array([point[0],point[1],1])@gradient
                        n=frame[:3,:3]@np.array([source_normal[0]-slope[0]*source_normal[2],source_normal[1]-slope[1]*source_normal[2],source_normal[2]])
                        shaded.append(n/np.linalg.norm(n))
                    for j in range(1,len(bent)-1):
                        triangle=[bent[0],bent[j],bent[j+1]]
                        normal=np.cross(triangle[1]-triangle[0],triangle[2]-triangle[0]);length=np.linalg.norm(normal)
                        unity=np.asarray(np.round(triangle,8),dtype=np.float32)
                        if length<2e-11 or np.linalg.norm(np.cross(unity[1]-unity[0],unity[2]-unity[0]))<2e-11:continue
                        triangle_key=tuple(sorted(tuple(p) for p in np.round(triangle,8)))
                        if triangle_key in seen_triangles:continue
                        seen_triangles.add(triangle_key)
                        indices=[]
                        for point,normal in zip(triangle,[shaded[0],shaded[j],shaded[j+1]]):
                            key=tuple(np.round(point,8))+tuple(np.round(normal,5))
                            if key not in cache:
                                cache[key]=len(vertices)//3
                                vertices.extend(np.round(point,8).tolist());normals.extend(np.round(normal,8).tolist())
                                uv.extend([round(float(point[0]*4),6),round(float(point[1]*4),6)])
                                bones.append(data['boneNames'].index(plate['bone']))
                            indices.append(cache[key])
                        submeshes[source['material']]['triangles'].extend(indices)
            parts.append({'id':source['id'],'bone':plate['bone'],'material':source['material'],'firstVertex':first,'vertexCount':len(vertices)//3-first})
    result=dict(data)
    result.update(vertices=vertices,normals=normals,uv=uv,boneIndices=bones,parts=parts,submeshes=submeshes,
                  surfaceFit=REVISION,revision='brakk-armor-runtime-20261016',sharedSurfaceGridMetres=GRID_STEP)
    return result
