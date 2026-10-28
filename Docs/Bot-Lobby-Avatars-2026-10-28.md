# Bot lobby names, replacement and avatars

Casual and private co-op lobby team cards now show a complete roster immediately.
Real service members take priority over bots. Joining with an invitation replaces a bot
card; leaving restores a bot. Bots are roster occupants rather than fake Unity session
members, so they do not consume invitation capacity, add network connections, enter
Edgegap groups or affect human readiness. Matchmaking can replace remaining bot slots
with its assigned humans before the authoritative roster freezes. There is no new
mid-match joining or takeover rule. Ranked still requires eight humans.

The bot display pool contains 64 curated handles without numeric suffixes, such as
Kairo, MoonDrift and PixelPanda. Bot IDs remain internal. Names avoid collisions with
human names and other bots, use deterministic lobby seeds and survive match-plan
serialization. Lobby seeds travel in optional dedicated hello metadata. If matchmaking
combines several private parties on a team, the server chooses their seed deterministically;
the final server roster takes priority. Old clients that cannot retain display aliases
continue to receive legacy-compatible numeric labels in mixed-version dedicated matches.
Offline scoreboard bots and replacements between co-op rounds also use the new aliases.

Lobby cards use the existing AvatarBadgeRenderer and the equipped in-game profile avatar.
Member-visible avatar properties accompany create/join, refresh while the lobby is editable,
and default to the starter avatar when absent or malformed. Remote avatar display does not
consult the viewer's inventory or grant cosmetic ownership. Bots use stable starter icons.
Optional avatar fields also round-trip through dedicated hello/plan messages. These cosmetic
fields are not part of the gameplay fingerprint and do not change matchmaking permissions.
The roster preview is cached between service changes to avoid rebuilding it for every
IMGUI paint event. Hero conflicts leave the preview visible and still block match staging.

## Validation and rollout

`Tools/Ranked/Regression/run.py --dotnet-root <.NET 8 installation>` compiles production
lobby, roster, handshake and lifecycle code against controlled engine/service substitutes.
The suite includes actual join/leave events for one through four humans, collision-free
aliases, cached snapshots, readiness/capacity, hero conflicts, avatar refresh/retry, seeded
server roster names, optional avatar round-trips and old hello/plan compatibility.
C# syntax validation covers all changed runtime and regression files.

No Unity Editor, Android device or live Edgegap server was available here. Build both the
Android client and Linux dedicated server from this commit, publish the new server image,
and point matchmaking to that version. Verify real invite joins/leaves, equipped avatar
updates and visual card layout on devices, then finish a match and repeat matchmaking.
Existing installed clients/server images are unchanged until rebuilt and released.
