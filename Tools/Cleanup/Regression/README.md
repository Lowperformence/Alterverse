# Lifecycle regression without Unity

```sh
python -m pip install tree-sitter tree-sitter-c-sharp
python Tools/Cleanup/Regression/run.py --mono-root /path/to/extracted-mono
```

Uses the production Bootstrap orchestration methods, MainMenu `EnterMatch`, full
`MatchExit`, `MatchClock`, `BaseRecall`, both respawn components, `DimensionsFront`,
co-op plan/session/slot logic, daily calendar/streak, `LocalHeroContext`, full match
audio director, music result method and Planet HUD resolution method.

The scene graph, coroutine stepping, component discovery, network session owners,
audio output, profile store and geometry/hero construction are substitutes. The
runner extracts specified methods through C# syntax nodes and spells selected
target-typed constructors explicitly for Mono. It never rewrites their control
flow. It does not load actual imported rigs, execute Unity callbacks or physics,
run a live Google checkout, verify all shader variants, or measure Android cost.

Scenarios: injected arena/hero/roster failures; menu failure/retry; 30 world restarts
and duplicate entry; direct online launch failure; asynchronous leave barrier;
final-frame and pause/resume recall interruption; successful recall and shielded
damage; paused/disabled manual respawn; normal and zero-delay paused bot respawn;
all 64 pause masks; timer/overtime/sudden death/result; private/online next-round
rosters and every distinct pair of the eight heroes; timezone-backed daily
boundaries; guest/host/solo local ownership, results and buffs.

`--source-root` can point to an older source revision as a negative control. New
`LocalHeroContext` is taken from the current checkout while older consumers run
unchanged; this proves their old respawn-based selection ignores local ownership.

See `Documentation/Clean-Game-Audit-2026-10-09.md` for outcomes and remaining Unity/
Android checks. `Tools/Cleanup/audit_repository.py` performs the separate read-only
source/resource/serialized-asset inventory from a Git tree plus GUID snapshot.
