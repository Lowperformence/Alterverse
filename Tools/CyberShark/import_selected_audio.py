"""Rebuild the user-selected hit and shared explosion mixes. Requires ffmpeg."""
from pathlib import Path
import subprocess
ROOT = Path(__file__).resolve().parents[2]
for source, target, gain in [
    ("ArtSource/Audio/Nerok/BasicHit.mp3", "Assets/Alterverse/Resources/Audio/Heroes/Nerok/Basic_Impact.wav", ".4"),
    ("ArtSource/Audio/Combat/Explosion.wav", "Assets/Alterverse/Resources/Audio/SFX/Explosion.wav", ".85"),
]:
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(ROOT/source),
        "-af", "volume="+gain+",afade=t=in:d=0.003", "-ac", "1", "-ar", "44100", "-c:a", "pcm_s16le", str(ROOT/target)], check=True)
