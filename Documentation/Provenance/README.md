# DIMENSIONS FRONT — Herkunft, Entwürfe und Lizenzbelege

Erstellt am **12. Oktober 2026**. Repository: `Lowperformence/Alterverse`.

Diese Sammlung rekonstruiert die vorhandene Entwicklung und dokumentiert neun neue Konzeptentwürfe. Sie bestätigt keine pauschale Rechtefreiheit des gesamten Spiels. Offene Quellen werden ausdrücklich ausgewiesen.

## Eigene Konzeptentwürfe

Alle neun Konzeptblätter wurden am 12.10.2026 mit der eingebauten ChatGPT-Bildgenerierung erstellt. Vollständige Prompts, Prüfsummen und die verwendeten Projektvorschauen sind in [generation-records.json](../../ArtSource/Heroes/Concepts/2026-10-12/generation-records.json) abgelegt. Die Bilder sind neue Designrevisionen; sie werden nicht als ursprüngliche Vorabentwürfe älterer Modelle ausgegeben.

| Held | Konzept | Herkunftsdossier |
|---|---|---|
| Zhaev Null | [Bild](../../ArtSource/Heroes/Concepts/2026-10-12/Zhaev-Null.png) | [Chronik](Heroes/Zhaev-Null.md) |
| Auren Kest | [Bild](../../ArtSource/Heroes/Concepts/2026-10-12/Auren-Kest.png) | [Chronik](Heroes/Auren-Kest.md) |
| Sivra Quell | [Bild](../../ArtSource/Heroes/Concepts/2026-10-12/Sivra-Quell.png) | [Chronik](Heroes/Sivra-Quell.md) |
| Ione Vaska | [Bild](../../ArtSource/Heroes/Concepts/2026-10-12/Ione-Vaska.png) | [Chronik](Heroes/Ione-Vaska.md) |
| Tahla Venn | [Bild](../../ArtSource/Heroes/Concepts/2026-10-12/Tahla-Venn.png) | [Chronik](Heroes/Tahla-Venn.md) |
| Brakk Orun | [Bild](../../ArtSource/Heroes/Concepts/2026-10-12/Brakk-Orun.png) | [Chronik](Heroes/Brakk-Orun.md) |
| THE TRAVELER | [Bild](../../ArtSource/Heroes/Concepts/2026-10-12/The-Traveler.png) | [Chronik](Heroes/The-Traveler.md) |
| Nerok Abyss | [Bild](../../ArtSource/Heroes/Concepts/2026-10-12/Nerok-Abyss.png) | [Chronik](Heroes/Nerok-Abyss.md) |
| Andromeda Timmy | [Bild](../../ArtSource/Heroes/Concepts/2026-10-12/Andromeda-Timmy.png) | [Chronik](Heroes/Andromeda-Timmy.md) |

## Belegsammlung

- [Lizenzregister](LicenseRegister.json): 52 Quellen-/Beleggruppen, einschließlich 22 identifizierter Pixabay-Aufnahmen.
- [Lesbare Lizenzübersicht](LICENSES.md): Anbieter, Lizenz, Quelle, Belege und verbleibende Lücken.
- [Assetregister](AssetRegister.json): 488 bestehende Mediendateien mit Git-Blob-Prüfsumme und Quellenstatus. Der Status „Zuordnung offen“ ist keine Freigabe.
- [Repository-Snapshot](RepositorySnapshot.json): eingefrorener Dateibestand des geprüften `dev/vertical-slice`-Commits `3dc8d779893c33ce0826c0cc1fa21eefcf4c294b`.
- [Entwicklungschronik](HistoricalCommits.json): nachvollziehbare Commit-Historien der Modellgeneratoren und ursprünglichen Laufzeitgrafik.
- [Software-Abhängigkeiten](DependencyRegister.json): Versionen und Quellen aus dem Package-Lock; eine Versionsliste ersetzt keine Kontolizenz oder paketinterne Notices.
- [Pixabay-Zuordnung](Pixabay-Evidence.json): konkrete Asset-IDs/Anbieter und derzeitige öffentliche Lizenzangaben. Die Dateizuordnung beruht auf den übergebenen Namen/IDs und vorhandenen Projektmanifesten. Persönliche Downloadzertifikate werden nicht erfunden.

## Originalbelege

Vorhandene Kenney-, Quaternius- und KayKit-Lizenztexte bleiben in ihren ursprünglichen Projektordnern. Die vollständigen OFL-Texte für Rajdhani und die umbenannte Orbitron-Version sowie die Apache-Texte der beiden Google-Plugins liegen unter `Licenses/`. Die Schriften wurden anhand ihrer tatsächlichen Namens-/Copyright-Tabellen identifiziert.

Die anatomischen menschlichen Basismodelle sind laut ursprünglichem Builder-README Blender Human Base Meshes 1.4.1 (CC0). Diese fremde Basis wird getrennt von eigenen Kostümen, Waffen und Modelländerungen geführt. Das ursprüngliche Bundle-README muss zusätzlich archiviert werden, sobald es abrufbar ist.

## Was noch offen ist

1. Originalquellen/Lizenzen der sieben generisch benannten Ultimate-Sounds: `brakk ulti.wav`, `heiler ulti.wav`, `ione ulti.wav`, `sivra ulti.wav`, `tank ulti.wav`, `ulti traveler.wav`, `assasinen ulti.mp3`. Die vorhandenen Hashes belegen die Dateizuordnung, keine Rechte. Details: [Ergänzung zur früheren Prüfung](PriorAuditFindings.md).
2. Ursprüngliche Quellen und Nutzungsrechte der früher eingebrachten Helden-Bildreferenzen; mehrere Builder nennen „supplied reference“, ohne die konkrete Bildquelle zu speichern.
3. Herkunft/Nutzungsbeleg des hochgeladenen `Universe.png` sowie des Menüvideos/Posters und der Branding-Dateien, soweit keine vollständige Generierungsakte vorliegt.
4. Kontogebundene ursprüngliche Downloadbelege, falls vorhanden, für Mixamo und Pixabay. Öffentliche Anbieter-Lizenzangaben wurden erfasst, aber historische persönliche Downloadnachweise sind nicht vorhanden.
5. Der vorhandene Quaternius-SpaceKit-Beleg trägt im Text den Namen „Ultimate Platformer Pack“. Der packgenaue Originalbeleg muss ergänzt werden.
6. Assetgenaue Auflösung weiterer Icons, Bilder und aktueller Audio-Overrides; das Assetregister benennt diese offen, statt allen Dateien automatisch CC0 zuzuweisen.
7. Vollständige Notices der tatsächlich installierten Unity-/Serverpakete und zugehörige Kontonutzungsrechte.

## Modellstatus

Die neuen Konzepte sind gespeichert. Die bisherigen vier Modelländerungen vom 11.10.2026 werden als historische Revision gesichert. Ein abgeschlossenes neues Blender-/FBX-/Runtime-Build wird erst durch das jeweilige neue Export- und Validierungsprotokoll belegt. Ein Konzeptbild oder ein Commit dieser Dokumentation allein bedeutet nicht, dass alle neun Spielmodelle fertig umgebaut oder in Unity/Android geprüft sind.

Die Nachweise werden nicht rückdatiert, und bestehende Quellenlücken werden nicht durch nachträglich erstellte Bilder ersetzt.
