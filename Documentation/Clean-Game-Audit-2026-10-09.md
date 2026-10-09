# Fehlersuche und Bereinigung — 9. Oktober 2026

Basis: `dev/vertical-slice`, Commit `52bd6bbd9f21d64dfadaef7d63b546061a72b124`.

Die folgenden Abläufe werden getrennt als Quellenprüfung, isolierter ausführbarer
Test oder echter Unity-/Gerätetest erfasst. Ein bestandener isolierter Test ersetzt
keinen vollständigen Unity-Build oder ein durchgespieltes Android-Match.

## Checkliste

| Ablauf | Prüfung | Stand |
|---|---|---|
| Kaltstart und Menü | Bootstrap, Pause-Besitz, Profilwiederherstellung, Eingaben | Quellen geprüft; Unity offen |
| Helden und Ausrüstung | acht Helden, Vorschau, Auswahl, Besitz, Waffen | Quellen geprüft; Darstellung in Unity offen |
| Karten | alle elf Varianten, Besitz, Shuffle, Aufbau, Fehlerpfad | Quellen geprüft; Aufbau in Unity offen |
| Matchstart | lokaler/Online-Aufbau, Teambesetzung, Kamera, Countdown | Fehlerpfade isoliert bestanden; echter Aufbau/Countdown in Unity offen |
| Bewegung und Kampf | Bewegung, Grundangriff, Fähigkeiten, Stun, Abbruch bei Tod | Quellen geprüft; bestehende Oberkörper-Regression bestanden; echte Matches offen |
| Recall | Bewegung, Schaden, Tod, Pause, letzte Kanal-Frame | Korrektur und isolierte Regression bestanden |
| Tod/Respawn | Verzögerung, manuelle Bestätigung, Bot, Pause, Renderer/Collider | Isolierte Regression bestanden; Unity-Lebenszyklus offen |
| Ziele und Ergebnis | Hot Zone, Planeten, Bomber, Overtime, Punkte, Belohnungsbeleg | Quellen geprüft; Timer/Overtime/Sudden Death/Ergebnis isoliert bestanden; echte Ziele offen |
| Folgerunde | frische Match-ID, Teams, Spielerabgang, World-Cleanup | 30 isolierte World-Neustarts und private/Online-Roster bestanden; echte Verbindung offen |
| Match verlassen | Netzwerk-/Lobby-Aufräumen, Menü, unabhängige Pausen | Busy-Barriere und alle 64 Pause-Kombinationen isoliert bestanden |
| Profil und Shop | Journal, Tageswechsel, Kaufabbruch, Recovery, Kontowechsel | 98 Server-Tests bestanden; echter Play-Checkout/Backend offen |
| Audio/VFX | ausgewählte Aufnahmen, Pause, Budget, Pool-Grenzen | VFX-Regression und lokale Spielerzuordnung bestanden; Hören/Android-GPU offen |
| Altlasten/Integrität | gesamte C#-Inventur, veraltete Prüfannahmen, Ressourcen, Pakete | Inventur abgeschlossen; verbleibende Unity-Import-/Paketprüfung unten erfasst |

## Belegte Korrekturen

- Fehlgeschlagener Arena-Aufbau meldet Misserfolg, räumt neu erzeugte Objekte auf
  und verwendet die vorhandene Rückkehr-/Session-Bereinigung. Das Menü wird vor
  dem Aufbau versteckt, damit eine synchrone Fehler-Rückkehr nicht wieder
  ausgeblendet wird. Ein direkter Online-Start meldet einen Baufehler als `false`.
- Recall prüft Bewegung und Tod nochmals vor dem Teleport, nachdem die letzte
  Kanal-Frame beziehungsweise eine Pause beendet wurde.
- Manuelles Respawn ist in Pause oder auf einem deaktivierten Actor gesperrt.
  Ein Bot wartet auch bei sofort bereitem beziehungsweise mit null Verzögerung
  konfiguriertem Countdown vor dem tatsächlichen Respawn auf laufende Spielzeit.
- `HeroRespawn` ist kein Beleg für lokale Spielerhoheit: Gäste verwenden Replikate,
  während der Host fremde Menschen mit `HeroRespawn` simuliert. Die neue gemeinsame
  Zuordnung berücksichtigt den lokalen Co-op-Slot. Ergebnis-Audio/-Musikstatus und
  Planeten-HUD wählen dadurch den eigenen Spieler; die eigenen Skins, Waffenfarben
  und Founder-Markierungen erscheinen auf dem eigenen Actor statt fremden Menschen.
  Vor der Slot-Zuweisung bleibt die bestehende lokale `BasicAttack`-Erkennung erhalten.
- Der Traveler-Prüfpunkt verwendet das vollständige Helden-Enum statt der
  überholten Zahl sieben; der Colorway-Test meldet die tatsächliche Anzahl.
- Der Server-Test für Battle Items verwendete alte lesbare Operations-IDs. Die
  Fixture verwendet jetzt die bestehenden 32-stelligen Hex-IDs, einschließlich
  identischer ID beim Retry. Die Produktionsvalidierung und Preise bleiben erhalten.
- Zehn vorhandene C#-Dateien erhalten stabile `.meta`-GUIDs. Der unreferenzierte
  Metadateneintrag des nicht mehr vorhandenen Ordners `Scripts/System` entfällt.
  Der wirkungslose `ControlChanged`-Audio-Callback entfällt ebenfalls.

