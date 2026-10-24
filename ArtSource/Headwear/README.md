# Headwear collection

| Model | Inventory / unlock |
| --- | --- |
| BETA Trucker Cap (ID 7) | Premium Battle Pass level 50 only; existing ownership key retained |
| UFO Test Hat (ID 8) | Existing 1-shard purchase; same SKU and ownership key |
| Wizard Hat (ID 9) | Free test hat, immediately available in Inventory → Cosmetics → Skins |
| Propeller Cap (ID 10) | Free test hat, immediately available in Inventory → Cosmetics → Skins |
| Mushroom Hat (ID 11) | Free test hat, immediately available in Inventory → Cosmetics → Skins |

All five hats use the production headwear slot, real-model thumbnails and the same attachment renderer in inventory previews, matches and multiplayer. Each hero keeps a separate selection under the existing account-scoped headwear key. Selecting a hat preserves the equipped wings.

The three free test hats require no purchase request, account-server SKU, ownership migration or shard debit. They do not grant the BETA cap or the paid UFO. Existing Battle Pass rewards, prices and gameplay bonuses are unchanged.

The BETA cap has a structured front, open mesh rear and curved stitched visor. Its thumbnail comes from the production C# mesh. The other thumbnails are rendered from the exact serialized geometry Unity loads. Editable Blender and GLB sources are retained for the three new models.

## Attachment

Only visible, indexed head geometry contributes to fitting. Removed hair/flower vertices, disabled anatomy and hidden pose controls are excluded. Rigid head-bone attachments such as Tahla's helmet contribute their visible surface; floating halos and existing headwear do not. This includes Tahla's halo after `HeadAdornmentSkin.Merge` has merged it into the body renderer: its `DF_TahlaHaloGlow` material submesh is excluded from head measurements. Real skin using `Skin_FineOrnaments` remains included. The halo itself stays visible and unchanged.

The opening surrounds the measured upper crown instead of placing its empty center at the absolute apex. Each hat uses its own inner-cavity profile; a bounded fit finds the smallest enclosing size for the visible crown, with half-percent shell clearance. The resulting mesh uses the real head bone's bind frame and follows head motion. Equipping after an animation has started does not bake an inverse head pose into the hat. Hats add no collider, combat power or progression bonus.

The approved UFO reference is 25% larger than its previous one-third size: `1.25 / 3` of its original automatic fit. On the same measured head, BETA matches that UFO's complete width, Wizard uses 125% of it, and Propeller/Mushroom use 75%. These model-specific adjustments scale all three axes uniformly, preserving each hat's fitted proportions and individual silhouette. Crown insertion depth scales with each model's size about the head apex, keeping the hat at the crown. This shared sizing runs in previews and equipped gameplay. The isolated regression checks the dimensions of all five meshes and reproduces the merged-halo case; real-hero clipping still needs the Unity visual check below.

## Multiplayer compatibility

V2 headwear-only messages carry all five cosmetic IDs. Updated hosts downgrade newer hats to Original for V1 peers, preserving their existing BETA/UFO rendering and packet compatibility. An updated client probes V2 at most three times on legacy hosts, then keeps the V1 behavior. Validated modern state cannot be erased by a delayed legacy fallback.

Seeing the three new models on other players requires an updated match host and clients. Older binaries cannot display models they do not contain. Existing movement and combat messages are unchanged.

## Checks

    python Tools/Headwear/Regression/run.py --dotnet-root /path/to/dotnet
    python Tools/Headwear/validate_assets.py

The isolated .NET suite covers all five hats, free inventory availability, premium BETA restrictions, the unchanged 1-shard UFO purchase, per-hero equip and save/reload, separate wing slots, model-thumbnail caching, visible helmet fitting, removed hair vertices, and mixed V1/V2 peers. It also compiles the actual headwear editor tools against compile-only stand-ins. These fixtures do not simulate or certify Unity rendering.

For the real Unity check, open **DIMENSIONS FRONT → Headwear → Fitting studio**. Rotate each of the ten heroes with each hat, tilt the head and enable wings. **Check all 5 hats on all 10 heroes** runs 50 structural combinations against the loaded hero models.

Real-hero visual clipping, idle/running/attack poses and Android rendering must still be verified in Unity/on device. A passing isolated fixture is not that verification.

## Rebuild art

Use Blender 5.0.1 and .NET 8, from the repository root:

    blender --background --python ArtSource/Headwear/create_collection.py -- --root .
    blender --background --python ArtSource/Headwear/BetaCap/render_preview.py -- --source-root . --dotnet-root /path/to/dotnet --output Assets/Alterverse/Resources/Headwear/BetaCapIcon.png


