# Brakk: smooth thighs and lower legs

Brakk no longer receives generated thigh/shin micro-panels from `HeroLimbTechDetails`.
After footwear is fitted, `BrakkForearmTrim.ApplyLegs` removes the complete imported
leg attachment components: red thigh flaps, shin shells, knee plates, trim,
fasteners and the old rigid waist ring. The thigh flaps belong to Hips in the FBX,
so Hips is included in the attachment selection. The ring is replaced by the
new utility belt. Fabric and skin material surfaces remain in their existing
slots, with the established gold suit treatment and leg rig.

Ankle boots and foot geometry remain. New armor plates are authored as a concept
in `ArtSource/Heroes/BrakkArmor`; they are not reintroduced into the running hero
by this cleanup. The utility belt is added after cleanup.

Selection joins complete attachment surfaces across FBX normal/UV seams and
supports blended leg weights. Meshes are cloned on Brakk's instance only. Nerok
and the shared Rook source asset remain unchanged. The cleanup is idempotent.

Source geometry validation removed 1,648 imported polygon faces from Rook's leg
and hip overlays, preserving all fabric/skin faces and foot geometry. This check
uses the original Blender source; Unity rendered verification remains pending.
