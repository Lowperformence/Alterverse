# Tahla Venn — bestätigte Elfenbein-/Gold-/Smaragd-Ausrüstung

Der am 13.10.2026 bestätigte Bildentwurf ist als bearbeitbare Blender-Geometrie
und als Spielressource umgesetzt. Rüstung, modularer Stab und Handfokus benutzen
Tahlas vorhandenes Rig (`HeroId.Mira`). Die bisherigen Fähigkeiten bleiben erhalten.

## Dateien und Modellierung

- `Tahla-approved-concept.png`: bestätigter künstlerischer Entwurf.
- `Tahla-equipment.blend`: 130 benannte, getrennte Solids, bestehender Referenzkörper,
  Rig und neun vorhandene Aktionen. Der Körper ist eine Modellreferenz; das Spiel
  verwendet weiterhin die importierte Mira-Anatomie mit ihrer vorhandenen Handsymmetrie.
- `Tahla-front.png`, `Tahla-back.png`: Ansichten der tatsächlichen Blender-Geometrie.
- `model.json`, `validation.json`, `reference-rig.csv`: Teilezahlen, geprüfte Daten
  und die ursprünglichen importierten Knochenpositionen.
- `Assets/Alterverse/Resources/TahlaEquipment/Model.bytes`: exakte Dreiecke,
  Materialgruppen, Knochengewichte und Stab-Anschlussdaten aus Blender.

Die Rüstung verwendet 20 Plattenmodule mit getrennten dunklen Unterlagen,
Goldrändern, Elfenbeinflächen und Sechskantschrauben. Ein offener goldener Stirn-
und Schläfenrahmen mit umlaufendem Band ergänzt den vorhandenen Elfenkopf.
Weißes Haar und Gesicht bleiben erhalten; der bestehende Smaragdhalo wird weiterhin
über `TahlaHalo` hinzugefügt. Die Brust und der Rücken erhalten facettierte Kristalle.

Der Stab besteht aus einem geraden Sechskantschaft, zwei Griffhülsen, goldenen
Verbindungsringen, drei getrennten Gabelarmen und zwei Kristallen. Der Handfokus
verwendet eine kompakte Auflage, eine Goldbasis, zwei Tragarme und einen Kristall.
Alle Formen entstehen aus geschlossenen Prismen, kleinen Bevels, Zylindern oder
Oktaedern. Es gibt keine Sculpting-, Boolean- oder Cloth-Abhängigkeit. Einheit: Meter.

## Integration

`TahlaEquipmentAppearance` erzeugt drei Renderer: `DF_TahlaArmor`, `DF_WeaponR`
und `DF_WeaponL`. Die Waffennamen bleiben für Shop-Farben und die bestehende
Präsentation erreichbar. Der Stab verwendet den ursprünglichen Griffkontakt.
Sein VFX-Socket folgt dem neuen Smaragdkristall und dem vorhandenen rechten Griff.

Die Umrechnung benutzt lokale Transformationsketten und importierte Bind-Posen,
einschließlich eines Hand-Bind-Fallbacks für einen bisher unbenutzten linken Griff.
Sie übernimmt keine gebackenen Weltkoordinaten als Handanschluss. Die alten
Rüstungsteile, getrennten Stoff-Anbauten und der alte Stab werden auf Tahlas Instanz
ersetzt. Kopfhaut, Gesicht, Anatomie und Fußform bleiben erhalten; Torso und Beine
erhalten den hellen Unteranzug. Die alten Detail- und Boot-Pässe laufen nur als
Fallback, falls die neue Ressource nicht geladen werden kann. Fehler beim Aufbau
stellen den bisherigen Körper und die bisherigen Waffen wieder her.

## Erzeugen und prüfen

Mit Blender 4.x:

```
blender -b --python-exit-code 1 --python Tools/TahlaEquipment/build_equipment.py -- --source ArtSource/Heroes/Mira.blend
blender -b --python-exit-code 1 --python Tools/TahlaEquipment/validate_equipment.py
python Tools/TahlaEquipment/run_regression.py
```

Die nativen Prüfungen öffnen die Blender-Datei, kontrollieren geschlossene
Volumen und Gewichte, vergleichen 19 ursprüngliche Knochenpositionen und prüfen
27 Animationsproben sowie die Plattenkontakte mit dem Referenzkörper. Der
C#-Regressionstest führt den tatsächlichen Komponenten-Code mit CPU-Fixtures aus:
Meter-/Zentimeter-Import, verschobene und gedrehte Welt-Hierarchien, linker Griff,
Waffen-Socket, Bereinigung, wiederholtes Anwenden und Wiederherstellung bei Fehlern.

Unity Editor und Android Player wurden in dieser Umgebung nicht ausgeführt.
