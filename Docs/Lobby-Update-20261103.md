# Lobby-Update 03.11.2026

- „Invite to Game“ öffnet auf Android den nativen Teilen-Dialog mit https://play.google.com/store/apps/details?id=com.lowperformence.dimensionsfront. Bei einer offenen, freien Gruppe enthält der Text auch den Gruppencode. In Editor/Desktop wird der Text kopiert. Ein Link startet keine automatische Gruppenzuordnung; nach Installation im Online-Menü den Code eingeben.
- Die Online-Gruppe hat eine bebilderte Heldenauswahl. Bereits belegte und nicht freigeschaltete Helden sind gesperrt. Serverseitig gilt die Eindeutigkeit pro Team auch für Ranked; kollidieren Helden aus getrennten Gruppen, behält der erste nach Spieler-ID seinen Helden, weitere erhalten einen freien Basishelden. Eine nachträgliche Draft-/Bannphase ist nicht enthalten.
- Offline öffnet eine Vorbereitung mit beiden Teams, Heldenwahl und Bot-Auswahl. Der separate Co-op-Einstieg entfällt; interne Relay-Komponenten bleiben kompatibel.
- Vier zufällig platzierte, gespiegelte kosmische Nebelfelder verbergen gegnerische Helden außerhalb desselben Felds. Angriffe/Fähigkeiten decken sie zwei Sekunden auf. Zielauswahl, Bots, Minions, Türme und Marker berücksichtigen diese Sichtregel. Positionspakete werden weiterhin übertragen.
- Party-Sprachchat ist standardmäßig aus. Nach ausdrücklicher Zustimmung und Android-Mikrofonfreigabe wird der private Vivox-Kanal verbunden. App-Wechsel schaltet stumm; Rückkehr schaltet niemals automatisch wieder frei.
- Der neue Kontoserver-Zusatz enthält außerdem private, fünf Minuten gültige Gruppeneinladungen unter akzeptierten Freunden (begrenzter Versand, keine Währungsänderung). Die bestehende Geschenkfunktion bleibt erhalten.

## Release

1. Diesen Branch in Unity 6000.6.1f1 aktualisieren und Paketauflösung abwarten (Vivox 16.12.1).
2. Vivox im zugeordneten Unity-Cloud-Projekt aktivieren. Ohne aktive Konfiguration bleibt Spielen möglich, Voice meldet „nicht verfügbar“.
3. Neue AAB und neuen Linux Dedicated Server bauen. Das aktuell aktive Edgegap-Image 20261102-225303 enthält diese Änderungen noch nicht.
4. Den Kontoserver separat mit dem aktualisierten Konsolen-ZIP bereitstellen; erwartete Revision: 2026-10-28.clans.sync.1.friend-gifts.2. Hier noch nicht live bereitgestellt.
5. Auf zwei Android-Geräten prüfen: WhatsApp-Auswahl/Abbrechen/Code, korrekte Bilder aller Helden, doppelte Helden pro Team, Gruppeneinladung annehmen/ablehnen, Mikrofon ablehnen/erlauben/App-Wechsel, Nebel auf beiden Clients und neuer Dedicated-Server-Build.

## Prüfung

Kontrollierte C#-Regressionen decken Lobby/Heldenverteilung, Nebel-Sichtregeln und Netzwerknachrichten, Voice-Einwilligung und Lebenszyklus sowie den verzögerten Android-Share-Aufruf ab. Node-Tests decken Geschenke, Berechtigungen, Einladungsablauf, Ratenlimit und Quellerhaltung ab. Native Unity-/GPU-/Android-Audio- und Live-Vivox-Prüfungen sind hier nicht möglich; diese Tests ersetzen keinen Geräte-Build.
