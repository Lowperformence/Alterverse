# Planetengerechte Landschaftsdetails — 01.10.2026

## IMPLEMENTED

Acht spiegelbildlich angeordnete Landschaftsgruppen pro Arena, mit grossen charakteristischen Formen, sekundären Materialschichten und kleinen Bodenfragmenten. Automatisch an den bestehenden CosmicArenaBackdrop angeschlossen, den der reale Arenaaufbau für alle Varianten erzeugt. Kein manueller Szenenimport nötig.

| Map | Neue Gestaltung |
| --- | --- |
| Moon | Niedrige gebrochene Auswurfränder, kantige Regolithblöcke, feines Gestein |
| Mars | Rotbraune, horizontal geschichtete Mesas, erodierte Platten, flache Sandrippen |
| MOOR12C | Gebogene ausserirdische Blätter, organische Mineralstängel, kleine gelbgrüne Spitzen |
| Noxara | Geneigte dunkelblaue Schieferfächer mit schmalen kühlen Mineraladern |
| Kryon | Aufgebrochene Eisplatten, kantige Eisspitzen, helle Schneeauflagen |
| Earth | Farnrosetten, moosbedeckte Felsen, liegende verwitterte Stämme |
| Pyraxis | Eng stehende Basaltsäulen, dunkle Kruste, schmale glühende Lavafugen |
| Neptune | Verzweigte Tiefseekorallen, feuchte mineralische Sockel, kleine biolumineszente Spitzen |
| DARK 00 | Dunkle technische Sockel, gerippte Maschinengehäuse und zurückhaltende weisse Lichtstreifen |
| PSR J1719-1438 b | Facettierte Mineralbetten, unterschiedlich geneigte Prismen und gebrochene Kristallsplitter |
| GJ 504 b | Magentafarbene Sandsteinschichten, schlanke Erosionssäulen mit breiten Kappen, Sandrippen |

Die bestehenden transparenten PSR-Kristalle und der Wächter bleiben erhalten; die neuen Mineralbetten sind opak. Der gemeinsame Weltraumhintergrund wird nicht je Planet unterschiedlich gestaltet.

Die neuen Objekte haben keine Collider, zusätzlichen Lichter oder Gameplay-Komponenten. Alle Details liegen im äusseren Streifen der Arena. Materialien werden je Planet geteilt; Geometrie wird pro Region und Material zusammengeführt (höchstens 32 MeshRenderer). Regionale LODGroups erlauben Sichtbarkeits-Culling. Kein Update pro Detail, kein Eingriff in den globalen Zufallszustand. Erzeugte Materialien und Meshes werden bei Zerstörung des Besitzers freigegeben.

Zusätzlich nutzen die bestehenden Earth/Pyraxis/Neptune-Themen sowie ihre gemeinsame Kreaturen-Materialfabrik Resources-basierte RuntimeShaderLibrary-Materialien. Damit entfällt deren alleinige Abhängigkeit von möglicherweise gestrippter Shader.Find-URP/Built-in-Materialauswahl. Temporäre Primitive-Collider werden sofort deaktiviert, bevor Unity sie am Frame-Ende entfernt.

## VERIFIED

- Vier geänderte/neue C#-Dateien mit tree-sitter-c-sharp ohne Syntaxfehler geprüft.
- Eigene Gestaltungsfunktion für alle elf ArenaVariant-Werte geprüft.
- Paarweise Positionssymmetrie und konservativer 3,5-m-Radius gegen Rand-/Lanegrenzen geprüft.
- Dreiecksorientierung der generierten Säulen, Prismen und Strukturprofile mathematisch geprüft.
- Anbindung im aktuellen PrototypeBootstrap.BuildArena geprüft: CosmicArenaBackdrop wird unabhängig von der Planetenvariante angelegt.
- Keine zusätzlichen Gameplay-Collider, Lichter, globalen Zufallszugriffe oder Update-Schleifen in der neuen Landschaftskomponente.

## NOT VERIFIED

Unity ist in dieser Umgebung nicht installiert. Syntaxprüfung ist keine Unity-Kompilierung. Darstellung aus der echten Spielkamera, Überschneidungen mit bereits vorhandenen Dekorationen, Shader-Import und reale Android-Framerate/GPU-Speichernutzung sind hier nicht getestet. Der Renderer-Höchstwert beschreibt die neue Komponente, nicht die gesamte Szene.

## OWNER ACTION REQUIRED — Laufzeit-Abnahme

Unity öffnen, Console prüfen und im Play Mode `DIMENSIONS FRONT/Checks/All planet landscape details (Play Mode)` ausführen. Dieser beigefügte Check baut alle elf Varianten kurzzeitig auf, prüft Meshes/Shader, vollständige Randlage, Höhenlimit, Batchbudget, Colliderfreiheit und wiederholten Build. Er stellt die vorherige Variante wieder her. Danach jede Map aus der Spielkamera ansehen, einen Matchwechsel durchführen und auf Android Laufzeit/Materialzahlen prüfen. Der Check wurde hier nicht ausgeführt.
