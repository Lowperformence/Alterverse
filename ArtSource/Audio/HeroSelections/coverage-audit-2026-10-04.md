# Hero sound coverage audit — 2026-10-04

Scope: eight heroes, basic attack plus Q/W/E/R (40 action slots). Repository resources and code dispatch checked; Unity playback was not available. MP3 source recordings may be imported as OGG or WAV, so the runtime extension alone does not establish provenance.

All 40 action slots have a non-empty primary resource. Anchor Recall now uses the supplied chain MP3 at Q_Cast. Nerok E previously had both E_Cast.wav and E_Cast.ogg under the same Resources key; the legacy WAV is removed and its importer GUID retained for the selected OGG.

| Hero | Basic | Q | W | E | R |
|---|---|---|---|---|---|
| Veyr / Zhaev | Legacy | Selected | Selected | Selected | Selected ultimate |
| Kaelon / Auren | Legacy | Selected | Selected: shared shield | Selected | Selected ultimate |
| Nyra / Sivra | Legacy | Selected | Selected: shared dash | Selected | Selected ultimate |
| Elyra / Ione | Legacy | Selected | Selected | Selected | Selected ultimate |
| Mira / Tahla | Legacy | Selected | Selected | Selected | Selected ultimate |
| Rook / Brakk | Legacy | Selected: Boom Swoosh | Selected: shared shield | Selected | Selected ultimate |
| Traveler | Legacy | Selected: requested dash sound | Selected | Selected | Selected ultimate |
| Nerok | Selected quiet hit | Selected: Anchor Recall | Selected: shared shield | Selected | Selected: Maelstrom |

32 of 32 ability slots use selected recordings. Seven basic attacks retain their earlier sounds; Nerok already has his selected quiet hit. The original selection manifest maps 17 ability slots; Anchor Recall and Maelstrom add two, and the separate ultimate importer maps seven more. Earlier selected ultimate sources include WAV recordings as well as MP3.

SelectedHeroAudioRegression now checks loaded clips for empty data and rejects duplicate Resources keys. Its 25 selected complete-cue mappings exclude the earlier seven ultimate mappings, which retain their existing layered audio behavior. This audit does not claim every action uses an MP3 or that every recording has been listened to in Unity.

Update: five additional shield/dash mappings reuse the existing selected recordings. Traveler Q legacy impact/windup assets are removed.

Final Brakk update: Wreckstorm uses Cinematic Hit Shot; Wreck Swing uses Boom Swoosh (exact source hashes in brakk-recordings.json). Maelstrom playback gain is multiplied by 0.80 for both local and remote playback.
