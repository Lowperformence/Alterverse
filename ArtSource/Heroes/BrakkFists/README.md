# Brakk Orun — Forge Grip V2

Stand: 12.10.2026. Spiel-ID: `HeroId.Rook`. Auftrag: eigene neue Faustpanzer entwerfen, mit Sprengzeichnung in Blender umsetzen und die bisherigen Modelle an beiden Händen ersetzen.

Die neuen Fäuste bestehen je Seite aus 34 benannten, geschlossenen Bauteilen: profilierter Graphit-Grundkörper, vier getrennte Knöchelpanzer, vier gefaltete Fingersegmente mit Schutzplatten, Daumenschale mit Gelenk, abgeschrägte Handrückenplatte mit Einlagen, seitliche Halteschienen und Außenschale, kurze offene Manschette und vier versenkte Schrauben. Gold, Graphit und dunkler Stahl bilden die Hauptflächen; zwei eingelassene Kanäle leuchten bernsteinfarben. Links ist die Geometrie der rechten Faust gespiegelt. Gelenke und Hände folgen dem bisherigen Rig.

## Dateien

| Datei | Zweck |
|---|---|
| `BrakkFists-concept.png` | Gestalterischer KI-Entwurf; keine maßhaltige Vorlage. Feine Gebrauchsspuren und Gravuren sind darin Illustration. |
| `BrakkFists-assembly.blend` | Tatsächliche neue Geometrie, 68 separat bearbeitbare Teile, mit dem übernommenen Hand-Rig und bisherigen Aktionen. |
| `BrakkFists-exploded.blend` | Rechte Faust in zerlegter Ansicht; Explosionsabstände sind reine Ansichtstransformationen. |
| `BrakkFists.fbx` | Zusätzlicher Austausch-Export, ohne neue Animationsclips. |
| `BrakkFists-parts.obj` | Benannte Bauteiloberflächen in Blender-Koordinaten, Meter. |
| `BrakkFists-Sprengzeichnung.svg` / `.png` | Exakte Projektionen der exportierten Laufzeitgeometrie mit gemessenen Maßen. |
| `BrakkFists-assembled.png` / `BrakkFists-exploded.png` | Blender-Render der tatsächlich modellierten Faust. |
| `BrakkFists-on-Brakk.png` | Passformrender am importierten Basismodell mit Handfaktor 0,8; nur die vorhandenen Enamel-Flächen wurden für die Ansicht goldfarben dargestellt. Die übrigen Laufzeitkorrekturen am Charakter werden im Projektcode angewendet. |
| `assembly.json` / `fit-contract.json` | Teilnamen, Vertexbereiche, Materialgruppen, gemessene Grenzen und Anschlussmatrizen. |
| `validation.json` | Ausgeführte Geometrie-, Gewichts- und Animationsanbindungskontrollen. |
| `provenance.json` / `concept-prompt.txt` / `SHA256SUMS.txt` | Herkunft, genauer Bildprompt und Integritätsprüfsummen. |

## Aufbau und Bearbeitung in Blender

1. `BrakkFists-assembly.blend` öffnen. Einheit ist Meter, Unit Scale 1. Die Teile liegen an den originalen Handgelenken des Rook-Rigs. Objektnamen `FGV2_L_*` und `FGV2_R_*` entsprechen den Bauteilen in `assembly.json`.
2. Hauptschalen sind geschlossene, abgeschrägte Prismen. Knöchel, Finger, Daumen und Einlagen sind getrennte Solids; die Manschette besitzt eine echte offene Innenkontur. Alle Bauteile lassen sich ohne Boolean-Abhängigkeiten bearbeiten.
3. Jede starre Platte ist ausschließlich zu 100 % an `HandL` oder `HandR` gebunden. Keine Platte greift auf Unterarm- oder Waffen-Griffknochen über. Die vorhandenen Aktionen wurden für die Passformprüfung verwendet.
4. Die Sprengzeichnung zeigt dieselben exportierten Dreiecke aus `BrakkFists/Model.bytes`. Ihre Maße beziehen sich auf die Blender-Quellgeometrie. Der vorhandene Laufzeit-Handfaktor 0,8 und die etablierten Charakter-Skalierungen kommen im Spiel zusätzlich zur Anwendung.
5. Der Generator liegt in `Tools/BrakkFists/build_fists.py`. Er baut die neuen Teile reproduzierbar auf Grundlage der archivierten Anschlussmatrizen der bestehenden `ArtSource/Heroes/Rook.blend`. Beispiel: `blender -b --python Tools/BrakkFists/build_fists.py -- --source ArtSource/Heroes/Rook.blend`. Anschließend `python Tools/BrakkFists/draw_blueprint.py` für die exakte Zeichnung ausführen; dazu werden NumPy und CairoSVG benötigt.
6. Änderungen an der Blender-Datei benötigen einen erneuten Export nach dem Datenvertrag des Generators, bevor sie im Spiel erscheinen. Die Laufzeit lädt `Assets/Alterverse/Resources/BrakkFists/Model.bytes`.

## Integration im Spiel

`BrakkFistAppearance.Apply` läuft nach `ApplyHandSize` und `BrakkForearmTrim`, vor der Erfassung der Waffenrenderer in `AuthoredHeroPresentation`. Die neuen zusammengefassten MeshRenderer heißen weiterhin `DF_GauntletL` und `DF_GauntletR`, damit Waffen-Skins und Effekte sie wie bisher erreichen. Die ursprünglichen Renderer werden erst nach erfolgreicher Erstellung beider Ersatzmodelle deaktiviert und in `DF_LegacyGauntletL/R` umbenannt.

Die neue Geometrie wird mit den echten Bind-Matrizen der importierten Anatomie in den jeweiligen Handraum übertragen. Sie benötigt keine geratenen FBX-Knochenachsen und folgt der Handgröße sowie allen bestehenden Handanimationen. Der Ersatz gilt ausschließlich für `HeroId.Rook`. Das gemeinsam verwendete Rook-FBX, Neroks Hände, Anatomie, Animationen und Gameplay-Kollisionskörper werden nicht verändert.

## Herkunft und Prüfung

Die neuen Solids und beide Generatoren wurden in diesem Auftrag erstellt; für die neuen Faustflächen wurden keine fremden Meshes oder Texturen importiert. Der Bildentwurf wurde mit OpenAI-Bildgenerierung aus dem gespeicherten Prompt erzeugt. Das bestehende Rook-Rig, seine Aktionen und die Referenzfigur bleiben Bestandteile der vorhandenen Projektbasis; ihre Herkunft wird dort separat geführt.

`validation.json` enthält die tatsächlich ausgeführten Prüfungen. Eine Prüfung im Unity Editor 6000.6.1f1 oder auf einem Android-Gerät wurde in dieser Umgebung nicht ausgeführt. Im Unity-Play-Mode steht **DIMENSIONS FRONT → Checks → Brakk Forge Grip V2** bereit; danach Idle, Run, Angriff, alle Fähigkeiten, Ultimate und Waffen-Skins am echten Charakter prüfen.
