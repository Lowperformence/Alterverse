"""Convert the owner-selected CC0 recording without changing speed, pitch or stereo.
Usage: python Tools/Insertion/prepare_audio.py 534692__thepodcastdoctor__spaceship-land.wav
Requires ffmpeg. The original four synthetic transporter cues are no longer used.
"""
import argparse,hashlib,json,subprocess
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('source',type=Path);a=p.parse_args()
root=Path(__file__).resolve().parents[2];out=root/'Assets/Alterverse/Resources/Audio/SFX/InsertionTransport/R35Arrival.wav'
subprocess.run(['ffmpeg','-v','error','-y','-i',str(a.source),'-map_metadata','-1','-ar','44100','-ac','2','-c:a','pcm_s16le',str(out)],check=True)
report={'title':'SpaceShip Land.wav','author':'thepodcastdoctor','license':'CC0 1.0','source':'https://freesound.org/people/thepodcastdoctor/sounds/534692/','source_sha256':hashlib.sha256(a.source.read_bytes()).hexdigest(),'runtime_file':str(out.relative_to(root)),'runtime_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'conversion':'44.1 kHz stereo PCM16; original duration, playback speed and pitch preserved'}
(root/'Tools/Insertion/audio-report.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
