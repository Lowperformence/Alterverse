"""Replace nine untraceable recordings with reproducible licensed-source mixes.

Only the immutable baseline is used for timing/level comparison. Its audio is
never incorporated into a replacement. Existing resource paths and GUIDs stay.
Requires numpy and ffmpeg; run from a full git checkout.
"""
from pathlib import Path
import hashlib, json, subprocess, tempfile
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
BASELINE = '9c6c0a395504d268adc180c71916d66edfaaf976'
RATE = 44100
LIBRARY = json.loads((ROOT/'Docs/SelectedAudioSources.json').read_text())
SOURCES = {x['assetId']: x for x in LIBRARY['records']}

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def decode(path, channels=2):
    raw = subprocess.check_output(['ffmpeg','-v','error','-i',str(path),
        '-f','f32le','-ac',str(channels),'-ar',str(RATE),'-'])
    pcm = np.frombuffer(raw, dtype='<f4').reshape(-1, channels).astype(np.float64)
    assert len(pcm) > 100 and np.isfinite(pcm).all(), path
    return pcm

def stats(pcm):
    return {'seconds':len(pcm)/RATE, 'frames':len(pcm), 'channels':pcm.shape[1],
            'rms':float(np.sqrt(np.mean(pcm**2))), 'peak':float(np.max(np.abs(pcm)))}

def fit(pcm, frames):
    # A documented constant-rate resampling changes timbre as well as duration.
    x = np.linspace(0, len(pcm)-1, frames)
    return np.stack([np.interp(x, np.arange(len(pcm)), pcm[:,i])
                     for i in range(pcm.shape[1])], axis=1)

def licensed(asset_id):
    record = SOURCES[asset_id]
    p = ROOT/record['originalFiles'][0]['path']
    expected = record['originalFiles'][0].get('sha256')
    if expected: assert sha(p) == expected, p
    return p, decode(p)

