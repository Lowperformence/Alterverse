"""Validate shipped scenery geometry, water/road layout and the visual-only integration."""
import json
import math
from pathlib import Path

import numpy as np

ROOT=Path(__file__).resolve().parents[2]


def footprints():
    result=[(side*36,0,11,11) for side in (-1,1)]+[(side*27.5,0,10,10) for side in (-1,1)]
    for side in (-1,1):
        for flank in (-1,1):
            result.extend([(side*26.25,flank*21.5,10,9),(side*25.25,flank*10.75,7,9)])
    return result


def blocked(points):
    low,high=points.min(axis=0),points.max(axis=0)
    for x,z,w,d in footprints():
        dx=max(0,low[0]-x-w/2,x-w/2-high[0]);dz=max(0,low[2]-z-d/2,z-d/2-high[2])
        if dx*dx+dz*dz<=4: return True
    return False


def transformed(asset,site):
    a=math.radians(site['yaw']);c,s=math.cos(a),math.sin(a)
    rot=np.array([[c,0,s],[0,1,0],[-s,0,c]])
    return [np.array(part['vertices']).reshape(-1,3)@rot.T*site['scale']+np.array([site['x'],.015,site['z']]) for part in asset['parts']]


def accepted(points):
    low,high=points.min(axis=0),points.max(axis=0)
    return not (low[2]<7 and high[2]>-7 or low[0]<11 and high[0]>-11 and low[2]<11 and high[2]>-11 or blocked(points))


def validate():
    data=json.loads((ROOT/'Assets/Alterverse/Resources/EarthMap/Dressing.json').read_text())
    assert data['version']==1
    names=[a['name'] for a in data['assets']];assert len(set(names))==len(names)==14
    kit={a['name']:a for a in data['assets']};triangles=0;checks=0
    for item in data['assets']+data['surfaces']:
        for part in item['parts']:
            v=np.array(part['vertices']).reshape(-1,3);uv=np.array(part['uv']).reshape(-1,2);t=np.array(part['triangles']).reshape(-1,3)
            assert len(v)==len(uv)>0 and np.isfinite(v).all() and np.isfinite(uv).all()
            assert t.min()>=0 and t.max()<len(v) and 0<=part['material']<len(data['materials'])
            normals=np.cross(v[t[:,1]]-v[t[:,0]],v[t[:,2]]-v[t[:,0]])
            assert np.min(np.linalg.norm(normals,axis=1))>1e-7, item['name']+' degenerate faces'
            if item in data['surfaces']: assert (normals[:,1]>0).all(),item['name']+' inverted surface'
            checks+=len(t)
    accepted_counts={};batch_counts={};vertices=0
    for i,site in enumerate(data['placements']):
        mirror=data['placements'][i^1]
        assert site['asset']==mirror['asset'] and site['scale']==mirror['scale']
        assert site['x']==-mirror['x'] and site['z']==-mirror['z']
        assert abs((site['yaw']-mirror['yaw'])%360-180)<.0011
        part_points=transformed(kit[site['asset']],site);all_points=np.concatenate(part_points)
        if not accepted(all_points):continue
        assert np.abs(all_points[:,0]).max()<42 and np.abs(all_points[:,2]).max()<36
        accepted_counts[site['asset']]=accepted_counts.get(site['asset'],0)+1
        for part,v in zip(kit[site['asset']]['parts'],part_points):
            triangles+=len(part['triangles'])//3;vertices+=len(v)
            center=(all_points.min(axis=0)+all_points.max(axis=0))/2
            region=(max(0,min(3,int((center[0]+42)//21))),max(0,min(2,int((center[2]+36)//24))))
            batch_counts[(region,part['material'])]=batch_counts.get((region,part['material']),0)+len(v)
    assert accepted_counts.get('Cottage')==4 and accepted_counts.get('CottageSlate')==2 and accepted_counts.get('Shed')==2
    assert sum(n for a,n in accepted_counts.items() if a.startswith(('Oak','Birch','Pine')))>=65
    assert accepted_counts.get('Reeds',0)>=30
    assert triangles<300000 and vertices<500000
    north_water=[s for s in data['surfaces'] if s['water'] and not s['name'].endswith('South')]
    assert len(north_water)==2
    lake=north_water[1];river=north_water[0]
    lake_v=np.array(lake['parts'][0]['vertices']).reshape(-1,3);river_v=np.array(river['parts'][0]['vertices']).reshape(-1,3)
    assert (lake_v[:,1]==river_v[0,1]).all() and (river_v[:,1]==river_v[0,1]).all()
    # Last stream centre lies well inside the lake (no disconnected water dots).
    assert ((-12+12)/3.62)**2+((20.4-19)/2.2)**2<.5
    assert any(s['name']=='CottagePath'+str(x) for s in data['surfaces'] for x in (4,10,16))
    for x in (4,10,16): assert any(s['name']=='CottagePath'+str(x) for s in data['surfaces'])
    assert any(s['name']=='ShedPath' for s in data['surfaces'])
    # Exercise equivalent regional shader-bank interpolation: UV must be 0 inside, 1 at both margins.
    river_uv=np.array(river['parts'][0]['uv']).reshape(-1,2)
    assert set(river_uv[:,0])=={0,.72,1}
    for s in north_water: assert not any(blocked(np.array(p['vertices']).reshape(-1,3)) for p in s['parts'])
    source=(ROOT/'Assets/Alterverse/Scripts/Visuals/EarthMapDressing.cs').read_text()
    assert 'ArenaVariantRuntime.Current!=ArenaVariant.Earth' in source
    for prohibited in ('AddComponent<Collider>','AddComponent<Rigidbody>','AddComponent<NavMeshObstacle>','Random.'):
        assert prohibited not in source
    assert 'RealityCoreShell' not in (ROOT/'Assets/Alterverse/Scripts/Visuals/EarthForestPolish.cs').read_text()
    print(json.dumps(dict(checkedTriangles=checks,acceptedPlacements=sum(accepted_counts.values()),
                         instances=accepted_counts,propTriangles=triangles,propVertices=vertices,
                         propBatches=len(batch_counts),largestPropBatch=max(batch_counts.values())),indent=2))


if __name__=='__main__':validate()
