# Ione Vaska: Schulterausrichtung am 12.10.2026

Ausgangsstand: `954f9cecb201260da40b507f93216f3f91b909d6`, Branch `dev/vertical-slice`. Anlass ist der Unity-Screenshot des Eigentümers mit nach innen zum Hals wirkenden Schulterlamellen.

## Korrektur

Die drei weißen Schulterlamellen pro Seite waren in den kanonischen Modellkoordinaten nach vorn (+Z) aufgebaut und standen zu hoch über den Schulterkernen. Beide Pakete einschließlich violetter Lichtkanten und Befestigungen sind jetzt um ihre jeweiligen lokalen Mittelpunkte nach außen gedreht: links -45°, rechts +45° um +Y. Sie sind zusätzlich 5,1 cm tiefer auf die runden Gelenke gesetzt. Die Bindung an `UpperArmL` bzw. `UpperArmR` bleibt erhalten.

Die Laufzeitressource `UmbralMage/Model.bytes` enthält die korrigierten Positionen und Normalen. Der Generator setzt die gleiche Ausrichtung ausschließlich für Ione. `Tools/HeroConcepts/shoulder_orientation.py` reproduziert die Korrektur aus dem unveränderten, archivierten Ausgangsmodell und verhindert eine zweite Anwendung.

2802 Zielvertices in sechs Lamellenpaketen wurden bearbeitet. Das Modell behält 29840 Vertices und 57147 Dreiecke. Alle übrigen Positionen, Normalen, Knochenzuordnungen, Submeshes, Gruppen und Waffenmarker sind exakt erhalten. Iones Fokusachse bleibt nach der bestehenden Laufzeitanpassung +Z.

## Tatsächlich geprüft

- Geometrie, endliche Werte, Normalen und Dreiecksorientierung.
- Ausrichtung nach außen, seitengleiche Grenzen und Abstand zur Halsmitte; die alte Ausrichtung wird als Negativkontrolle abgewiesen.
- Exakter Hash-Abgleich der unveränderten Daten und starre Bindung unter 30 synthetischen Gelenkrotationen.
- Bestehende Ione-Ressourcenprüfung mit 27 Gelenk-Konfigurationen und Fokusmarkern.
- Reproduzierbares Ergebnis der Generator-Korrektur; eine zweite Anwendung bleibt ohne Änderung.

Nachweise: `ArtSource/Heroes/ConceptModels/Ione-shoulder-orientation-checks.json`, `Sivra-Ione-integration-checks.json` und `ArtSource/Heroes/Concepts/Provenance/Ione.json`.

```sh
python Tools/HeroConcepts/shoulder_orientation.py
python Tools/HeroConcepts/validate_sivra_ione.py --hero Ione
```

## Modellansichten und Exportstand

`ArtSource/Heroes/ConceptModels/Ione-shoulders-front.png` ist eine direkte Software-Rasterisierung der korrigierten Ressourcen-Dreiecke. Sie ist keine Unity-Aufnahme. Die separat erzeugte KI-Nahansicht dient als gestalterische Erläuterung.

Die bisherigen Blender-/FBX-Dateien, OBJ-/SVG-Bauteilpläne und ihre Exportprüfungen bleiben als Nachweise des vorherigen Modellstands archiviert. Für die neue Orientierung wurden hier keine Blender-/FBX-Dateien erzeugt; der aktualisierte `build_models.py` ist der Reproduktionsweg für diese Exporte. Die neue Laufzeitressource wird durch den zusätzlichen Korrekturbericht mit dem ursprünglichen Export verknüpft.

Unity-Editor und Android wurden hier nicht ausgeführt. Nach dem Aktualisieren des Branches Play Mode beenden und neu starten, damit die zwischengespeicherte Ressource neu geladen wird. Die Sichtprüfung im tatsächlichen Spiel steht noch aus.
