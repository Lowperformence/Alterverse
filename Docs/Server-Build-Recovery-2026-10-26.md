# Play Games settings collision during server build

The native build screenshot on 2026-10-26 reports `Both original and held
PlayGamesSettings assets exist` from the dedicated server builder's restoration
step. That error can occur when an Editor/plugin import recreates the Resources
asset while the original is temporarily held under Editor for a Linux build.
The screenshot alone does not establish whether the player build itself succeeded.

Recovery now archives both assets and their `.meta` files under a unique
`Builds/ServerBuildRecovery/<timestamp>-<id>/` folder before moving either file.
The copies are verified byte for byte. `HeldOriginal.asset` is the configuration
held before building; `ResourceCopy.asset` is the displaced Resources copy.
The held original is restored to its normal Resources path with its existing GUID.
Import callbacks are paused only during the recovery moves and always resumed.
They are not paused while building the player.

If restoring the held original fails, recovery puts the displaced copy back when
the destination is empty. A failed rollback leaves the assets recoverable under
Editor and in the archive. Failed backup creation stops recovery before any asset
is moved. Cleanup removes only the verified, archived duplicate after the original
has been restored. A cleanup failure keeps that extra copy under Editor.
Temporary/quarantined assets and build archives remain outside version control.

On reopening Unity, the existing startup hook attempts recovery automatically.
Then retry `Tools > Dimensions Front > Build Linux Dedicated Server`. Require the
new `DIMENSIONS FRONT SERVER BUILD READY` message and inspect the newly generated
`Builds/DedicatedServer/Linux` output before uploading an image. An old executable
in that folder is not evidence that the failed attempt completed.

Validation: 220 controlled checks compile the production builder and exercise
normal builds, native exceptions, failed build reports, settings regeneration,
startup recovery, distinct GUID preservation, failed backup/move/restore/rollback,
cleanup failure and idempotent recovery. These are Editor substitutes; the next
native Unity build and server startup still need to be verified.
