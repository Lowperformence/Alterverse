# Skin Shop + Inventory Overhaul

## Implementation and validation status

Implemented on dev/vertical-slice. Unity was NOT executed in this environment. No Unity/.NET C# compiler is installed. Syntax parsing and numeric layout checks passed; this is not a successful Unity compile, visual QA pass or Android performance certification.

## 1. Changed files

Paths below are relative to Assets/Alterverse/Scripts:

- Shop/CosmeticCatalog.cs (new): definitions, account ownership and common purchase entry point.
- Shop/CosmeticProfile.cs (new): thin access boundary to the existing wallet/journal, with an Editor-only in-memory test seam.
- Shop/UniversalSkin.cs: per-hero equip, one-time migration, compatibility wrappers.
- UI/CosmeticCollectionPanel.cs (new): cached shop, cosmetic inventory, compact battle bag, scrolling and responsive detail views.
- UI/CosmeticCollectionChecks.cs (new): non-destructive Editor regression menu.
- UI/CosmeticCollectionStressWindow.cs (new): production grid with 100 synthetic cosmetic cards / 30 synthetic item slots; purchasing disabled.
- UI/MainMenuController.cs: integrates the collection panel, preserves navigation and other store categories; prevents wordmark/navigation overlap at the minimum layout width.
- UI/MenuHeroShowcase.cs: cached hero-specific loadout and camera fitting to renderer bounds, including wings and weapons.
- Visuals/StylizedHeroVisual.cs: reads EquippedFor(identity.Hero) for the player model; no geometry/stats/gameplay changes.
- Profile/ProfileTransaction.cs: notification after an existing transaction is applied; journal behavior unchanged.
- Profile/BattleItemInventory.cs: change notifications after existing equip/grant/consume operations; storage, effects and drop economy unchanged.
- Shop/PaidShardCommerce.cs: entitlement-change notifications so cached ownership updates after sync/disconnect; verification/claim/purchase behavior unchanged.
- Matching Unity .meta files for the five new scripts; this report.

Concurrent changes to MainMenuController's mobile combat descriptions/key widths and HeroAttackCatalog were preserved when integrating with the newer branch head.

## 2–4. Shop, scale and inventory

The cosmetic shop uses a vertical card grid with a separate preview/detail panel. Cards contain a lightweight icon, name, price and visually distinct Locked/Owned/Equipped markers. Selection uses an accent outline. Detail shows the hero preview, price, short description, progression bonus, NO COMBAT POWER and a purchase action. Original is included and always owned. The shop no longer auto-equips purchases.

The list is data-driven, with no fixed card count: 100+ entries use the same grid, and only rows intersecting the viewport are drawn. Normal landscape widths give about three columns; narrower available widths reduce columns while keeping cards readable. The current catalog contains four cosmetics, not 100 real products. ALL/WINGS/SKINS filters activate when at least six entries and both types exist. Future category identifiers are reserved without adding new content or extra simultaneous equipment slots.

Inventory has COSMETICS and BATTLE ITEMS tabs. Cosmetics shows owned entries only, with a hero selector and Equip/Unequip actions. The selected hero is named above the collection. Unequip sets that hero to Original. Shop and inventory have independent scroll/selection state; transactions refresh cached status without replacing view state. Purchased items appear on entering the inventory without scene reload or restart.

At narrow screen widths, selecting a card opens a detail view with a large back-to-collection action, preserving the grid scroll position. Existing safe-area scaling remains in place. Grids accept wheel scrolling and IMGUI pointer drag, with drag cancellation of card clicks. Actual Android touch behavior still requires device verification.

## 5–7. Equip storage, migration and ownership

Account ownership keeps DF_SkinOwned_Voidfall, DF_SkinOwned_PlagueWings and DF_SkinOwned_BetaSupporterWings unchanged. Existing authenticated Voidfall ownership is still recognized and remains session-bound.

Each hero gets one integer slot: DF_Skin_HeroEquipped_<HeroId integer>. There is never more than one selected universal cosmetic for that hero. UniversalCosmeticSelection.EquippedFor(hero) is the stable read point. Compatibility Equipped/Equip APIs target PlayerHeroSelection.Selected.

Migration is marked by DF_Skin_HeroEquip_v1. It recovers the existing journal first, reads DF_Skin_UniversalEquipped or the older DF_Skin_Voidfall bit, and initializes missing hero slots. The selected hero receives the old selection; others start at Original. Existing per-hero values are preserved. All missing slots plus the marker are committed in one ProfileTransaction. Ownership, wallet, battle items and old equip keys are not deleted or reset. Recognized legacy selection intent is retained even if a server entitlement has not synchronized yet; reads return Original until ownership is available, without granting an unowned item.

