# World/Hot Zone creature motion — 2026-10-17

Reported symptom: online Hot Zone monsters move jerkily and walk strangely.

World entities receive server poses at 8 Hz. The previous client followed each new goal using exponential smoothing. Constant server speed therefore became a repeating acceleration/deceleration pattern at every packet, which also changed the distance sampled by the creature gait. The root was moved in LateUpdate after HotZoneTank.Update had already sampled that frame's travel.

Use a bounded eight-sample interpolation buffer with a 150 ms delay for the 125 ms world snapshot cadence. Sample it in Update before default-order creature animation. First poses, explicit relocations and jumps greater than six units reset the buffer. Missing packets hold the final received pose without extrapolation. Dedicated-server authority is explicitly excluded, including when LocalIsHost is false. The existing hero buffer keeps its 100 ms default; only world entities request 150 ms. Protocol, attack damage, server AI, snapshot frequency and resource allocation are unchanged.

Production world-entity component and pose buffer compile in controlled timing/render fixtures. 862 checks cover 8 Hz movement at 30/60/120 render FPS, bounds, packet gaps, relocations, life presentation, dedicated authority and the hero default delay. For a constant 2.25-unit/s server path, velocity standard deviation changed from 1.2157 to 0.2502 at 30 FPS, 1.2482 to 0.1503 at 60 FPS and 1.2550 to approximately zero at 120 FPS. These are synthetic timing results, not Android FPS measurements. Existing 4306 VFX/combat checks and 225 transport checks also pass.

The client/AAB must be rebuilt to receive this presentation change. A server rebuild is not required for this wire-compatible client interpolation update. Native Unity compilation, the creature's complete authored animation and a real-device online match remain to be verified.
