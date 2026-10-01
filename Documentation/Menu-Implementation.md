# Interstellar menu implementation

The menu is the current priority. This commit includes UI and UI assets only; map and figure source changes from the earlier visual-pass branch are excluded.

## Interface

- Orbital Home page with the live selected loadout, profile/XP badge and three individually illustrated cards.
- Left navigation with cached line icons throughout Deployment, Heroes, Store, Inventory, Battle Pass, Profile, Daily Missions, Ranking, Settings and Co-op.
- Direct Battle Pass destination, illustrated season banner, reward cards, premium/free tracks, previews and the existing claim/purchase handlers.
- Weapon shop is reachable again; existing two-variant preview and purchase/equip operations are retained.
- Thin steel frames, dark translucent surfaces, champagne highlights, hover states and short page fades.
- Bundled sci-fi display font derived from Orbitron and renamed Dimensions Display to respect its reserved font name. Rajdhani remains the readable body/button font. The SIL OFL licenses ship with both families.
- Fit/clipping rules contain long labels and wrapped paragraphs within authored rectangles. Settings extension coordinates follow the same safe area; voice volume and quit confirmation occupy separate slots.
- The original hidden manual difficulty controls are removed at their callers instead of suppressing arbitrary text/click targets in the shared theme.

## Validation

C# syntax parsing and git whitespace checks passed. Font binaries are static (no variable axes), parse correctly and contain the required menu glyphs. Asset paths and unique meta GUIDs are checked. Home/navigation/card layouts are checked against landscape and ultrawide safe-area dimensions. Route arrays, Page switch targets and the direct home-card targets are checked in source.

Unity is unavailable in the execution environment. Unity compilation, font/shader/texture import, rendered appearance, touch, purchases, co-op and return-from-match runtime behavior have not been tested here. These remain the runtime acceptance gate. The downloaded headless browser package was invalid, so no browser rendering is claimed either.

## Asset provenance

InterstellarBackdrop.png: built-in image generation, cinematic blue-gray planet/gold rings/orbital city, dark negative space on left, separate live hero on right; no embedded interface or text. A follow-up edit removed unwanted marks at bottom right.

MenuCardAtlas.png: built-in image generation, three separate illustrations: orbital battle, golden seasonal crown/core, blue-violet dimensional portal. Labels and click regions are live menu code; images are cropped with their aspect ratio preserved.

Rajdhani fonts: Indian Type Foundry / google/fonts ofl/rajdhani, Latin/symbol subsets. Dimensions Display: static 600-weight Latin/symbol derivative of google/fonts ofl/orbitron, with internal family/PostScript names changed. See the included OFL.txt and Orbitron-OFL.txt.

## Unity acceptance

Open the real game scene on dev/vertical-slice; check Console, every navigation entry, weapon/cosmetic purchases with sufficient and insufficient shards, equip/unequip, profile rename, all three home cards, Battle Pass claim/premium flows, rankings/auth failure states, settings/voice/quit, Co-op lobby actions, then start and finish a match and return Home. Check long names and landscape safe areas on Android as well as 16:9 and ultrawide desktop.

## In-match interface

The combat HUD, health and monster nameplates, target objective labels, Hot Zone progress, planet event notices and match results use the shared dark glass, champagne frames and bundled typography. Match clock/control panels, onboarding and in-match settings already use the shared theme. Combat ability buttons, their material/shader, artwork, sizes, positions and touch regions are intentionally unchanged, as requested. The joystick is unchanged. No standalone minimap implementation exists in this source tree.
