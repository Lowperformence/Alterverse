# Nerok attack regression

Run from the repository root with Python 3 and a .NET 8 SDK:

```bash
python3 Tools/Combat/NerokRegression/run.py --dotnet-root /path/to/dotnet
```

The runner compiles current production combat, all ability execution partials, manual aiming,
the range preview overlay and the V5 gameplay command handler. It reuses the existing aiming
engine fixtures and Void Knight presentation fixtures. Production sources are not rewritten.
The fixture's downward-facing `LookRotation` honors the supplied planar up vector so preview
orientation can be checked. Physics, movement, Health, VFX and transport remain substitutes.

284 assertions cover:

- Hook capture at both 70-degree cone edges and the 6.2m range edge at 30/60/120 FPS.
- A real swept hook contact beside cover that previously caused no damage because a second
  centre sightline rejected it. The pull path still cannot cross that cover.
- The held hook's faint selection cone and narrow .35m flight path, correct selected-point
  orientation, first-body/wall endpoints and release/cancel cleanup. Other heroes retain their
  existing previews; Pressure Hull has no targeting geometry.
- One hit, first enemy, allies, passive shield, minions, damage gates, cooldown, dodging a frozen
  shot, stun before/after release and bot execution through the same delivery.
- Basic attack windup/damage, Pressure Hull shield/speed, Breaker Wave radius/slow/multiple
  targets, and Maelstrom's warning/radius/six damage ticks. Allied and outside targets are excluded.
- Production V5 client-to-host command routing preserving a captured cone-edge hook after
  caster movement, using an in-memory packet rather than a real connection.

The original source fails the cover-contact test (`1000` HP instead of `972`). The correction
passes all cases. These checks do not constitute a Unity build, GPU/shader render, native
CharacterController test or live client/server playtest. See the manual checks in
`Docs/Nerok-Hook-Audit-2026-10-16.md`.
