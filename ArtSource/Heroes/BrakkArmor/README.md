# Brakk Orun - modularer Rüstungsentwurf 01

Der bestätigte Entwurf verwendet Graphit, Gold und dunklen Stahl. Jede Panzerplatte
ist ein eigenes, gut in Blender bearbeitbares Modul: Halterplatte, separate
Dämpfungseinlage, abgeschrägte Goldschale, Graphit-Deckplatte und drei
Sechskantschrauben. Alle Teile bestehen aus geschlossenen Polygonprismen oder
facettierten Zylindern. Sculpting, Gravuren und Boolean-Konstruktionen sind für
die Modellierung nicht erforderlich.

## Dateien

- `BrakkArmor-artistic-concept.png`: bestätigte künstlerische Darstellung. Sie
  veranschaulicht Materialwirkung und Charakterbild; technische Maße und Teile
  richten sich nach der tatsächlichen Blender-Geometrie.
- `BrakkArmor-concept.blend`: tatsächliche neue Geometrie am bestehenden
  Referenzkörper/Rig, mit den zuvor bestätigten Forge-Grip-Fäusten als Referenz.
  Der Spielkopf und die bestehenden Fäuste werden durch diesen Auftrag nicht
  ersetzt. Der neutrale Kopf in der Passformdatei ist der übernommene Referenzkopf.
- `BrakkArmor-exploded.blend`: getrennte Schichten aller 18 Plattenmodule.
- `models/`: 18 einzelne Plattenmodule, 34 einzelne Gürtelteile und ein
  zusammengebauter Gürtel, jeweils als eigenständig öffnende Blender-Datei.
  Diese Dateien enthalten keine Abhängigkeit vom Referenzkörper oder Rig.
- `plates/`: für jede der 18 Panzerplatten eine eigene Sprengzeichnung als
  maßhaltige SVG-Projektion und PNG. Die Zeichnungen verwenden dieselben Flächen
  wie die tatsächlichen Blender-Modelle. Explosionsabstände sind Ansichtsabstände.
- `BrakkArmor-front.png`, `BrakkArmor-back.png`, `BrakkUtilityBelt.png`: Blender-
  Ansichten der tatsächlichen Geometrie.
- `BrakkArmor-parts.obj`: benannte Teiloberflächen in der zerlegten Anordnung.
- `catalogue.json`, `diagram-geometry.json`, `individual-models.json`: Maße,
  Flächen, Teile, Materialgruppen, ursprüngliche Anschlussdaten und Dateizuordnung.
- `validation.json`, `provenance.json`, `SHA256SUMS.txt`: ausgeführte Prüfungen,
  Herkunft und Integrität. Der vollständige Bildprompt ist separat gespeichert.

## Aufbau in Blender

Einheit: Meter, Unit Scale 1. Jede eigenständige Plattendatei besitzt sieben
benannte Mesh-Objekte. Die Schichten bleiben getrennt und können einzeln geändert
werden. Objektgruppen und Kennungen entsprechen der jeweiligen Sprengzeichnung.
Links und rechts erhalten eigene Dateien; das gemeinsame Grundprofil kann per
Mirror-Verfahren weiterentwickelt werden.

Die Teile des Gürtels sind ein hohles Band mit Schließe, zwei Vordertaschen,
eine hintere Reparaturbox, eine Lampe, ein Maulschlüssel, zwei Kapseln und ihre
Halterungen. Die 34 Teile sind getrennte Solids. Neue Funktionen oder
Gameplay-Kollisionskörper sind damit nicht verbunden.

Die Generatoren liegen in `Tools/BrakkArmor/`: `build_armor.py` baut die Solids,
`export_individual.py` erzeugt die Einzeldateien, `draw_plates.py` die exakten
Zeichnungen und `build_catalogue.py` den illustrierten PDF-Katalog. Änderungen an
Maßen werden im Generator definiert und anschließend in Modelle/Zeichnungen
übernommen. `export_runtime.py` exportiert die Platten in die Unity-Ressource;
`validate_runtime.py` prüft die Knochenbindung und die torso-seitige Bereinigung.
Der Blender-Quellkörper bleibt eine bestehende Projektressource.

## Stand der Integration

Im Spiel werden Brakks bisherige zusätzliche Ober- und Unterschenkel-Bauteile,
Knieplatten, rote Hüft-/Oberschenkelklappen und der alte starre Hüftring entfernt.
Automatische Bein-Mikroplatten werden für Brakk unterdrückt. Anatomie, bestehende
Ankle-Boots, Spielkopf und bestätigte Fäuste bleiben erhalten.

Der neue Utensiliengürtel wird nach dieser Bereinigung als eigenes, zu 100 % an
`Hips` gebundenes Mesh eingefügt. Er benutzt die tatsächlichen importierten
Bind-Daten und lokale Koordinaten. Dies betrifft ausschließlich `HeroId.Rook`;
Nerok und die gemeinsam genutzte Rook-Quelldatei bleiben unverändert.

Die vollständige neue Rüstung wird durch `BrakkArmorAppearance` auf Brakk
aktiviert. Alle 18 Module bestehen aus der exportierten Blender-Geometrie und
folgen mit starren Gewichten den bestehenden Brust-, Wirbelsäulen-, Arm- und
Beinknochen. Die alten Brust-/Rücken-Anbauten werden erst entfernt, nachdem die
neue Rüstung erfolgreich erzeugt wurde. Der neue Renderer wird bei der
Bereinigung ausgespart. Kopf, Hände, Füße, Gürtel und gemeinsam genutzte
Quellressourcen werden durch diesen Schritt nicht verändert.

## Prüfung

Die neuen 160 Solids (126 Rüstungsteile plus 34 Gürtelteile) sind geschlossen,
manifold und besitzen ein positives Volumen. Der Gürtel und die Platten werden mit 27 Proben der neun bestehenden Aktionen
auf korrekte Knochenbindung geprüft. Quellgeometrie, Indexdaten
und Gewichte wurden geprüft. Die eigenständigen Dateien werden nach dem Export
separat geöffnet und kontrolliert. Eine Prüfung im Unity Editor oder auf Android
ist hier nicht durchgeführt; der PDF-Katalog zeigt Blender-Daten.

## Runtime layer repair (2026-10-16)

The current runtime rebuild uses one shared contour triangulation per module.
This fixes the missing/buried chest and thigh insets caused by projecting each
layer independently. The original editable part models remain unchanged. See
`Docs/BrakkArmorLayers.md` and `layer-validation.json` for reproduction and results.
The studio runtime preview displays only armor; it is not a Unity screenshot.
