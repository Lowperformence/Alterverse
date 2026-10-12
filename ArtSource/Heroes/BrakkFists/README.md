# Brakk Orun — Forge Grip V2

Stand: 12.10.2026. Spiel-ID: `HeroId.Rook`. Auftrag: eigene neue Faustpanzer entwerfen, mit Sprengzeichnung in Blender umsetzen und die bisherigen Modelle an beiden Händen ersetzen.

Korrektur nach dem Spiel-Screenshot: Alle drei Abmessungen der neuen Fäuste sind gegenüber dem ersten Forge-Grip-V2-Stand verdoppelt. Auf ausdrücklichen Wunsch wird die Position der alten Faustwaffe übernommen: Der Ersatz bleibt im selben vorhandenen Waffenobjekt, mit derselben Transform-Hierarchie und Handanbindung.

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

1. `BrakkFists-assembly.blend` öffnen. Einheit ist Meter, Unit Scale 1. Die Teile liegen an den Positionen der ursprünglichen Faustwaffen des Rook-Rigs. Objektnamen `FGV2_L_*` und `FGV2_R_*` entsprechen den Bauteilen in `assembly.json`.
2. Hauptschalen sind geschlossene, abgeschrägte Prismen. Knöchel, Finger, Daumen und Einlagen sind getrennte Solids; die Manschette besitzt eine echte offene Innenkontur. Alle Bauteile lassen sich ohne Boolean-Abhängigkeiten bearbeiten.
3. Jede starre Platte ist ausschließlich zu 100 % an `HandL` oder `HandR` gebunden. Keine Platte greift auf Unterarm- oder Waffen-Griffknochen über. Die vorhandenen Aktionen wurden für die Passformprüfung verwendet.
4. Die Sprengzeichnung zeigt dieselben exportierten Dreiecke aus `BrakkFists/Model.bytes`. Ihre Maße beziehen sich auf die Blender-Quellgeometrie. Der vorhandene Laufzeit-Handfaktor 0,8 und die etablierten Charakter-Skalierungen kommen im Spiel zusätzlich zur Anwendung.
5. Der Generator liegt in `Tools/BrakkFists/build_fists.py`. Er baut die neuen Teile reproduzierbar auf Grundlage der archivierten Anschlussmatrizen der bestehenden `ArtSource/Heroes/Rook.blend`. Beispiel: `blender -b --python Tools/BrakkFists/build_fists.py -- --source ArtSource/Heroes/Rook.blend`. Anschließend `python Tools/BrakkFists/draw_blueprint.py` für die exakte Zeichnung ausführen; dazu werden NumPy und CairoSVG benötigt.
6. Änderungen an der Blender-Datei benötigen einen erneuten Export nach dem Datenvertrag des Generators, bevor sie im Spiel erscheinen. Die Laufzeit lädt `Assets/Alterverse/Resources/BrakkFists/Model.bytes`.

## Integration im Spiel

`BrakkFistAppearance.Apply` läuft nach `ApplyHandSize` und `BrakkForearmTrim`, vor der Erfassung der Waffenrenderer in `AuthoredHeroPresentation`. Die vorhandenen SkinnedMeshRenderer `DF_GauntletL` und `DF_GauntletR` bleiben dieselben Objekte. Ausschließlich ihre instanzbezogenen Meshes und Materialien werden ausgetauscht. Beide Ersatzmeshes werden vor dem Austausch vorbereitet; bei einem Fehler bleiben die alten Fäuste erhalten.

Die Position wird von der alten Waffe übernommen: Der Mittelpunkt der neuen Geometrie liegt auf dem Vertex-Bounds-Mittelpunkt des bisherigen Waffenmeshes. Transform, Knochenliste, Root Bone und Bind-Matrizen bleiben erhalten. Alle neuen Vertices besitzen wie die bisherigen Fäuste genau ein Handgewicht von 100 %. Die Größe wird anhand der tatsächlichen alten Waffenabmessungen in die importierten FBX-Einheiten übertragen. Die Umrechnung verwendet ausschließlich lokale Transform-Matrizen; die entfernte Menüvorschau bei `(12000,12000,12000)` beeinflusst die Geometrie nicht. Unity aktualisiert die Skin-Grenzen auch außerhalb der Kamera.

Die Faustmaße sind gegenüber dem ersten Forge-Grip-V2-Entwurf in allen drei Achsen um Faktor 2 vergrößert; der vorhandene Handfaktor 0,8 bleibt erhalten. Der Ersatz gilt ausschließlich für `HeroId.Rook`. Das gemeinsam verwendete Rook-FBX, Neroks Hände, Anatomie, Animationen und Gameplay-Kollisionskörper werden nicht verändert.

## Herkunft und Prüfung

Die neuen Solids und beide Generatoren wurden in diesem Auftrag erstellt; für die neuen Faustflächen wurden keine fremden Meshes oder Texturen importiert. Der Bildentwurf wurde mit OpenAI-Bildgenerierung aus dem gespeicherten Prompt erzeugt. Das bestehende Rook-Rig, seine Aktionen und die Referenzfigur bleiben Bestandteile der vorhandenen Projektbasis; ihre Herkunft wird dort separat geführt.

`validation.json` enthält die tatsächlich ausgeführten Prüfungen: Faktor 2 gegenüber dem ersten exportierten Modell, gleiche Position wie die alten Waffen, 27 Animationsproben, FBX-Reimport und sechs numerische Importvarianten einschließlich Zentimeter, Achsenwechsel und Spiegelung. `Tools/BrakkFists/validate_attachment.py` prüft den Anschluss anhand der ursprünglichen Rook-Faustgeometrie; es ersetzt keinen Unity-Lauf. Eine Prüfung im Unity Editor 6000.6.1f1 oder auf einem Android-Gerät wurde in dieser Umgebung nicht ausgeführt. Im Unity-Play-Mode steht **DIMENSIONS FRONT → Checks → Brakk Forge Grip V2** bereit; danach Idle, Run, Angriff, alle Fähigkeiten, Ultimate und Waffen-Skins am echten Charakter prüfen.
