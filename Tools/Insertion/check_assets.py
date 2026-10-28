"""Static dependency/import checks; this is not a native Unity import or rendering test."""
from pathlib import Path
import re,json
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
art=ROOT/'Assets/Alterverse/Art/InsertionR35';resource=ROOT/'Assets/Alterverse/Resources/InsertionTransport'
report=json.loads((ROOT/'Tools/Insertion/r35-report.json').read_text())
guids={}
for p in [*art.glob('*.meta'),*resource.glob('*.meta')]:
 guid=re.search(r'^guid: ([0-9a-f]{32})$',p.read_text(),re.M).group(1)
 assert guid not in guids,('duplicate guid',p)
 guids[guid]=p
prefab=(resource/'R35.prefab').read_text()
assert 'm_PrefabInternal:' not in prefab and '--- !u!1001 ' not in prefab
assert not re.search(r'--- !u!(?:114|65|54|64|136|135) ',prefab),'R35 must be presentation only'
assert len(re.findall(r'--- !u!33 ',prefab))==9
assert len(re.findall(r'guid: 72696c59f2331984ba67d602cf17c997, type: 3',prefab))==9
for block in re.findall(r'--- !u!1 .*?(?=--- !u!|\Z)',prefab,re.S):
 if re.search(r'm_Name: Dropship_R35-(Laser|Landing)',block):assert 'm_IsActive: 0' in block
assert not re.findall(r'\{fileID: 100100000\}',prefab)
for name in ['Dropship_Hatch','Dropship_R35-Engine1','Dropship_R35-Engine2']:assert 'm_Name: '+name in prefab
external={'72696c59f2331984ba67d602cf17c997','933532a4fcc9baf4fa0491de14d08ed7'}
for p in [resource/'R35.prefab',resource/'R35_Zero.mat',resource/'R35_Lumen.mat']:
 for g in re.findall(r'guid: ([0-9a-f]{32})',p.read_text()):assert g in guids or g in external,(p,g)
for p in resource.glob('*.mat'):
 s=p.read_text();assert 'guid: 933532a4fcc9baf4fa0491de14d08ed7' in s
 for name in ['_BaseMap','_BumpMap','_EmissionMap','_MetallicGlossMap']:assert name+':' in s
 for keyword in ['_NORMALMAP','_METALLICSPECGLOSSMAP','_EMISSION']:assert keyword in s
for name,size in report['texture_sizes'].items():
 p=art/(name+'.png');assert Image.open(p).size==(size,size)
 meta=Path(str(p)+'.meta').read_text()
 for required in ['enableMipMap: 1','isReadable: 0','buildTarget: Android','textureFormat: 50']:assert required in meta
 if name in ['Normal','MetallicSmoothness']:assert 'sRGBTexture: 0' in meta
 if name=='Normal':assert 'textureType: 1' in meta
print('PASS: R35 prefab/material/texture dependencies, original FBX references, URP maps, team materials and mobile texture settings.')
