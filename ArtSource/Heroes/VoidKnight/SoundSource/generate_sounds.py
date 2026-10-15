"""Original procedural Void Knight SFX prototypes. Python + NumPy + SciPy."""
from pathlib import Path
import numpy as np
from scipy.signal import butter, sosfilt
from scipy.io.wavfile import write

SR = 48000
ROOT = Path(__file__).parent
ROOT.mkdir(exist_ok=True)
rng = np.random.default_rng(415)

def make(name, duration, kind, power=1.):
    t = np.arange(int(SR*duration))/SR
    n = rng.normal(0, 1, len(t))
    airy = sosfilt(butter(2, [550, 7500], btype='bandpass', fs=SR, output='sos'), n)
    if kind == 'swing':
        env = np.sin(np.pi*np.minimum(t/(duration*.75), 1))**2
        freq = np.linspace(620*power, 95, len(t))
        x = .65*airy*env + .18*np.sin(2*np.pi*np.cumsum(freq)/SR)*env
    elif kind == 'impact':
        x = .55*airy*np.exp(-t*32) + .3*np.sin(2*np.pi*(95*t+6*(1-np.exp(-18*t))))*np.exp(-t*9)
        for f in [730, 1171, 1733, 2411]:
            x += .12*np.sin(2*np.pi*f*power*t)*np.exp(-t*11)
    elif kind == 'charge':
        freq = 65+310*(t/duration)**2
        phase = 2*np.pi*np.cumsum(freq)/SR
        env = np.minimum(t/.07, 1)*(0.15+.85*(t/duration)**1.3)
        x = (.35*np.sin(phase)+.14*np.sin(phase*1.501)+.13*airy)*env
    elif kind == 'burst':
        x = .6*airy*np.exp(-t*9)+.4*np.sin(2*np.pi*(52*t+14*(1-np.exp(-t*9))))*np.exp(-t*5)
        x += .13*np.sin(2*np.pi*391*t)*np.exp(-t*7)
    else:
        x = np.zeros_like(t)
        for i, f in enumerate([196, 293.66, 415.3, 587.33]):
            dt = np.maximum(t-i*.035, 0)
            x += .23*np.sin(2*np.pi*f*dt)*np.exp(-dt*7)*(t>=i*.035)
        x += .04*airy*np.exp(-t*10)
    # Short finite echo, with no circular wrap.
    delay = int(.065*SR)
    if len(x)>delay:
        x[delay:] += .16*x[:-delay].copy()
    fade = np.minimum(t/.006, 1)*np.minimum((duration-t)/.025, 1)
    x *= fade
    x = .8*x/max(np.max(np.abs(x)), 1e-9)
    out = np.round(x*32767).astype(np.int16)
    write(ROOT/name, SR, out)
    return {'file':name, 'seconds':round(len(out)/SR,3), 'peak_dbfs':round(float(20*np.log10(np.max(np.abs(out.astype(float)))/32767)),2)}

if __name__ == '__main__':
    import json
    definitions = [
        ('basic_01_swing.wav', .38, 'swing', 1.),
        ('basic_02_swing.wav', .42, 'swing', 1.2),
        ('basic_03_swing.wav', .56, 'swing', .7),
        ('sword_hit.wav', .48, 'impact', 1.),
        ('rift_lunge.wav', .58, 'swing', 1.6),
        ('null_guard_raise.wav', .30, 'charge', 1.),
        ('null_guard_block.wav', .65, 'impact', .7),
        ('null_guard_counter.wav', .46, 'swing', .85),
        ('eventide_charge.wav', .65, 'charge', 1.),
        ('eventide_cleave.wav', 1.05, 'burst', 1.),
        ('void_resonance.wav', .72, 'chime', 1.),
    ]
    checks = [make(*args) for args in definitions]
    (ROOT/'audio_manifest.json').write_text(json.dumps(checks, indent=2))
    print(json.dumps(checks, indent=2))
