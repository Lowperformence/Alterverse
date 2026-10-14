# Movement-direction dash taps

All six current dash/leap abilities use the current camera-relative movement
input on a button or keyboard tap. This takes precedence over attack-facing and
remembered manual aim. When movement input is neutral, the last movement direction
is used. Dragging a skill button past its aim dead zone explicitly overrides it.
The gesture preview and cast resolve the same intent at release.

Movement taps do not capture a cone target for Timmy's Scratch leap. Its manual
drag targeting and existing scripted/bot casts retain their capture behavior.
Distance, cooldowns, collision handling and combat timing are unchanged.

`AimPose.AllowDashTargetCapture` survives cast clamping and is sent as an optional
trailing V5 command field. New hosts accept older V5 commands with capture enabled.
Use the updated build on both client and host for movement-tap leap behavior;
older hosts ignore the trailing field.

Validation: production input, aiming, ability and network code compiled against
deterministic engine fixtures; `--movement-dashes` passes 782 assertions covering
all dash slots, current input versus facing, release-time input, idle fallback,
manual override, remembered aim, touch control, Timmy and remote host execution.
The full pre-existing suite fails at `nearest collision gets hit` (980 versus
978 HP), identically on the unchanged parent commit. No Unity Editor/device test
was available here.
