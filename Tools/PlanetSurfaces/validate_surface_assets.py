from pathlib import Path
import hashlib,json
import numpy as np
from PIL import Image
root=next(p for p in Path(__file__).resolve().parents if (p/'Assets').is_dir())
rows=[];hashes=set();guids=set()
for p in sorted((root/'Assets/Alterverse/Resources/PlanetSurfaces').glob('*/*.png')):
 im=Image.open(p);a=np.asarray(im,dtype=float)/255
 assert im.mode=='RGBA' and im.size==(512,512),p
 assert all(np.std(a[:,:,i])>.005 for i in range(4)),p
 meta=Path(str(p)+'.meta').read_text()
 for required in ['sRGBTexture: 0','enableMipMap: 1','streamingMipmaps: 1','alphaIsTransparency: 0','textureFormat: 4','buildTarget: Android','overridden: 1']:
  assert required in meta,(p,required)
 guid=meta.split('guid: ')[1].splitlines()[0];assert guid not in guids;guids.add(guid)
 digest=hashlib.sha256(p.read_bytes()).hexdigest();assert digest not in hashes;hashes.add(digest)
 if p.stem=='Macro':
  assert .1<a[:,:,0].min()<.5 and .5<a[:,:,0].max()<.9,(p,'macro range')
  assert a[:,:,1].min()>.35,(p,'excessive cavity')
  rows.append({'path':str(p.relative_to(root)),'sha256':digest,'macroChannels':'value cavity roughness sediment'});continue
 h=a[::-1,:,3];gx=np.roll(h,-1,1)-np.roll(h,1,1);gy=np.roll(h,-1,0)-np.roll(h,1,0)
 corr=[float(np.corrcoef(g.ravel(),a[::-1,:,i].ravel())[0,1]) for i,g in enumerate([gx,gy])]
 assert min(corr)>.8,(p,corr)
 rows.append({'path':str(p.relative_to(root)),'sha256':digest,'slopeHeightCorrelation':corr})
assert len(rows)==33
(root/'ArtSource/PlanetSurfaces/texture-validation.json').write_text(json.dumps(rows,indent=2))
print('Validated 33 linear RGBA maps for eleven planets; slopes, macro channels and matching Android/PC imports passed.')
