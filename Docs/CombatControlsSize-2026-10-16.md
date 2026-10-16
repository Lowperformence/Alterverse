# Larger, raised combat controls

The five attack/ability buttons, their hit rectangles, the movement joystick
and its thumb are 25% larger. The attack cluster scales about its original
24-unit bottom/right inset, including its spacing, and moves up by another
32 logical HUD units. The idle joystick also moves up by 32 units. This keeps
the enlarged controls inside the safe area and avoids overlapping attack hit
rectangles. The ultimate artwork scales with its hit rectangle instead of
retaining the old fixed 84-unit size.

Attack artwork and its shader frame use 70% opacity (30% transparency) for every
hero, including atlas icons and separate hero textures. Cooldown numbers and
the ultimate label retain full opacity; their fonts scale with the controls.
The existing shader already multiplies its output alpha by the RawImage tint,
so no shader change or image re-export is required.

Touch and editor mouse movement share the same enlarged joystick radius and
safe-area center clamp. The clamp reserves room for the fully displaced thumb
and the raised bottom inset. Floating movement still starts from the original
touch position, using the existing linear displacement-to-input mapping.
The left-half capture region, aiming sensitivity, cooldowns, cancellation and
attack execution are unchanged. Feedback and optional hold-help follow the top
of the enlarged cluster; compact landscape screens place help beside it.

## Verification

Compiled and evaluated the production CombatHudLayout with minimal Unity math
fixtures at 960x540, 1920x1080, 2400x1080, 1280x720, 1920x1200, 2048x1536 and
1080x2400, including asymmetric and vertical safe-area insets. All five centers
map to their correct input slots in all seven cases; every width/height ratio
is 1.25, with zero attack-rectangle overlaps or controls outside the safe area.
Joystick radius is 92.5, thumb radius 32.5, and icon opacity is 0.70. Source
syntax checks pass for all three modified C# files.

These are layout/math checks, not a Unity rendering or device touch test. Verify
thumb reach, multi-touch movement plus aiming, held basic attacks and cooldown
legibility on the target phone after pulling the changes.
