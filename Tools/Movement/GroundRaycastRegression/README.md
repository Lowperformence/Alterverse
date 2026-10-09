# Ground raycast regression

Run `python3 Tools/Movement/GroundRaycastRegression/run.py`, optionally supplying
`--mono-root /path/to/extracted/mono`.

The runner extracts the **unchanged production `Ground` method** and compiles it
against a small controlled Physics substitute. It verifies both foot positions:
crowded Units (8), Default (0), and Structures (9) cannot consume the hit buffer or
become floors; only Ground (10) is queried. It also verifies trigger, steep slope,
self, hero, controller, and height filtering; nearest valid floor selection;
valid Ground slopes; and deterministic fallback when no Ground is hit.

`--old-mask` is a negative control restoring `Physics.DefaultRaycastLayers` in
the temporary extracted method. It must exit nonzero: the first crowded case
selects an unrelated surface instead of the actual floor.

This exercises method logic and layer/trigger filtering contracts with simulated
physics. It does **not** run Unity raycasts, import FBX skins, or prove visual
quality in the game. No production file is modified.
