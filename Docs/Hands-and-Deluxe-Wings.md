# Hands and Deluxe Wings

Hands are reduced from the previous 200% setting: six heroes use 160% of the original model size (20% smaller than the previous setting), while Brakk Orun uses 80% (60% smaller). This also resizes Brakk's hand-weighted gauntlets. Scaling applies after appearance replacement and before retarget setup. WeaponGrip child bones counter-scale to keep original weapon dimensions. Grip positions follow the resized palm contacts; rifle support uses the adjusted palm.

All cosmetic wing builders use a shared 0.70 size multiplier. This applies to VOIDFALL, Beta Supporter, Plague and Deluxe Wings in gameplay and menu previews.

Deluxe Wings are paired silver crystal wings with layered, flat triangular facets and sparse fluorescent white accents, following the supplied reference. Geometry is generated in the existing wing presentation: 28 closed crystals per side, double-sided flat normals, two renderers, one shared 1344-vertex mesh, three owned materials, no colliders, lights or particles.

Catalog ID 4 preserves IDs 0–3. Price: 20,000 shards. Persistent account ownership: DF_SkinOwned_DeluxeWings. Purchase uses the existing atomic wallet/ownership journal; equip is explicit and independently persisted per hero. The collection shop, inventory and 3D preview use the same definition.

Bonus follows the existing price ratio: 2,000 / 5%, 4,000 / 10%, 8,000 / 20%, hence 20,000 / 50%. Equipped Deluxe Wings grant +50% match XP and +50% match shards through the existing non-stacking match snapshot. Match bonuses do not modify purchased currency.

Validation: C# syntax and grip/contact matrix checks performed outside Unity. The existing non-destructive editor collection check now includes exact-balance purchase, insufficient balance, duplicate purchase, every hero equip, profile reload, independent unequip and 50% match snapshot cases. Unity Editor/device visual and interactive checks remain pending.

