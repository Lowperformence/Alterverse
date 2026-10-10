# Daily map quest and login streak

- New daily mission: win on five different arena variants, then claim 15 earned shards. Repeated victories on one map count once; defeats do not count. The active match arena is recorded, including shuffled and cooperative arenas.
- Daily login rewards are granted automatically in the visible main menu: days 1–7 award 1, 3, 6, 12, 13, 14, 15 earned shards. Day 8 starts a new cycle. Missing a full Berlin calendar day resets the cycle to day 1. The display derives today's position from the last claim date, so a missed day also resets the visible series before today's automatic grant.
- Both systems use midnight Europe/Berlin, including summer time. Moving the clock behind the stored day pauses new progress/rewards.

## Persistence and security

Rewards and completion markers use the existing absolute-value profile transaction journal. Recovery replays absolute values rather than adding rewards again. Match receipt retries retain the existing duplicate guard. Map masks reject unknown arena bits; counter addition saturates instead of overflowing. Daily claims require current-day completion and an unset claim bit.

The commerce server validates login dates, streak range, value types and map mask bounds. Login markers cannot be removed by an older client's profile upload, moved backwards or changed within the same day. Existing authenticated account isolation and revision checks reject competing stale profile writes. Rewards affect earned shards, never the paid wallet.

These protections do not make client-reported offline progress authoritative. Device-clock manipulation forwards and fabricated wins require a server-owned reward ledger and server-confirmed match results to prevent fully. Deploy the updated commerce server to activate the new server validations; updating the Unity client alone is insufficient.

## Validation

`python -m unittest discover -s Server/commerce -p 'test_*.py'`: 46 tests passed, including account isolation, persistence after reopening, invalid daily values, concurrent profile writes and immutable login markers.

Unity validation entry point: `DIMENSIONS FRONT > Validation > Daily quests and login streak`. It checks distinct maps, losses, invalid map values, daily repeat/clock rollback, missing days, cycle wrap, reward amounts and Berlin date transitions. These checks do not change the player's saved profile.

Unity compilation, editor validation and Android runtime/UI checks were not run in this environment because Unity is unavailable. Before release, run the editor check and test map wins/claims, a repeated claim, login after restarting, account switching, cloud conflicts, all seven days and midnight rollover on a test profile.
