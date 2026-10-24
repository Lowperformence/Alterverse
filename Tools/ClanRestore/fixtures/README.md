# Historical test input, not a deployment artifact

`node-2026-10-22.2.js` is the retained Node account-source export dated 22 October, version 2026-10-22.2 (53,924 bytes). `package.json` is its retained Functions Framework dependency manifest from the account-server update archive. Neither contains a credential.

This fixture exercises the actual purchase, profile and authentication code, with network/Firestore substitutes. It is deliberately unchanged, including its old catalog. To prepare deployment, `build_release.py` requires a fresh export of the actual current Node index and package. Never deploy this historical fixture or replace a newer live catalog with it.
