"""Collect real file metadata and original-source comparisons, without licensing guesses."""
from pathlib import Path
import hashlib,json,re,subprocess
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Docs/HeroAssets';OUT.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
images=[]
for sub in ['CombatUI','CyberShark','AndromedaTimmy','HeroArt','AuthoredHeroes/TimmyTextures']:
    for p in (ROOT/'Assets/Alterverse/Resources'/sub).rglob('*.png'):
        raw=p.read_bytes()
        with Image.open(p) as im:
            meta={k:v for k,v in im.info.items() if isinstance(v,(str,int,float))}
            images.append({'path':str(p.relative_to(ROOT)),'sha256':sha(p),
                'size':im.size,'metadata':meta,'c2paMarkerPresent':b'c2pa' in raw,
                'openaiMarkerPresent':b'OpenAI' in raw or b'openai' in raw,
                'embeddedAssertionSnippets':[m.group().decode('utf-8','replace')[:300]
                    for m in re.finditer(rb'(?:c2pa|OpenAI|openai)[\x20-\x7e]{0,200}',raw)][:15],
                'signatureVerified':False})

def decode(p):
    raw=subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-f','f32le','-ac','1','-ar','22050','-'])
    return np.frombuffer(raw,dtype='<f4').astype(np.float64)

originals=[]
for p in (ROOT/'ArtSource/Audio/HeroSelections/Originals').glob('*'):
    if p.suffix.lower() in ['.mp3','.wav','.ogg']:
        originals.append((p,decode(p)))
comparisons=[]
for p in list((ROOT/'Assets/Alterverse/Resources/Audio/Heroes/Timmy').glob('*.mp3'))+list((ROOT/'Assets/Alterverse/Resources/Audio/Heroes/Nerok').glob('*.mp3')):
    target=decode(p);best=[]
    for source,pcm in originals:
        if abs(len(pcm)-len(target))>22050*.1:continue
        count=min(len(pcm),len(target))
        if count<100:continue
        # Alignment is used only to compare decoded content, never to infer a publisher.
        corr=float(np.corrcoef(pcm[:count],target[:count])[0,1])
        best.append({'source':str(source.relative_to(ROOT)),'sourceSha256':sha(source),
            'correlation':corr,'maxSampleDelta':float(np.max(np.abs(pcm[:count]-target[:count]))),
            'sameBytes':p.read_bytes()==source.read_bytes(),'framesDelta':len(target)-len(pcm)})
    comparisons.append({'path':str(p.relative_to(ROOT)),'sha256':sha(p),
        'frames':len(target),'bestCandidates':sorted(best,key=lambda x:-x['correlation'])[:3]})
resources=[]
for sub in ['Visuals','UI','Audio','Shop']:
    for p in (ROOT/'Assets/Alterverse/Scripts'/sub).glob('*.cs'):
        text=p.read_text()
        refs=re.findall(r'Resources.Load(?:<[^>]+>)?\(\s*"([^"]+)"',text)
        if refs:resources.append({'path':str(p.relative_to(ROOT)),'refs':refs})
report={'schemaVersion':1,'images':images,'audioComparisons':comparisons,'literalResources':resources}
(OUT/'Inspection.json').write_text(json.dumps(report,indent=2)+'\n')
print('Inspected',len(images),'images and',len(comparisons),'direct MP3 cues.')
