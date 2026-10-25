"""Validate authored audio and PCM preload settings; no playback/device performance is measured."""
from pathlib import Path
import hashlib,json
import numpy as np
from scipy.io import wavfile
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Alterverse/Resources/Audio/SFX/InsertionTransport'
result={}
for name in ['TurbineLoop','BayRelease','CargoHydraulics','Departure']:
    path=OUT/(name+'.wav');rate,pcm=wavfile.read(path)
    assert rate==44100 and pcm.dtype==np.int16 and pcm.ndim==1,name
    x=pcm.astype(np.float64)/32768
    assert np.isfinite(x).all() and .1<np.max(np.abs(x))<.9,name
    assert abs(np.mean(x))<.01,name+' DC offset'
    meta=path.with_suffix('.wav.meta').read_text()
    assert all(s in meta for s in ['loadType: 0','compressionFormat: 0','preloadAudioData: 1','loadInBackground: 0']),name
    if name=='TurbineLoop':
        assert len(pcm)==rate*3
        assert abs(x[0]-x[-1])<np.percentile(abs(np.diff(x)),99), 'audible loop discontinuity'
    else:
        assert .6<len(pcm)/rate<.72 and x[0]==0 and abs(x[-1])<.001,name+' edge fade'
    result[name]={'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
print('PASS: 4 mono PCM sound assets; finite peaks, DC, loop seam, one-shot fades and synchronous preload settings.')
print('Total WAV bytes:',sum(x['bytes'] for x in result.values()))
