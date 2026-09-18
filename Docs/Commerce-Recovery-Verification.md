# DIMENSIONS FRONT — recovery verification

## Recovered work

Recovered the existing dirty worktree without replacing it. A separate scratch backup preserves the pre-review worktree and Git directory. The remote branch was still `c40fba79cd447846f3e5e556f459ce743b3fa172`; its matching local checkpoint was `b8a031f`. Every file tracked by that local baseline matched the remote blob hash. The local checkout contains checkpoint history rather than the full GitHub history; local and remote commit IDs are consequently different.

Published as fast-forward commits on `dev/vertical-slice`:

- `86f7e11c00cb9c43c87b568bcc01e8e239f89669`: VOIDFALL wings.
- `17ba3081687f05cd8607bc2c60f77bcd97bf66e1`: licensed music and SFX.
- `1574c1a695aa9328a5bc4caf25a96db3533d45a2`: shard commerce and account-session safeguards.

All 101 changed files, including all 31 OGG assets, were verified against GitHub blob hashes after updating the branch. Existing remote files outside this change set were preserved. Temporary conversion output was excluded. Never force-push the local checkpoint history over GitHub; base subsequent API commits on the freshly read remote head, or use a proper remote checkout.

## Additional stability work

The initial recovery now blocks new purchases until purchase fetching completes. Pending tokens are retained until store confirmation. Paid ownership stays separate from offline-earned ownership and is cleared on disconnect; session changes invalidate pending requests and stale automatic equip/acknowledgement. The login integration must explicitly call `Disconnect` at logout. Both commerce services survive scene changes.

HTTP tests exposed malformed payloads returning retryable server errors and unknown products unnecessarily reaching Google. Request shape, SKU and token checks now reject these before Google access. All 11 ledger/HTTP tests pass using isolated SQLite databases and a fake Google transport.

## Validation boundary

- 205 C# files passed tree-sitter syntax parsing, including the guarded Android source. This is not type checking or Unity compilation.
- Relevant IAP APIs were checked against the locally available Unity Purchasing 5.4.3 SDK sources. Unity package resolution and native compilation remain unverified.
- All 31 final OGG files decode in FFmpeg without warnings; continuous timestamps were rebuilt for both music recordings. This is not a listening test.
- No Unity, Android, device purchase, visual/clipping, audio playback or frame-time tests were run.
- Real payments remain unavailable without external login/backend/Play configuration. Refund reconciliation and device recovery tests remain release blockers; see GooglePlay-Shards-Setup.md.
- Paid cosmetics require a fresh authenticated wallet after restart; no insecure cross-account offline ownership cache is written.
