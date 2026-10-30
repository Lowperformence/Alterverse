#!/usr/bin/env python3
"""Catch star cores disappearing at mobile texture sizes; not an ASTC/device rendering test."""
from pathlib import Path
import json
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[3]

def verify():
    path=ROOT/'Assets/Alterverse/Resources/MatchSkybox360/Textures/Sky360_StarsNebula.png'
    with Image.open(path) as image:
        image.load();assert image.size==(4096,2048) and image.mode=='RGB'
        measurements=[]
        for width,minimum_bright_pixels in ((2048,4000),(1024,700)):
            reduced=np.asarray(image.resize((width,width//2),Image.Resampling.LANCZOS))
            bright=int((reduced.max(axis=2)>127).sum())
            assert bright>=minimum_bright_pixels,f'Star cores vanish at {width}px: {bright} bright pixels'
            # The original nebula alone stays below this threshold; bright pixels identify star detail.
            measurements.append({'width':width,'pixels_over_half_srgb':bright})
    print(json.dumps({'star_visibility_after_reduction':measurements,'astc_device_test':False}))

if __name__=='__main__':verify()
