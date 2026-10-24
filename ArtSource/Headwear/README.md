# Headwear collection

| Model | Inventory / unlock |
| --- | --- |
| BETA Trucker Cap (ID 7) | Premium Battle Pass level 50 only; existing ownership key retained |
| UFO CRUISER (ID 8) | 100 shards in Store → Cosmetics → Hats; same SKU and ownership key |
| ASTRAL WIZARD (ID 9) | 100 shards in Store → Cosmetics → Hats; equip after purchase in Inventory → Cosmetics → Hats |
| SKY SPINNER (ID 10) | 100 shards in Store → Cosmetics → Hats; equip after purchase in Inventory → Cosmetics → Hats |
| COSMIC MUSHROOM (ID 11) | 100 shards in Store → Cosmetics → Hats; equip after purchase in Inventory → Cosmetics → Hats |

All five hats use the production headwear slot, real-model thumbnails and the same attachment renderer in inventory previews, matches and multiplayer. Each hero keeps a separate selection under the existing account-scoped headwear key. Selecting a hat preserves the equipped wings.

The four shop hats each cost 100 shards and use the existing transactional account purchase path. Each has a stable SKU and ownership key in both CosmeticCatalog and Commerce/ShardCatalog.json. Existing ownership flags are retained; previously automatic prototype availability is no longer an ownership grant. The BETA cap remains exclusive to the premium Battle Pass. Purchase unlocks the item for the account; equipping remains a separate per-hero inventory action.

The BETA cap has a structured front, open mesh rear and curved stitched visor. Its thumbnail comes from the production C# mesh. The other thumbnails are rendered from the exact serialized geometry Unity loads. Editable Blender and GLB sources are retained for the three new models.

## Attachment

Only visible, indexed head geometry contributes to fitting. Removed hair/flower vertices, disabled anatomy and hidden pose controls are excluded. Rigid head-bone attachments such as Tahla's helmet contribute their visible surface; floating halos and existing headwear do not. This includes Tahla's halo after `HeadAdornmentSkin.Merge` has merged it into the body renderer: its `DF_TahlaHaloGlow` material submesh is excluded from head measurements. Real skin using `Skin_FineOrnaments` remains included. The halo itself stays visible and unchanged.

The opening surrounds the measured upper crown instead of placing its empty center at the absolute apex. Each hat uses its own inner-cavity profile; a bounded fit finds the smallest enclosing size for the visible crown, with half-percent shell clearance. The resulting mesh uses the real head bone's bind frame and follows head motion. Equipping after an animation has started does not bake an inverse head pose into the hat. Hats add no collider, combat power or progression bonus.

The approved UFO reference is 25% larger than its previous one-third size: `1.25 / 3` of its original automatic fit. The latest size adjustment adds 15% to all five hats. On the same measured head, BETA matches that UFO's complete width, Wizard uses 125% of it, and Propeller/Mushroom use 75%. These model-specific adjustments scale all three axes uniformly, preserving each hat's fitted proportions and individual silhouette. Crown insertion depth scales with each model's size about the head apex, keeping the hat at the crown. This shared sizing runs in previews and equipped gameplay. The isolated regression checks the dimensions of all five meshes and reproduces the merged-halo case; real-hero clipping still needs the Unity visual check below.

## Multiplayer compatibility

V2 headwear-only messages carry all five cosmetic IDs. Updated hosts downgrade newer hats to Original for V1 peers, preserving their existing BETA/UFO rendering and packet compatibility. An updated client probes V2 at most three times on legacy hosts, then keeps the V1 behavior. Validated modern state cannot be erased by a delayed legacy fallback.

Seeing the three new models on other players requires an updated match host and clients. Older binaries cannot display models they do not contain. Existing movement and combat messages are unchanged.

## Checks

    python Tools/Headwear/Regression/run.py --dotnet-root /path/to/dotnet
    python Tools/Headwear/validate_assets.py

The isolated .NET suite covers all five hats, 100-shard shop purchases, insufficient balances, duplicate purchases, client/server SKU and ownership contracts, premium BETA restrictions, per-hero equip and save/reload, separate wing slots, model-thumbnail caching, visible helmet fitting, removed hair vertices, and mixed V1/V2 peers. It also compiles the actual headwear editor tools against compile-only stand-ins. These fixtures do not simulate or certify Unity rendering.

For the real Unity check, open **DIMENSIONS FRONT → Headwear → Fitting studio**. Rotate each of the ten heroes with each hat, tilt the head and enable wings. **Check all 5 hats on all 10 heroes** runs 50 structural combinations against the loaded hero models.

Real-hero visual clipping, idle/running/attack poses and Android rendering must still be verified in Unity/on device. A passing isolated fixture is not that verification.

## Rebuild art

Use Blender 5.0.1 and .NET 8, from the repository root:

    blender --background --python ArtSource/Headwear/create_collection.py -- --root .
    blender --background --python ArtSource/Headwear/BetaCap/render_preview.py -- --source-root . --dotnet-root /path/to/dotnet --output Assets/Alterverse/Resources/Headwear/BetaCapIcon.png



## Adding more hats

Add a stable cosmetic ID and one headwear definition in CosmeticCatalog with its price, ownership key, purchase SKU and resource name. The existing scrollable Hats grid and inventory enumerate this catalog, so they need no new per-hat UI code or fixed number of slots. Resource lookup, thumbnails and the serialized loader also use this metadata. Supply the real model/icon, an appropriate inner-cavity fit profile, and a matching entry in Commerce/ShardCatalog.json. Keep existing numeric IDs, SKUs and ownership keys stable.

The Python commerce service reads ShardCatalog.json. A Node Cloud Run service using inline ITEMS needs its catalog updated too: run `node Server/commerce/node/enable-ufo-test-hat.mjs /path/to/current/index.js` to check, then add `--write` to apply only the catalog entries with a backup. This script configures all four shop hats from the shared catalog, upgrades only the explicitly superseded 1-shard UFO, and rejects unexpected prices/ownership layouts. It never deploys. Deploy the current service source with this catalog change before testing purchases against that Node service; do not replace a current service with an old source snapshot.

Shop and inventory expose only WINGS and HATS collection tabs. The Original/body card is not mixed into either accessory collection. Selecting a tab chooses an item from that category, preserving a valid selection or preferring the equipped item; empty owned categories show their own empty state. There is no combined All view or fixed limit on future hat entries.
