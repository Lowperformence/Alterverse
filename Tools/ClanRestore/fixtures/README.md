# Historical test input, not a deployment artifact

`node-2026-10-22.2.js` is the retained Node account-source export dated 22 October, version 2026-10-22.2 (53,924 bytes). `package.json` is its retained Functions Framework dependency manifest from the account-server update archive. Neither contains a credential.

This fixture exercises the original purchase, profile and authentication code with network/Firestore substitutes. It retains its historical catalog. Generate the standalone release with Server/commerce/node/attach-standalone-clans.mjs using a fresh live export; do not use a fixture as a replacement for a newer catalog.

## Cloud Console export supplied on 24 October 2026

`node-2026-10-22.2-cloud-export.js` is the exact Node source from the operator's
`services_dimensions-front-purchase-server_1792759012.950000 (1).zip` export.
SHA-256: `162bbdc85e02192d5b30240cc9acc1656545eea7f8cc6bcf8774c221e1921c88`.
Its compact formatting is tested alongside the earlier formatted source.
This is a fixed regression fixture, not a substitute for checking the current
live export before a later rollout. It contains no cloud credentials.
