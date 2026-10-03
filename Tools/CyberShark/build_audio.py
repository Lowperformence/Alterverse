"""Original synthesized anchor/hydraulic cues. Never overwrite the user's ultimate MP3."""
from pathlib import Path
import numpy as np,wave
R=Path(__file__).resolve().parents[2];O=R/'Assets/Alterverse/Resources/Audio/Heroes/Nerok';O.mkdir(parents=True,exist_ok=True)
sr=22050;rng=np.random.default_rng(1874)
for action in range(5):
 for phase in range(3):
  if action==2 and phase!=0:continue
  if action==4 and phase==0:continue
  duration=.22 if phase==2 else .35 if phase==1 else .55 if action<4 else 1.0
  t=np.arange(int(sr*duration))/sr;noise=rng.normal(0,1,len(t))
  water=np.convolve(noise,np.ones(8)/8,mode='same')
  freq=85+action*22
  metal=np.sin(2*np.pi*freq*t)*np.exp(-t*10)+.3*np.sin(2*np.pi*freq*2.71*t)*np.exp(-t*18)
  if phase==2:y=.25*water*np.sin(np.pi*t/duration)**2+.12*np.sin(2*np.pi*(110*t+70*t*t))
  elif phase==1:y=.42*metal+.32*water*np.exp(-t*7)
  else:y=.25*metal+.35*water*np.sin(np.pi*t/duration)**1.5
  y*=np.minimum(t/.008,1)*np.minimum((duration-t)/.035,1);y=np.clip(y,-.85,.85)
  name=['Basic','Q','W','E','R'][action]+['_Cast','_Impact','_Windup'][phase]+'.wav'
  with wave.open(str(O/name),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(sr);f.writeframes((y*32767).astype('<i2').tobytes())
print('Original Nerok cues generated')
