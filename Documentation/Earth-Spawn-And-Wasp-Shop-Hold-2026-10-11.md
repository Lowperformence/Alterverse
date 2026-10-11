# Earth spawn terrain and WASP-76b shop hold

Earth's `EarthForestPolish.RefreshBuildingSoil` no longer creates the 12 by 10.2 metre
brown soil sites around `RealityCoreShell` objects. This removes the overlay at both
team spawns. Tower soil, the forest, normal terrain, core structures and gameplay
collision are preserved.

WASP-76b remains visible in the map shop at **900 shards**, with **Coming Soon** and a
drawn padlock on its disabled purchase button. `ArenaShopCatalog.Wasp76bPurchasesEnabled`
is false and the shared `arena_Wasp76b` catalog item has `available: false`. Client
purchase logic and both server ledger adapters reject attempts without debiting
shards or changing ownership. Existing owned WASP maps remain playable; this is a
purchase hold. Later release requires enabling both flags and updating the client
and commerce server together.

Verification: the portable shop harness passed **203 assertions**, including Timmy's
existing hold, WASP online/offline purchase rejection, old ownership preservation
and unchanged purchases for the other seven paid arenas. The commerce suite passed
**56 tests**, covering both HTTP paths, both ledger adapters, wallet/profile
restoration and 24 concurrent held WASP attempts. Changed C# syntax was checked.
Earth's change was reviewed in the shared creation path; native Unity/Android visual
playback was not run.
