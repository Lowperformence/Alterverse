# Live online diagnosis — 16 October 2026

## Confirmed hosting state

The authenticated Edgegap dashboard was inspected, including its integrated live API, deployment archives, application versions and container registry. No account, registry or matchmaker credentials are included here.

- `dimensions-front-live` is online. An authenticated `GET /inspect/tickets` succeeded with HTTP 200 and returned zero active tickets at the time of inspection.
- Its active configuration contains only `dimensions-front-online`, references `v0.1.1-server`, initially requires two teams of five, expands to one–five humans per team after ten seconds, and expires tickets after two minutes. It contains no single-team fallback. GitHub configuration changes had not been imported.
- Yesterday's two-ticket deployments `812b82b96c63` (15 October, 22:11 Berlin time) and `b66a977f68d1` (22:14) were successfully provisioned. The latter completed in 0.83 seconds and remained until its one-hour hosting limit. This establishes allocation, not successful game-client connection or match launch. Their game-process logs have expired.
- Only application/container versions `v0.1.0-server` and `v0.1.1-server` exist. The registry dates the active `v0.1.1-server` artifact to **3 October, 19:07 Berlin time**; its digest is `sha256:a83b9c9c163958d332100d063c3d7a1602fe85465274dfc14f5d259257cd42dd`. There is no `v0.1.2-server` image/version.
- The published mapping is `gameport`, UDP, internal port 7777; this agrees with the client and current dedicated-server bootstrap.

## Why the old server matters before and after 4v4

The source handshake at `7464c67e6199bd6d3756d831bcb8780c69781443` on 3 October used protocol **1**. By 12 October (`58d459c09580e197d00ba89e2d94cc315dcb664d`) it used protocol **3**, still for 5v5. The current 4v4 client/server uses protocol **4**. The registry artifact predates those changes. An updated client paired with that old build is therefore an incompatible deployment; this problem is not explained solely by changing from 5v5 to 4v4. The binary's embedded protocol was not directly extracted, and the expired historical logs do not establish the exact rejection observed by the two devices.

One explicitly tagged diagnostic deployment, `756d5689a87d`, ran the existing image on 16 October. Its live stdout confirmed:

```text
Initialize engine version: 6000.6.1f1 (7efac9f6c10e)
DIMENSIONS FRONT SERVER: listening on UDP 7777 with no local player.
```

The process also reported a missing `libpulse.so.0` while loading Vivox and 106 stripped-shader messages in the captured startup log. It nevertheless started the game transport; those messages alone do not establish the matchmaking failure. No real game client was connected in this diagnostic. The diagnostic deployment was terminated after capture.

Captured startup-log SHA-256: `e46a82acd8105722cc470224ac7d160da7fa0a69ecfa5a29f984714cd8f50f42`.

## Source corrections

- Membership status changes now reach the console. Previously the response overwrote the old status before the change detector ran, suppressing the intended diagnostic logs.
- A readiness PATCH must confirm the requested `is_ready` value. Otherwise the search reports a clear error and performs existing membership cleanup instead of silently assuming the player is queued.
- Casual permits one matchmaking team after 20 seconds and now has a **30-second ticket expiration**. Edgegap attempts partial matches at expansion/expiration boundaries; lowering the minimum alone did not guarantee a solo allocation after 20 seconds. Expect allocation around the final boundary, then container startup and game connection, not gameplay exactly at 20/30 seconds.
- `edgegap-casual-live-v1.2.json` carries the same Casual settings without requiring a Ranked application version that is absent from the live account. The canonical Ranked queue and its release lock remain unchanged.

References: [Edgegap partial matches](https://docs.edgegap.com/learn/matchmaking/matchmaker-in-depth#partial-matches), [player-count timing](https://docs.edgegap.com/learn/matchmaking/matchmaker-in-depth#matchmaking-rules).

## Required hosting rollout

GitHub changes do not rebuild a hosted Unity server. Unity is unavailable in this execution environment. The following build/upload remains required; the active matchmaker was not switched to an absent image or relabeled old binary.

1. Pull `dev/vertical-slice` in the actual Unity project. Run **Tools → Dimensions Front → Build Linux Dedicated Server**.
2. From the project root, log in to the existing registry, then build and push the **generated game-server Dockerfile**:

   ```powershell
   docker login registry.edgegap.com
   docker build --platform linux/amd64 -t registry.edgegap.com/dimensions-front-yf4x25e6aj2i/dimensions-front-server:v0.1.2-server Builds/DedicatedServer/Linux
   docker push registry.edgegap.com/dimensions-front-yf4x25e6aj2i/dimensions-front-server:v0.1.2-server
   ```

   The project-root Dockerfile builds the Python account service, not the Unity game server. Keep the explicit build context above.
3. Create Edgegap application version `v0.1.2-server` from the new tag, preserving UDP `gameport` 7777 and existing runtime settings. Do not merely rename/duplicate the old container tag.
4. Import `Server/matchmaking/edgegap-casual-live-v1.2.json` in the existing matchmaker after the new application version exists. A configuration reload clears queued tickets; re-create searches afterwards. This Casual-only rollout does not require enabling/deploying Ranked.
5. With matching updated clients, verify one real solo player gets three allied bots and four opposing bots, and two independently queued players can enter a shared match. Save the client phases, assignment and live server handshake/launch logs before terminating the deployment.

## Verification

The .NET production-code suite passed **1,904 checks**, including one–four humans in one assigned team, exact bot counts, acknowledgements, successful launch, readiness rejection/cleanup, and phase logging. **245 backend tests** passed, including the Casual-only configuration consistency check. These are controlled engine/HTTP/transport fixtures, not an Android/Unity build or a successful live gameplay test.
