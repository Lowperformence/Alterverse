# Game cleanup audit — 2026-10-03

Scope: scanned all 403 C# source files at 7464c67e6199bd6d3756d831bcb8780c69781443; inspected weapon shop/rendering, match construction, authored hero construction, material ownership and round audio lifecycle. Checked literal Resources.Load paths against the repository tree (none missing) and scene build settings.

## Changes
- The active weapon catalog already contained only two standard-price colorways per hero. Removed the unused 1,100-shard legacy tier constant and renamed its obsolete Legendary editor check to WeaponColorwayChecks. Epic colorways, 50% transparent energy layers, prices and purchase/equip APIs remain. Existing purchases continue to migrate to the surviving colorways.
- Removed the unused, empty WeaponShopVariantSelector and its meta. No source or serialized scene references were found.
- Removed the exact duplicate recovery scene Assets/_Recovery/0.unity and its folder metadata. It is identical to Assets/2222222.unity, which remains; only SampleScene is in the build.
- Match construction now adopts generated scene roots in a finally block and hides/destroys an incomplete world after a failed build. Previously early roster failure or an exception could leave actors, HUDs and scenery outside the match root.
- Repeated authored hero creation reuses the rig instead of duplicating the model and attempting to add duplicate pose-control keys.
- Repeated material tinting reuses copies keyed to the original source material. Previously each tint could copy the preceding copy and allocate another native material.
- Weapon renderer capture deduplicates renderer entries, excludes existing energy layers, and reuses a single clear shield-inscription material per renderer.
- AudioRuntimeReset now binds when the match appears and when the next round begins; its old one-time menu Start lookup could miss every match.
- Removed unused prototype objective/primitive visual builders.
- Updated the Play Mode regression check to cover the actual energy-layer design, original mesh/material preservation, 50% transparency, repeated sync, unequip, rig reuse and tint reuse.

## Validation and limits
Completed: source/reference checks, catalog invariants (14 unique hero/variant pairs, two variants per hero), matching resource paths, balanced delimiters in changed sources, deletion references and merge comparison with GitHub.

Unity Editor and a C# compiler are not installed here. The updated Play Mode regression check has NOT been executed, and device rendering, scene object counts and multiplayer play remain unverified. Run DIMENSIONS FRONT/Checks/Runtime cleanup (Play Mode) plus Weapon colorways (non-destructive) in Unity, then play repeated rounds on multiple maps and reconnect clients. This is a scoped static audit, not a claim that the whole game is bug-free.