def design(label, frames):
    ids = {
        'Veyr':[393847], 'Kaelon':[442015,234631],
        'Nyra':[229313,7103], 'Elyra':[482500,7103],
        'Mira':[378663], 'Rook':[416172,234631],
        'Traveler':[482495,482500], 'NerokHit':[234631],
        'Explosion':[442015,416172],
    }[label]
    clips = {i:licensed(i)[1] for i in ids}
    result = np.zeros((frames,2))
    recipe = []
    def layer(asset, start, duration, gain=1., reverse=False):
        count = min(int(round(duration*RATE)), frames-int(round(start*RATE)))
        if count <= 0: return
        clip = clips[asset][::-1] if reverse else clips[asset]
        part = fit(clip,count)
        edge = min(int(.008*RATE),count//4)
        part[:edge] *= np.linspace(0,1,edge)[:,None]
        part[-edge:] *= np.linspace(1,0,edge)[:,None]
        offset = int(round(start*RATE))
        result[offset:offset+count] += part*gain
        recipe.append({'assetId':asset,'startSeconds':start,
            'durationSeconds':count/RATE,'gain':gain,'reverse':reverse,
            'method':'linear constant-rate resampling; 8 ms endpoint fades'})
    seconds = frames/RATE
    if label == 'Nyra':
        # Three rifle pulses, with a separate rising charge bed.
        layer(7103,0,min(1.5,seconds),.16,True)
        for n,g in enumerate([1.,.92,.82]):
            layer(229313,.13+n*.42,min(1.65,seconds-.13-n*.42),g)
        for n in range(3):
            layer(229313,1.8+n*.5,seconds-1.8-n*.5,.07/(n+1))
    elif label == 'Traveler':
        layer(482495,0,seconds,.40,True)
        layer(482500,min(.55,seconds*.1),seconds-min(.55,seconds*.1),1.)
        layer(482495,min(2.1,seconds*.3),seconds-min(2.1,seconds*.3),.25)
    else:
        layer(ids[0],0,seconds)
        if len(ids)>1:
            layer(ids[1],.015, min(seconds-.015,1.1), .22,
                  reverse=label=='Elyra')
    return result, recipe, ids

def save(pcm, path, codec):
    path.parent.mkdir(parents=True,exist_ok=True)
    enc = {'vorbis':['-c:a','libvorbis','-q:a','5'],
           'wav':['-c:a','pcm_s16le'],
           'mp3':['-c:a','libmp3lame','-b:a','256k']}[codec]
    subprocess.run(['ffmpeg','-v','error','-y','-f','f32le','-ar',str(RATE),
        '-ac',str(pcm.shape[1]),'-i','-','-map_metadata','-1',*enc,str(path)],
        input=pcm.astype('<f4').tobytes(),check=True)

def main():
    jobs = [(h,f'Assets/Alterverse/Resources/Audio/Heroes/{h}/Ultimate_Selected.ogg','vorbis',2)
            for h in ['Veyr','Kaelon','Nyra','Elyra','Mira','Rook','Traveler']]
    jobs += [('NerokHit','ArtSource/Audio/Nerok/BasicHit.mp3','mp3',1),
             ('Explosion','ArtSource/Audio/Combat/Explosion.wav','wav',1)]
    report = {'schemaVersion':1,'generated':'2026-10-12',
        'referenceCommit':BASELINE,
        'referenceUse':'timing and loudness measurements only; no reference samples copied',
        'generator':'Tools/HeroConcepts/close_audio_gaps.py',
        'ffmpegVersion':subprocess.check_output(['ffmpeg','-version'],text=True).splitlines()[0],
        'numpyVersion':np.__version__,'replacements':[]}
    with tempfile.TemporaryDirectory() as td:
        for label, path, codec, channels in jobs:
            dest = ROOT/path
            old = Path(td)/Path(path).name
            old.write_bytes(subprocess.check_output(['git','show',BASELINE+':'+path],cwd=ROOT))
            ref = decode(old,channels)
            reference = stats(ref)
            meta = dest.with_name(dest.name+'.meta')
            meta_before = sha(meta) if meta.exists() else None
            mixed, recipe, ids = design(label,len(ref))
            if channels == 1: mixed = np.mean(mixed,axis=1,keepdims=True)
            level = min(reference['rms']/stats(mixed)['rms'],
                        .72/max(stats(mixed)['peak'],1e-9))
            mixed *= level
            edge = min(int(.012*RATE),len(mixed)//4)
            mixed[-edge:] *= np.linspace(1,0,edge)[:,None]
            save(mixed,dest,codec)
            output = decode(dest,channels)
            # Lossy overshoot is allowed up to this conservative ceiling.
            if stats(output)['peak']>.92:
                level *= .9/stats(output)['peak']
                mixed *= .9/stats(output)['peak']
                save(mixed,dest,codec); output = decode(dest,channels)
            assert stats(output)['peak']<.95
            assert abs(len(output)-len(ref)) <= 2048
            assert stats(output)['rms'] > .002
            if meta_before: assert sha(meta) == meta_before
            inputs = []
            for i in ids:
                p,_ = licensed(i); s=SOURCES[i]
                inputs.append({'assetId':i,'path':str(p.relative_to(ROOT)),
                    'sha256':sha(p),'title':s['title'],'creator':s['creator'],
                    'sourceUrl':s['sourceUrl'],'license':s['license'],
                    'licenseUrl':s['licenseUrl'],'licenseChecked':s['checked'],
                    'remoteDownloadByteComparison':False})
            report['replacements'].append({'label':label,'path':path,
                'oldSha256':sha(old),'oldStatus':'source unverified; retired from current revision',
                'reference':reference,'inputs':inputs,'recipe':recipe,'masterGain':level,
                'codec':codec,'output':stats(output),'sha256':sha(dest),
                'metaSha256':meta_before,'validation':'decoded finite audio, duration contract, no clipping, non-silent, existing meta unchanged'})
    # Preserve the existing runtime gain on the two shared WAV outputs.
    subprocess.run(['python','Tools/CyberShark/import_selected_audio.py'],cwd=ROOT,check=True)
    for p in ['Assets/Alterverse/Resources/Audio/Heroes/Nerok/Basic_Impact.wav',
              'Assets/Alterverse/Resources/Audio/SFX/Explosion.wav']:
        pcm=decode(ROOT/p,1)
        assert 0.002<stats(pcm)['rms'] and stats(pcm)['peak']<.95
        report.setdefault('derivedRuntime',[]).append({'path':p,
            'sha256':sha(ROOT/p),'audio':stats(pcm),
            'processor':'Tools/CyberShark/import_selected_audio.py'})
    out = ROOT/'Docs/HeroAssets';out.mkdir(parents=True,exist_ok=True)
    (out/'AudioReplacements.json').write_text(json.dumps(report,indent=2)+'\n')
    retired = LIBRARY.pop('unresolved',[])
    LIBRARY['retiredUnverified'] = retired
    LIBRARY['replacementEvidence'] = 'Docs/HeroAssets/AudioReplacements.json'
    LIBRARY['unresolved'] = []
    LIBRARY['notes'] = [n for n in LIBRARY.get('notes',[]) if 'no sound replacement' not in n]
    LIBRARY['notes'].append('The nine unverified inputs are retired; current replacements and licensed inputs are recorded in AudioReplacements.json. Git history preserves old revisions without assigning them a retrospective license.')
    for entry in LIBRARY['records']:
        for mapping in entry.get('currentMappings',[]):
            if 'same Git blob' in mapping.get('note',''):
                mapping['note']='Filename/catalog match; byte identity was not established. See decoded-audio audit.'
    (ROOT/'Docs/SelectedAudioSources.json').write_text(json.dumps(LIBRARY,indent=2)+'\n')
    print('Validated and documented nine replacement mixes; legacy runtime gains preserved.')

if __name__ == '__main__': main()
