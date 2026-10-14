# TOI-715 b — Ivory Grove Front

TOI-715 b is a free selectable arena on the existing `dev/vertical-slice` game. Its appended enum ID is 13; all previous saved and network IDs remain stable. The PLAY browser and owned-map shuffle derive their entries from `ArenaShopCatalog`, so the new map is immediately available without changing the selected map or charging shards.

## World and creature

The user-supplied red/white planet illustration inspires rust soil, light mineral bands, ivory fan plants, dark lobed shrubs and layered red shelves. This is fictional game art, not a claim about the real planet's surface or life. Ground bands mirror across the team axis. Eight paired peripheral vegetation pockets use the existing surface anchoring, building-clearance filter, regional mesh batches and LODs.

RUBROX is an original six-legged rust-armoured creature with ivory back bands and a branched crown. Both team variants receive faction-tinted eyes, animated legs, an explicit projectile muzzle and a crown/head attack gesture bound to the shared combat event. It inherits `HotZoneTank` health, targeting, capture rewards, authority, attack timing and collision. Its art has no extra colliders or lights.

Base healing guards receive rust/ivory habitat armour through the existing guard rig. Terrain uses the existing Mars packed textures with a new colour bake, dry-soil finish and warm lighting. The normal shared universe panorama remains in use; the procedural atmosphere fallback also has a TOI colour profile.

## Shared gameplay

The shared exoplanet path builds the same gameplay surface as Moon. The normal bootstrap supplies bases, healing, lanes, towers, minions, bots, pickups, supply drops, the dimensional front, Hotzones, match results and rewards. No separate scene or duplicated combat system is required. Co-op/online plans transport the appended enum value and dispatch the same monster builder on each client. Dedicated server and clients must use the same updated build.

Daily map wins include bit 13. The account server accepts the complete 14-map mask through 16383 and still rejects bits outside that range. Deploy the updated account server before or alongside the new clients to avoid rejecting TOI wins; the running service has not been deployed by this change.

## Validation

Profile tests verify selection and map-win persistence for ID 13 and the 14-map mask. The complete profile suite has three existing purchase-price expectation failures, reproduced against the unchanged source snapshot; the map persistence and invalid-mask tests pass.

Unity 6000.6.1f1 is unavailable in the implementation environment. Unity compilation, Play Mode and Android rendering have not been run. Added editor checks under `DIMENSIONS FRONT/Checks/TOI-715 b` validate catalogue/resources/geometry, both monster teams, attack binding, mesh normals, duplicate-build guards and visual budgets. Also run the existing All maps shader/asset checks, then test a complete local and co-op match and an Android device build before release.
