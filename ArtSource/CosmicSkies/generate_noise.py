#!/usr/bin/env python3
"""Generate periodic numerical shader data; does not create or edit the sky artwork."""
from pathlib import Path
import numpy as np
from PIL import Image


def main():
    size = 256
    rng = np.random.default_rng(20261029)
    frequencies = np.fft.fftfreq(size)*size
    radius_squared = frequencies[:, None]**2+frequencies[None, :]**2
    channels = []
    for _ in range(2):
        spectrum = np.fft.fft2(rng.normal(size=(size, size)))
        weights = sum(np.exp(-radius_squared/(2*scale**2))/scale
                      for scale in (2, 4, 8, 16, 32))
        field = np.fft.ifft2(spectrum*weights).real
        field = (field-field.min())/(field.max()-field.min())
        channels.append(np.round(field*255).astype(np.uint8))
    data = np.stack((channels[0], channels[1], np.full((size, size), 128, np.uint8)), axis=-1)
    root = Path(__file__).resolve().parents[2]
    destination = root/'Assets/Alterverse/Resources/MapPanoramas/CosmicNoise.png'
    Image.fromarray(data).save(destination)
    print(destination)


if __name__ == '__main__':
    main()
