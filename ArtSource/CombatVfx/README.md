# Attack VFX source assets

The runtime uses one 1024×1024 RGBA mask atlas: 18 particle masks, five six-frame
weapon swings, and a sixteen-frame energy/fire burst. All three source packs are
published by their authors under CC0 1.0:

| Author | Original pack | Selected use |
| --- | --- | --- |
| Kenney Vleugels / Kenney.nl | [Particle Pack](https://kenney.nl/assets/particle-pack) | Muzzle flashes, sparks, soft stars, dust, mist, runes, portal and vortex accents |
| Cethiel | [Weapon Slash - Effect](https://opengameart.org/content/weapon-slash-effect) | Heavy sword, assassin, claw, cross and lunge animations |
| rubberduck | [sparkling fireball effect](https://opengameart.org/content/sparkling-fireball-effect) | Brakk's hot impacts and Ione's tinted energy core |

`CC0-1.0.txt` preserves the legal text; `Kenney-License.txt` preserves Kenney's
included notice and additional template credits. The other two archive downloads
contain no separate license file; their author upload pages identify CC0.
`AttackAtlas.sources.json` records those pages, original archive hashes, every
source member, cell, frame range and the packed image checksum. Credits are
documented voluntarily. There are no imported sample scripts or dependencies.

Rebuild with Python and Pillow installed:

```sh
python3 ArtSource/CombatVfx/build_atlas.py --packs-root /absolute/path/to/original-downloads
```

The input filenames are the three archives in the provided download bundle.
Packing preserves source alpha, converts RGB to neutral masks for runtime hero
tinting, downsizes within 120×120 pixels, and leaves four transparent pixels on
all cell edges. Cell numbers are top-to-bottom in the source PNG; the shader
converts them to Unity's bottom-left UV origin. Frames clamp within their clip.

The atlas is sampled through one shared material, two triangles per layer,
MaterialPropertyBlocks, and the existing 192-slot scheduler. Its Android import
override is ASTC 6×6, with mipmaps and CPU readability disabled. Optional layers
use the existing decorative budget; warnings and projectile cores keep their
reserved slots. No new packets, compute shaders, postprocessing, scene-colour
or depth-texture dependencies are introduced.

Actual Unity import/shader compilation and Android GPU/memory/frame-time
measurements must run in Unity/on the device. The Python/C# fixture checks are
not a substitute for those acceptance steps.
