# Einzelne Sprachchat-Spieler im Match stummschalten

Im laufenden Match: Zahnrad öffnen, dann **SPRACHCHAT / STUMM**. Die Liste zeigt die anderen tatsächlich verbundenen Teilnehmer des privaten Gruppen-Sprachchats. Jeder Eintrag hat einen eigenen Stumm-/Freigabe-Button. Die Ansicht ist scrollbar; Zurückkehren ins Match und Verlassen bleiben erreichbar. Das Öffnen der Optionen pausiert kein Online-Match.

`PartyVoiceService` benutzt Vivox 16.12.1 `MutePlayerLocally()` und `UnmutePlayerLocally()`. Die Aktion ist lokal: Sie ändert weder das Mikrofon des anderen Spielers noch dessen Hörbarkeit für Dritte. Spieler werden anhand ihrer Vivox/Unity-PlayerId ausgewählt, nicht anhand veränderlicher oder gleicher Anzeigenamen. Eine Stummschaltung wird in allen aktiven Kanälen auf dieselbe Identität angewendet. Bei erneuter Teilnahme wird die Sitzungseinstellung erneut gesetzt. Beim Wechsel der eigenen Identität, Verlassen/Wechseln der Gruppe und Zerstören des Dienstes werden die gespeicherten Entscheidungen verworfen. Es werden keine permanenten Blockierungen oder Chat-Sanktionen gesetzt.

Die Anzeige liest den bestätigten SDK-Zustand. Noch ausstehende Änderungen sperren nur den betroffenen Button und laufen nach fünf Sekunden in eine sichtbare Wiederholungsmöglichkeit aus. Selbst-, unbekannte und nicht mehr verbundene Ziele sind ausgeschlossen. Die Oberfläche initialisiert weder Vivox noch das Mikrofon automatisch. Das eigene Mikrofon hat einen getrennten expliziten Schalter; Berechtigungs-/Zustimmungs- und App-Wechsel-Regeln gelten weiter.

Zusätzlich wurden falsch dekodierte UTF-8-Sequenzen aus `MainMenuFriends`, `InMatchSettingsMenu` und dem Kauf-Speicherstatus entfernt. Die Geschenkbuttons zeigen beispielsweise **GIFT 50 SHARDS**. Preise, Geschenke, Kontostände und Kaufabläufe wurden dabei nicht geändert.

## Prüfung

- `python Tools/Lobby/Regression/run.py --dotnet-root DOTNET_ROOT`: 57 Editor-, 62 Android-, 31 Server- und 7 Lobby-Einstiegsprüfungen bestanden; vollständige Offline-Lobby-/Picker-Kompilierung ebenfalls bestanden. Enthält echte Produktionsklassen für Sprachdienst und Match-Optionen, individuelle Mute-/Unmute-Klicks, gleichnamige Spieler, Rejoin, Mehrkanal-Stummschaltung, native Bestätigung/Timeout, Kontowechsel, Selbstschutz und Event-Aufräumen. SDK, Audio und GUI sind kontrollierte Ersatzgrenzen.
- Sprach-/Banner-Suite: 311 Android-, 311 Android-Development-, 287 Editor- und 287 Server-Prüfungen bestanden. Die neue Oberfläche hat deutsche, englische, spanische und französische Texte.
- Die verwendeten Teilnehmer-APIs und `LoginOptions.DisplayName` wurden zusätzlich mit der tatsächlichen `Unity.Services.Vivox.api` des Unity-Pakets 16.12.1 abgeglichen.

Nur den Client neu bauen. Unity-/IL2CPP-Build und echte Zwei-Geräte-Sprachübertragung sind hier nicht ausgeführt worden. Im internen Test zwei optierte Gruppenmitglieder verbinden, während eines Matches gegenseitig stumm-/freischalten und eine Neuverbindung prüfen.

Unity-Referenz: https://docs.unity.com/en-us/vivox-unity/developer-guide/muting/mute-other-users
