# Kräftigere Beinpanzerung und mehr Rüstungsdetails

Void Knight und THE TRAVELER wurden am tatsächlich verwendeten Modell überarbeitet. Beide behalten ihre vorhandenen Skelette und Animationen; die Änderungen betreffen die Client-Darstellung.

| Held | Neue Proportionen | Panzerung |
| --- | --- | --- |
| Void Knight | Oberschenkelschalen 235 statt 177 mm breit (+33%), 282 statt 195 mm tief (+45%) | Überlappende Quadrizepsplatten, Kühlkassetten, Rückenschalen, Energiekanäle, Brustschuppen, Gelenk- und Kniepanzer |
| THE TRAVELER | Oberschenkel und zugehörige Panzerplatten im Querschnitt 25% schlanker als die erste verstärkte Fassung; Beinlänge unverändert | Carbon-Schalen mit Titanrahmen, seitliche Kühlrippen, segmentierte Schultern, eingesetzte Anzeigen und mechanische Verbindungen |

Bearbeitbare Blender-Dateien und echte Geometrieansichten:

- [Void Knight](../ArtSource/Heroes/VoidKnight/Armor-V3/README.md), [Vorschau](../ArtSource/Heroes/VoidKnight/Armor-V3/Renders/armor-angle.png).
- [Traveler](../ArtSource/Heroes/ChronoTraveler/README.md), [Vorschau](../ArtSource/Heroes/ChronoTraveler/reinforced-angle.png).

Die korrigierte Schwerthaltung des Void Knights bleibt erhalten. Alle 86 Idle-/Run-Frames sind frei von Klingenüberschneidungen mit Armen, Kopf und Brust. 384 Animationsframes, 180 Sichtstrahlen, der FBX-Export und 17 einzelne Blender-Baugruppen bestehen die Prüfungen.

Bolts Basisangriff erhält zusätzlich einen Faktor von `0.8` auf die bisherige Schusslautstärke. Nur die Gatling-Mündungsstufe ist betroffen; Fähigkeiten und die übrigen Helden behalten ihre Lautstärke. Die 60 vorhandenen BOLT-7-VFX-/Audio-Prüfungen bestehen. Traveler-Audio bleibt unverändert.

Die Abbildungen sind Blender-Studioansichten. Die abschließende Sicht- und Hörprüfung in Unity beziehungsweise auf Android steht aus. Für diese Änderungen ist kein neuer Server nötig; für das Gerät wird ein aktualisierter Client benötigt.
