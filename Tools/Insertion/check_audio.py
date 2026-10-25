"""Validate the selected recording, its provenance and preload settings; no listening/device test."""
from pathlib import Path
import hashlib,json,wave
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Alterverse/Resources/Audio/SFX/InsertionTransport'
report=json.loads((ROOT/'Tools/Insertion/audio-report.json').read_text())
path=OUT/'R35Arrival.wav'
assert hashlib.sha256(path.read_bytes()).hexdigest()==report['runtime_sha256']
with wave.open(str(path)) as w:
 assert w.getframerate()==44100 and w.getsampwidth()==2 and w.getnchannels()==2
 assert w.getnframes()==44100*14
 x=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').astype(float)/32768
assert np.isfinite(x).all() and .8<np.max(np.abs(x))<1
assert abs(np.mean(x))<.01
meta=Path(str(path)+'.meta').read_text()
assert all(s in meta for s in ['loadType: 0','compressionFormat: 0','preloadAudioData: 1','loadInBackground: 0','forceToMono: 0'])
assert report['license']=='CC0 1.0' and report['author']=='thepodcastdoctor'
assert sorted(p.name for p in OUT.glob('*.wav'))==['R35Arrival.wav'],'old synthetic clips must be removed'
print('PASS: selected CC0 recording; source/runtime hashes, 14s stereo PCM16, original rate, finite unclipped samples, DC and preload settings.')
print('Runtime WAV bytes:',path.stat().st_size)
