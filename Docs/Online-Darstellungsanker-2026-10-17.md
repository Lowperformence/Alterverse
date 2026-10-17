# Schilde und Soft-Target-Anzeige im Online-Match

## Bestätigte Ursachen und Korrektur

- Entfernte Helden besitzen eine geglättete sichtbare Position. Die Zielmarkierung und mitlaufende Effekte verwendeten bisher die schrittweise aktualisierte Netzwerkposition. `HeroPresentationAnchor` verbindet diese Anzeigen mit der sichtbaren Position; die Spielphysik bleibt auf der autoritativen Position.
- `HeroVisualMotion` berechnet seine Darstellung in `LateUpdate` mit Reihenfolge 930. Der Effektpool zeichnet jetzt danach mit Reihenfolge 1600. Damit verwenden Heldenmodell und angehängte Effekte den Stand desselben Bildes.
- Ein empfangener Schildwert wurde auf dem Client nach 350 ms verworfen. Jetzt bleibt der letzte bestätigte Wert erhalten, bis ein neuer Serverstand ihn reduziert oder löscht. Die Dauer serverseitiger und offline erzeugter Fähigkeitsschilde bleibt unverändert.
- Lokale Figuren, gewöhnliche Ziele und noch nicht initialisierte Darstellungen verwenden ihre normalen Transformpositionen. Angeheftete Effekte behalten skalierte lokale Abstände und die sichtbare Blickrichtung.

## Kontrollierte Prüfungen

- 4.306 Online-VFX-/Kampfprüfungen einschließlich echter Präsentationspuffer-, Effektpool-, Ziel-HUD- und Schild-Snapshot-Methoden mit Ersatz für Unity, GPU und Netzwerk.
- 30/60/120 FPS, unregelmäßige Paketankunft und 570 ms Paketpause; gleiche Anker pro Bild, Schildbruch, Zielwechsel, lokale Rückfallposition, Rotation und Skalierung.
- 1.274 Skill-Entry- und bestehende Kampfprüfungen auf dem gemeinsamen Stand mit den parallel hinzugekommenen Kegel-/Trefferänderungen.
- Gegenproben mit jeweils alter Ziel-HUD-, Effektpool- oder Schild-Snapshot-Implementierung schlagen an der betreffenden Regression fehl.

## Noch nativ zu prüfen

- Android und Dedicated Server aus demselben gemeinsamen Commit bauen. Seit dem derzeit aktiven Server v0.1.7 sind zusätzlich Arena- und Kampfänderungen hinzugekommen, die beide Builds betreffen.
- Im echten Online-Match Schilde, Zielmarkierung und angeheftete Effekte beim Laufen, Drehen und Dashen beobachten; Schildbruch und Tod prüfen.
- Geräte-Frametimes, RTT/Paketjitter und Server-Frametimes getrennt messen. Die kontrollierten Prüfungen belegen keine native Bildrate oder allgemeine Lagfreiheit.
- Die bisherigen nativen Warnungen zu fehlendem Script und Serialisierung sind durch diese Darstellungsänderung nicht geklärt.

Diese Änderung wurde vor einem nativen Unity-/Android-Neubau geprüft. Der laufende Edgegap-Server wurde dafür nicht umgeschaltet.
