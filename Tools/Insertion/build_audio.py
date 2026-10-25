"""Original ATLAS sound design: layered turbine wash, bearing whine, belly-hatch pressure and cargo hydraulics.
Offline authoring only; the game loads the resulting PCM assets during the loading screen.
No third-party samples. Requires numpy/scipy. Run from any directory.
"""
from pathlib import Path
import json
import numpy as np
from scipy.signal import butter, sosfiltfilt
from scipy.io import wavfile
RATE = 44100
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Alterverse/Resources/Audio/SFX/InsertionTransport'
RNG = np.random.default_rng(25102026)
REPORT = {}

def filtered_noise(n, low, high):
    # Filter a longer bed to avoid startup transients; deterministic, no game RNG consumed.
    pad = RATE // 4
    x = RNG.normal(size=n+2*pad)
    if low == 0:
        sos = butter(3, high, fs=RATE, output='sos')
    else:
        sos = butter(3, [low, high], btype='bandpass', fs=RATE, output='sos')
    y = sosfiltfilt(sos, x)[pad:pad+n]
    return y / max(np.std(y), 1e-6)

def fade(t, length, attack=.012, release=.08):
    a = np.clip(t/attack, 0, 1)
    b = np.clip((length-t)/release, 0, 1)
    return np.sin(a*np.pi/2)**2 * np.sin(b*np.pi/2)**2

def save(name, signal, peak=.72, loop=False):
    signal = np.tanh(signal*.8)
    signal = signal / max(np.max(np.abs(signal)), 1e-6) * peak
    pcm = np.round(signal*32767).astype('<i2')
    wavfile.write(OUT/(name+'.wav'), RATE, pcm)
    REPORT[name] = {'seconds':len(signal)/RATE, 'peak_dbfs':round(float(20*np.log10(np.max(np.abs(signal)))),2),
                    'rms_dbfs':round(float(20*np.log10(np.sqrt(np.mean(signal*signal)))),2),
                    'loop':loop, 'loop_edge_delta':round(float(abs(signal[0]-signal[-1])),6)}

OUT.mkdir(parents=True,exist_ok=True)
# Three second seamless turbine bed. Harmonic frequencies contain whole cycles.
n=RATE*3;t=np.arange(n)/RATE
# Periodic FFT filtering produces a smooth, truly looping noise bed.
f=np.fft.rfftfreq(n,1/RATE);spectrum=np.fft.rfft(RNG.normal(size=n))
spectrum *= np.exp(-(f/1700)**2) * (1-np.exp(-(f/90)**2))
wash=np.fft.irfft(spectrum,n);wash/=np.std(wash)
rumble=.20*np.sin(2*np.pi*48*t)+.12*np.sin(2*np.pi*96*t+.3)
whine=.10*np.sin(2*np.pi*432*t+.7*np.sin(2*np.pi*3*t))
whine+=.06*np.sin(2*np.pi*864*t+.4*np.sin(2*np.pi*4*t))
blade=.72+.28*np.sin(2*np.pi*24*t)
signal=rumble+whine+.22*wash*blade
save('TurbineLoop',signal,peak=.68,loop=True)
# Cargo-bay pressure release: low air rush and a short latch, with no ground impact.
length=.70;t=np.arange(int(RATE*length))/RATE
pressure=.34*filtered_noise(len(t),75,1800)*np.exp(-t*4.2)
pressure+=.20*np.sin(2*np.pi*58*t)*np.exp(-t*5)
pressure+=.10*np.sin(2*np.pi*275*t)*np.exp(-t*26)
save('BayRelease',pressure*fade(t,length,.012,.16),peak=.75)
# Pneumatic pressure release + geared hydraulic motor + locking latch.
length=.69;t=np.arange(int(RATE*length))/RATE
hiss=.22*filtered_noise(len(t),550,6300)*np.exp(-t*2.5)
servo=.17*np.sin(2*np.pi*(182*t-32*t*t))+.065*np.sin(2*np.pi*366*t)
servo*=np.sin(np.clip(t/length,0,1)*np.pi)
dt=np.maximum(0,t-.54);latch=(t>=.54)*.36*np.sin(2*np.pi*162*dt)*np.exp(-dt*36)
save('CargoHydraulics',(hiss+servo+latch)*fade(t,length,.016,.07),peak=.67)
# Short rising bearing whine and broad jet wash, with a smooth Doppler-like tail before GO.
length=.70;t=np.arange(int(RATE*length))/RATE
spool=.28*np.sin(2*np.pi*(175*t+240*t*t))+.12*np.sin(2*np.pi*(350*t+480*t*t))
spool+=.28*filtered_noise(len(t),90,3600)*(1+.15*np.sin(2*np.pi*28*t))
spool+=.18*np.sin(2*np.pi*54*t)
save('Departure',spool*fade(t,length,.05,.26),peak=.75)
(ROOT/'Tools/Insertion/audio-report.json').write_text(json.dumps(REPORT,indent=2)+'\n')
print(json.dumps(REPORT,indent=2))
