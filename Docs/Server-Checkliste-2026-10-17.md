# Server-Checkliste vor dem nächsten Build

Stand: 17.10.2026. Zielversion: `v0.1.5-server` und passende Android-App vom selben Quellstand.

**Ergebnis:** Die hier möglichen Code-, Build-Konfigurations- und Ablaufprüfungen sind bestanden. Eine Freigabe für das neue Server-Image oder den Android-Build ist damit noch nicht gegeben. Unity, Docker-Image und echtes Handy müssen anschließend geprüft werden.

## Abgearbeitet

| Prüfung | Ergebnis und Nachweis |
| --- | --- |
| Live-Matchmaker erreichbar | Am 17.10. erneut im Dashboard geprüft: `dimensions-front-live`, ONLINE / Healthy. |
| Tatsächlich verwendete Server-Version | Gespeicherte aktuelle Matchmaker-Konfiguration verweist auf `dimensions-front / v0.1.4-server`. Die neuen VFX-Änderungen laufen dort noch nicht. |
| Container und Port | Version und Image-Tag beide `v0.1.4-server`, Repository `dimensions-front-yf4x25e6aj2i/dimensions-front-server`, `7777/UDP` als `gameport`. Active Caching zeigt Good. Keine Einstellungen verändert. |
| Match mit Bots startet | Zuvor gelesener Log von `ab39476506cf`: `authoritative online match started with 1 humans and bot fill.` Er belegt den Start dieser alten Version, keinen Test der neuen. Bei der jetzigen Nachprüfung ist das Deployment beendet (Terminated), die Container-Logs sind abgelaufen; sie lassen sich dort nicht erneut abrufen. |
| Dedicated-Build und Shader | 37 Prüfungen bestanden: Linux-Server-Subtarget, Shader-Erhalt, Headless-Start, UDP-Port und Wiederherstellung der Editor-Einstellungen auch nach Fehlern. Editor-Ersatzobjekte, kein nativer Unity-Build. |
| Anmeldung, Suche und erneuter Versuch | Lobby/Ranked-Suite: 1.932 Prüfungen bestanden, einschließlich Cleanup, zugewiesener alter Gruppen und Solo-Neuversuch ohne veraltete Relay-Bereitschaft. |
| Position, HP, KDA und Heilobjekte | 166 Prüfungen bestanden: unter anderem Owner-Reconciliation, Snapshot-Übertragung, Scoreboard und Heilobjekt-Verfügbarkeit. Die echte Geräte-Latenz ist damit nicht gemessen. |
| Gewöhnliche Online-Angriffseffekte | Fix aus `4a12a6e`: echte gemeinsame VFX-Fassade wird vom Server an Clients übertragen. 1.128 kontrollierte VFX-/Kampfbefehls-Prüfungen bestanden; siehe `OnlineVfx-2026-10-17.md`. |
| Schaden ohne Servergrafik | Dieselbe Suite prüft bei allen zehn Helden Basic und Q mit tatsächlichen Befehls-/Delivery-Quellen und kontrollierten Health-/Physics-Objekten. HP/Shield sinken, menschliche Schadenszähler steigen, VFX-Replay erzeugt keinen zusätzlichen Schaden. Der gemeldete fehlende Schaden am echten Handy ist weiterhin nicht belegt oder ausgeschlossen. |
| Bestehende Skill-Treffer | Vorherige abgeschlossene Prüfungen desselben unveränderten Kampfcodes: 284 Nerok- und 1.245 Entry-/Kampfprüfungen. |
| Online-Ladebildschirm | Ursache gefunden und ergänzt: Online ging zuvor direkt von der Serverfreigabe in `OnlineMatchRuntime.Enter`, ohne den lokalen Menü-Ladebildschirm. Jetzt erscheint nach Zuweisung eine eigene Anzeige, dann die Spielfeld-Vorbereitung. Keine erfundenen Prozentwerte. |
| Ladeanzeige und Fehlerausgänge | 27 neue Prüfungen bestanden: ein UI-Frame vor synchronem Client-Aufbau, kein vorzeitiger automatischer Bootstrap während Ladeanzeige oder Exit-Cleanup, Anzeige bis zur tatsächlichen Kamera-Freigabe, Disconnect, abgebrochener/ersetzter Plan, Aufbaufehler, verschwundene Arena, Menü-Rückkehr und erneuter Match-Aufbau. Dedicated-Aufbau bleibt sofort und meldet Fehler synchron. UI/Netzwerk-/Arena-Ersatzobjekte, kein Android-Rendering. |
| Automatische Prüfungen | Die bisherigen beiden GitHub-Workflows für `4a12a6e` waren grün. Die neue Lade-Suite ist zusätzlich im Ranked/Account-Workflow eingetragen. Vor dem Build müssen die Checks des neuen Quellstands ebenfalls grün sein. |

## Vor Upload und Freigabe noch offen

- [ ] Den endgültigen Stand von `dev/vertical-slice` ziehen; Server und Android daraus bauen. `4a12a6e` allein enthält den neuen Ladebildschirm noch nicht.
- [ ] Native Unity-Kompilierung ohne Fehler; Linux Dedicated Server mit dem vorhandenen Menüeintrag bauen.
- [ ] Neues Docker-Image als `v0.1.5-server` bauen und pushen. Erfolgreichen Push/Digest prüfen.
- [ ] Edgegap-Version mit **Name UND Image-Tag** `v0.1.5-server` prüfen. `7777/UDP`, Registry und Repository beibehalten.
- [ ] Passende Android-App bauen und auf dem Testgerät installieren. Bei Play-Upload Version Code erhöhen.
- [ ] Erst bei vorhandenem Image und passender Test-App den Live-Matchmaker auf `v0.1.5-server` umstellen, neu laden und gespeicherte Konfiguration/ONLINE prüfen.
- [ ] Neue Suche erzeugt ein Deployment der Version `v0.1.5-server`; kein altes Match weiterverwenden.
- [ ] Am Handy Ladeanzeige beim Verbinden, Spielfeld-Freigabe und Rückkehr nach Abbruch/Fehler prüfen.
- [ ] Basic/Q/W/E/Ultimate sichtbar; echte Treffer reduzieren HP oder Schild. Im neuen Serverlog menschliche angenommene Befehle und `humanHealthAndShieldRemoved` prüfen, Bot-Schaden allein reicht nicht.
- [ ] Danebenschießen, Deckung, Tod/Respawn, Position am Heilobjekt, Lebensbalken und KDA nach Kill prüfen.
- [ ] Suche abbrechen und erneut suchen; Match verlassen und erneut suchen; danach Offline-Match prüfen.

**Keine Gesamtfreigabe vor diesen Geräteprüfungen.** Hier ist keine Unity-Installation vorhanden. Quellcode- und kontrollierte C#-Tests ersetzen weder einen nativen Serverstart des neuen Images noch einen echten Android-/UDP-Test.

## Ladebildschirm: technische Änderung

`OnlineMatchLoadingScreen` ist unabhängig vom sichtbaren Menü und der offenen Party. Bei aktiver Suche mit Serverzuweisung zeigt er den Verbindungsstatus. Nach verifiziertem Launch blendet der Client die Vorbereitung ein und wartet einen Frame vor dem synchronen Spielfeldaufbau. Die bereits vorhandene Kamera-Freigabe in `PrototypeBootstrap.LateUpdate` beendet die Anzeige erst nach Arena-Initialisierung. `MatchExit` und Planabbruch räumen sie auf. Bestehende Verbindungs-/Start-Timeouts bleiben zuständig. Der Dedicated Server wartet nicht auf einen UI-Frame.