## Ausgeführte Prüfungen

- [x] 2.760 Assertions in 15 isolierten C#-Szenariogruppen: Produktionsabläufe für
  Start/Exit/Recall/Respawn, Pause, Match-Ergebnis, Roster, Tageswechsel und lokale
  Präsentationshoheit. Engine, Netzwerk, Coroutine-Schritte, Geometrie-/Actor-Bau,
  Audioausgabe und Profilspeicher sind ersetzt. Musik-Ergebnis und HUD-Auflösung
  sind echte extrahierte Methoden; sichtbare Kosmetik wurde nur an der Quelle geprüft.
- [x] Negativkontrolle mit dem ursprünglichen Code: acht Szenariogruppen scheitern
  gezielt, insbesondere Aufbaufehler, letzter Recall-Frame, pausiertes Respawn und
  Gast-Buff-Auflösung. Der korrigierte Stand besteht dieselben Tests.
- [x] 98 bestehende Account-/Commerce-/Social-Server-Tests: SQLite mit temporären
  Datenbanken; Google-Transport, Identitätstransport und Firestore-Transaktionen
  verwenden Test-Doubles. Keine reale Zahlung, kein Live-Backend und keine Nachricht.
- [x] Bestehende VFX-Regression: 9.274 Assertions mit Produktions-Poolsteuerung,
  Qualitätshysterese und Audio-Hüllkurve; Engine und Mesh-Fabrik ersetzt.
- [x] Bestehende Oberkörper-Regression: 261.846 Assertions, 1.536 finale Posen
  über acht Profile und zwei Rig-Fixtures; Transform-/Clip-/Grip-Substitute.
  Die getesteten Produktionsdateien stimmen mit diesem GitHub-Stand überein.
- [x] Sieben tatsächlich eingebettete Map-Meshes: Base64/Gzip-CRC, Längen,
  endliche Koordinaten, Dreiecksanzahl und Indexgrenzen bestanden.
- [x] 463 C#-Dateien einschließlich Test-Fixtures syntaktisch geparst; keine
  Parserfehler. Der gültige lokale Bezeichner `required` benötigt im Parser-Eingang
  eine Maskierung wegen einer Grammatikschwäche. Das ist kein Unity-Kompiliernachweis.
- [x] Alle 865 vorhandenen `.meta`-Dateien gelesen: gültige eindeutige GUIDs.
  40 serialisierte Projekt-/Szenen-/Materialdateien und 66 konstante typisierte
  `Resources.Load`-Adressen geprüft; diese 66 Adressen sind eindeutig im Tree vorhanden.

Runner: `Tools/Cleanup/Regression/run.py`,
`Tools/Cleanup/audit_repository.py`; maschinenlesbare Inventur in
`Documentation/Clean-Game-Audit-2026-10-09.json`.

## Verbleibende Import-/Paketprüfung

Die sieben `Resources/AuthoredHeroes/*.fbx` und
`Resources/CyberRanger/SivraRifleDetails.fbx` hatten bereits vorher keine eingecheckten
Importer-Metadaten. Sie sind nicht verändert. Ihre Import-Einstellungen müssen aus
dem echten Unity-Projekt erhalten beziehungsweise dort geprüft und eingecheckt
werden; ohne Unity lassen sich Rig-/Mesh-Importer nicht verlässlich nachbilden.

217 referenzierte GUIDs sind außerhalb der Projekt-GUID-Inventur. Sie liegen
überwiegend in URP-GlobalSettings, Renderern und Volume-Profilen und können zu den
installierten Unity-/Google-/Netcode-Paketen gehören. Sie sind ausdrücklich nicht
als fehlende Assets bewertet. Paketauflösung und GUID-Auflösung erfordern den
echten Editor. Manifest, Lockdatei, Pipeline, FBX-Importer und Audio-Importer wurden
deshalb nicht auf Verdacht ersetzt. Dynamische Resource-Adressen bleiben ebenfalls
ein Editor-Testpunkt.

## Schutz des vorhandenen Spiels

Keine Veränderung von MP3s, anderen Audio-Assets/Importern, Modellen, Karten,
Schadenswerten, Reichweiten, Cooldowns oder der aktuellen Lauf-/Oberkörperanimation.
Keine Entfernung von Komponenten allein wegen ihres Alters oder Namens.

## Noch ausstehende echte Durchläufe

- [ ] Unity 6000.6.1f1: Paketwiederherstellung, fehlerfreie Kompilierung und Android-Build.
- [ ] Jede der elf Karten starten, spielen, beenden, erneut starten und verlassen.
- [ ] Alle acht Helden: Bewegung, Grundangriff, Q/W/E/R, Tod und Respawn.
- [ ] Android: Multitouch, Safe Area, Hintergrund/Rückkehr, Lautstärke und Performance.
- [ ] Zwei echte Clients: Lobby, Countdown, Verbindungstrennung und Folgerunde.
- [ ] Google-Play-Testtrack: Kauf, Abbruch, verzögerter Kauf, Recovery, Kontowechsel.

Unity und ein Android-Gerät stehen in dieser Umgebung nicht zur Verfügung.
Diese Punkte sind ausdrücklich nicht als bestanden markiert.