Purchase still uses the existing PlayerWallet balance and ProfileTransaction to deduct shards and grant ownership atomically. Already-owned, insufficient-balance and unknown-ID checks run before mutation. Existing purchase entry points delegate to this shared implementation; there is no second economy.

## 8. Battle items

The bag displays only positive-count items, using small icon slots, stack count and equipped marker. Selection opens name, effect, quantity and Equip/Unequip controls. Existing four item types, effects, counts and persistence remain unchanged. Adding future supported item definitions to the existing inventory list does not require another menu layout. The Editor stress window draws 30 synthetic slots using the existing item icons; these are UI fixtures, not granted items or 30 new gameplay types.

## 9–11. Economy and combat invariants

| Cosmetic | Shards | Match XP / shards bonus |
|---|---:|---:|
| Original | 0 | 0% |
| Voidfall | 2000 | 5% |
| Plague Wings | 4000 | 10% |
| Beta Supporter Wings | 8000 | 20% |

Prices and bonus percentages are unchanged. WingMatchRewardBonus itself is unchanged: BeginMatch(playerHero) now obtains the correct per-hero selection through its existing EquippedFor call and keeps the existing match-start snapshot. Cosmetics add no damage, HP, speed, attack-speed, ability or cooldown bonuses. Bot AI, map, towers, minions, Battle Pass and reward calculations were not edited.

## 12–14. Checks and limitations

Executed here:

- Tree-sitter C# syntax parsing of all 22 files in the targeted snapshot: no syntax errors.
- git diff --check: passed.
- Numeric layout checks for 1920x1080, 2160x1080 (18:9), 2400x1080 (20:9) and narrow width, including horizontal safe-area inset: card widths stay readable and inside the viewport; 100 cards and 30 slots produce bounded scrollable content.
- Static checks of unchanged prices, ownership keys and absence of save-deletion calls in the new cosmetic path.
- Integration review: current branch's concurrent main-menu changes retained; unrelated remote tree entries preserved.

Authored, but NOT executed here:

- DIMENSIONS FRONT / Checks / Cosmetic collection (non-destructive): exercises real cosmetic selection/purchase methods against an Editor-only memory store. Covers legacy enum/bool migration, existing slots, one-time migration, delayed entitlement recovery, ownership/currency preservation, fixed prices, exact deductions, immediate ownership, duplicate rejection, insufficient funds, per-hero independence, Original, and the unchanged match-bonus snapshot. It restores the live snapshot and hero selection and does not write/delete PlayerPrefs.
- DIMENSIONS FRONT / Checks / Collection layout preview (100 cosmetics, 30 items): draws the production grid with long names and synthetic content; scroll/select/resize without changing player saves. This fixture does not reproduce the full menu navigation or render a hero model.

Unity compile result: unavailable; NOT compiled with Unity or a C# compiler. Unity was NOT run. Android touch scrolling, real font clipping, all six hero/wing combinations, complete menu visual overlap, frame time/GC and 60 FPS are unverified. No screenshots from a Unity runtime were generated.

Required before release: compile in Unity 6000.6, run the checks, inspect the stress window and full menu at 16:9/18:9/20:9 and narrow safe areas, swipe without accidental selections, buy/equip/unequip with a test profile, verify preview wings remain framed, then start/end a match and confirm its locked-in hero bonus. Smoke-test Home, Play, Heroes, all Store categories, Inventory, Profile, Ranking, Settings and Battle Pass. No player save reset is needed.

## Content workflow

A supported new cosmetic needs an enum ID plus one CosmeticCatalog definition; the menu automatically lists it. New meshes/effects still need implementation in the existing visual builders and appropriate reward mapping if a new progression bonus is intended. The catalog reuses existing price constants and WingMatchRewardBonus as the authoritative percentage source rather than duplicating balance values.

The UI rebuilds cached rows on navigation/hero changes and purchase/equip/inventory/entitlement notifications. Idle drawing does not poll PlayerPrefs or instantiate hero models per card. One shared RenderTexture/model preview is reused. Camera fitting scans renderer bounds only when the selected model changes. No blur shader, extra preview particle system or per-card camera was introduced. Performance remains subject to actual profiling.
