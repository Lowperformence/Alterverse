# Traveler — Nocturne V01

Neuer, KI-unterstützt entwickelter Raumanzug-Charakter für Alterverse. Auftrag vom 12.10.2026: Traveler von Grund auf bauen, alles schwarz und düster, Kugelwaffe, Prototyp, Explosionszeichnungen und nachvollziehbare Herkunft. GitHub-Veröffentlichung wurde im Auftrag ausdrücklich erlaubt.

## Gelieferter Stand

Ein echtes editierbares, **noch ungeriggtes 3D-Prototypmodell** mit 143 benannten Bauteilen und 41.294 Dreiecken. Keine alten Körpermeshes, Texturen, Waffenmeshes, Scan-Daten oder Bildreferenzen importiert. Das künstlerische Konzept und der parametrische Geometrieprototyp sind unterschiedliche Reifestufen: Der Prototyp setzt Baugruppen und Formensprache um, erreicht aber noch nicht den Oberflächendetailgrad des Konzeptbilds.

| Datei | Zweck |
|---|---|
| concept_exploded.png | Neu generierter Konzeptbogen, gestalterische Richtung |
| prototype_geometry.png | Vorder-/Rückansicht der tatsächlich exportierten Dreiecke |
| exploded_geometry.png | Sechs Baugruppenansichten aus derselben Modellgeometrie |
| traveler_prototype.gltf | Selbstständiges glTF 2.0 mit eingebettetem Buffer, Materialien und Einzelteilen |
| traveler_prototype.obj + traveler.mtl | Austauschmodell mit benannten Objektgruppen |
| traveler_exploded.obj | Zerlegte Anordnung aller Einzelteile |
| build_traveler.py | Neu geschriebene parametrische Bauquelle; reproduziert Exporte und Prüfbericht |
| import_blender.py | Blender-Importer aus derselben Bauquelle, erzeugt editierbare .blend-Datei |
| parts.json | Vollständiges Bauteilregister, Material, vorgeschlagener Bone, Verschiebevektor |
| DESIGN.md | Maße, Baugruppen, Materialien, Montage- und Fertigstellungsplan |
| PROVENANCE.md + prompts.txt | Auftrag, Eingaben, Werkzeuge, Herkunft und Prompts |
| RIGHTS.md | Rechtebasis und klar bezeichnete Nachweisgrenzen |
| validation.json + SHA256SUMS.json | Tatsächlich ausgeführte Prüfungen und Dateiprüfsummen |

## Reproduktion

Python 3, NumPy und Matplotlib erforderlich. Von diesem Verzeichnis aus:

```bash
python build_traveler.py
blender --background --python-exit-code 1 --python import_blender.py
```

Alternativ `traveler_prototype.gltf` in Blender importieren. Der Importer verwendet direkte Mesh-Konstruktion, legt zusammengebaute und ausgeblendete Explosionskollektionen an, weist PBR-Materialien zu und setzt einen `DF_Muzzle`-Marker ins Kugelzentrum. .blend wird erst beim Ausführen mit Blender erzeugt. Blender ist in dieser Arbeitsumgebung nicht installiert; diese Ausführung wurde daher nicht geprüft. Ein vorhandenes Blender-Projekt wird durch den Importer ergänzt, nicht gelöscht.

## Integration in Alterverse

Ausgangspunkt: `dev/vertical-slice`, Commit `3dc8d779893c33ce0826c0cc1fa21eefcf4c294b`.
Bestehende Dateien wurden zur Schnittstellenanalyse gelesen, nicht als Mesh-Quelle verwendet:

- `Tools/HeroRebuild/README.md`: 19-Bone-Kontrakt und separate Waffenrenderer.
- `Tools/ChronoTraveler/build_chrono_traveler.py`: vorhandene Ressource; ihr Quelltext und ihre Geometrie wurden nicht übernommen.
- `Assets/Alterverse/Editor/TravelerOrbImportPass.cs`: separates `DF_WeaponR`, Griff-Bone `WeaponGripR`, Mündungsmarker `DF_Muzzle`.
- `Assets/Alterverse/Scripts/Visuals/TravelerOrbAppearance.cs`: Laufzeitcode baut die Kugel aktuell selbst auf und setzt farbige Materialien. Vor einer finalen Integration muss dieser Override auf das neue Modell abgestimmt werden, sonst wird die neue schwarze Kugel ersetzt.

Dieses Paket liegt bewusst unter `ArtSource/Heroes/TravelerNocturneV01`. Es schaltet das laufende Spiel nicht auf den ungeprüften Prototyp um. Für die endgültige Version fehlen Modellverfeinerung auf Konzeptniveau, UVs/Normal-Bakes, deformierendes Rigging, passende Griff-/Kugelpose, LODs, FBX-Rundlauf und echte Unity-/Android-Tests. Fremdanimationen sind nicht Teil der Herkunftskette dieses neuen Modellpakets.
