# Server-only hazard preparation

Dedicated servers currently build meteor warning rings, falling meshes, bomber models and flyby audio even though player devices render those independently. The new guards skip this presentation setup only when `DedicatedServerNetworkRuntime.Requested` is true. Normal clients and local games still use the existing rendering path.

Planet and Noxara meteor strike objects, hazard registries, telegraph deadlines, impact positions, radius, damage and cleanup remain in the simulation. Bomber route generation, bomb drops, impact timing and network events remain intact. The headless bomber no longer requires visual shaders to run its gameplay. Hero movement/attack code, NPC spawn timing, AI update rates and network serialization are unchanged.

This reduces server-side object/material/mesh preparation. It does not reduce packet sizes or send rates, and it does not establish that this work caused the observed 179.5 ms frame. No numeric latency improvement has been measured.

## Validation

`python Tools/Performance/ServerHazardRegression/run.py --dotnet-root <dotnet-8-sdk>`

30 checks compile the production hazard creation, update and damage methods plus presentation guards against deterministic engine fixtures. Checks cover original deadlines, damage, duplicate colliders, Noxara team filtering, pause behavior, single impact resolution, bomber monster fallback and entry to the rendering path on normal clients. Rendering bodies and Unity physics are substitutes; this does not render or build the game.

Existing `Tools/Multiplayer/HotZoneResultRegression/run.py`: 34 checks passed for reward identities, authoritative spawn, snapshot reconstruction and result capture.

## Before release

Build a dedicated server from the reviewed commit and run a separate test match with existing Android clients. Check planet/Noxara warning rings and falling meteors, bomber flyby/audio/bombs, original warning-to-impact timing, damage and cancellation on match end. Compare server frame-time distributions and allocations under matched workloads. A native Unity build and this visual/network verification are still outstanding.

For this isolated change, the network protocol is unchanged and the guards take effect in the new server build; an Android update is not required. No Edgegap version or running deployment was changed during preparation.
