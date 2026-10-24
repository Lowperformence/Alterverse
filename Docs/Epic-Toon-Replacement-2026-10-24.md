# Epic Toon FX – Austausch der Kampfeffekte

Die kosmetischen Angriffseffekte verwenden eine aus dem gekauften Epic-Toon-Import erzeugte Bibliothek. Ein belegter Effektplatz zeichnet entweder das neue Partikel-Prefab oder seinen bisherigen Mesh-Renderer. Bei einem Partikel-Prefab ist der alte Renderer ausgeschaltet. Die beiden zusätzlichen Cooldown-Beobachter sind deaktivierte Kompatibilitätskomponenten; ihre Script-GUIDs bleiben erhalten.

## Einrichtung im Unity-Projekt

1. Diesen Branch übernehmen und den vollständigen gekauften Ordner unter `Assets/Epic Toon FX/` im Projekt behalten.
2. Unity 6000.6.1f1 öffnen und den Import abschließen lassen. Wenn die Bibliothek fehlt und der gekaufte Import vorhanden ist, startet die Erzeugung automatisch nach dem Kompilieren.
3. Bei Bedarf manuell `DIMENSIONS FRONT > VFX > Build Epic Toon replacement` ausführen; danach `Validate Epic Toon replacement`.
4. Die erzeugten Dateien samt `.meta` unter `Assets/Alterverse/Resources/CombatVfx/EpicToon/` gemeinsam mit den referenzierten gekauften Assets im privaten Projekt sichern.

Die Erzeugung kann wiederholt werden. Sie schreibt ausschließlich die erzeugte Bibliothek, Prefabs und Materialien. Original-Prefabs und URP-Projekteinstellungen werden nicht überschrieben. Das separate URP-Upgrade des Herstellers ist für diese konvertierten Prefabs nicht erforderlich: sie verwenden den mitgelieferten URP-Shader `DimensionsFront/EpicParticles`. Client-Builds werden ohne vollständige erzeugte Bibliothek abgebrochen; ein Linux-Dedicated-Server benötigt sie nicht. Im Editor bleibt bei fehlender Bibliothek ein protokollierter Fallback verfügbar.

## Zuordnung

| Held | Neue Effektfamilien und Farben |
|---|---|
| Zhaev Null (`Veyr`) | Violette Risse, Schwertschnitte, Schwerttreffer |
| Auren Kest (`Kaelon`) | Schwere Einschläge, Runen und Schilde; Q violett, E blau, Ultimate gelb |
| Sivra Quell (`Nyra`) | Blauer durchgehender Q-Strahl mit Pakettextur, weiße E-Kugel und einzelne Fesselrune, rote übrige Energie |
| Ione Vaska (`Elyra`) | Energiegeschosse, Portale, Runen und Nova in der vorhandenen Heldenfarbe |
| Tahla Venn (`Mira`) | Goldene Angriffsenergie, grüne Heilung und Unterstützung |
| Brakk Orun (`Rook`) | Orange Feuerenergie, metallische Explosionen, schwere Einschläge |
| THE TRAVELER (`Traveler`) | Violetter Basisangriff, goldene Q/W-Energie und Portale, violettes E, weißes Ultimate |
| Nerok Abyss (`Nerok`) | Cyanblaue Wasserspritzer, Wirbel und Schilde; physischer Anker und Kette bleiben als Waffenmodelle erkennbar |
| Andromeda Timmy (`Timmy`) | Cyanfarbene Klauenschnitte, Rauch und je ein Jet-Effekt pro Düse; Granatenmodell bleibt erkennbar |
| Void Knight | Violette schwere Schnitte und Treffer, einzelner exakt begrenzter Stoßstrahl |

21 Einträge decken Schnitte, Treffer, Geschosse, Runen, Schutz, Heilung, Portale, Wasser, Rauch, Explosionen und Jets ab. Portal und Wirbel teilen bewusst eine gekaufte Vorlage. Die ausgewählten Vorlagen nutzen 28 Pakettexturen sowie die horizontale Verlaufsmaske für maßgenaue Strahlen. Glanz, Alpha und Texturformen werden übernommen und mit der jeweiligen Fähigkeitsfarbe eingefärbt. Die Effektmasken eignen sich für Kampfenergie und Partikel; sie werden nicht als Boden-, Charakter- oder UI-Oberflächen eingesetzt.

Exakte Warngeometrie, Anker/Kette, Timmy-Granate und physische Monster-Geschosse behalten ihre geometrische Darstellung. Das erhält die erkennbaren Gefahrenflächen und Waffen. Kosmetische Mesh-Stapel und alte zusätzliche Atlas-Ebenen im Helden-Facade werden entfernt. Zusammengesetzte gekaufte Prefabs können mehrere Partikelsysteme enthalten; sie laufen gemeinsam als ein Effekt unter derselben Lebensdauer.

## Lebensdauer und Begrenzung

Die bestehenden Slots bestimmen weiterhin Startzeit, Verzögerung, Bewegung, Zielverfolgung, Ablauf und Abbruch. Die Partikel werden von dieser Uhr simuliert und pausiert; sie erzeugen keine eigenständigen Treffer oder Zeitabläufe. Beim Verbergen werden Partikel sofort geleert und wiederverwendet. Warnplätze bleiben reserviert; der Partikel-Cache ist auf 160 Instanzen begrenzt. Die ausgewählten Vorlagen enthalten zwei bis fünf Systeme und erhalten maximal 96 Partikel pro Effekt. Demo-Scripts, Audio, Lichter, Collider und Subemitter werden aus den erzeugten Prefabs entfernt; Partikelkollisionen und Trigger werden abgeschaltet.

## Prüfung und verbleibende Abnahme

`python Tools/Multiplayer/VfxRegression/run.py --dotnet-root <SDK8>`: 5.501 Prüfungen bestanden. Der Harness kompiliert den tatsächlichen Runtime-Code mit kontrollierten Unity-/Netzwerk-/Partikel-Adaptern. Er prüft unter anderem exklusiven Rendererbesitz, Verzögerung, Farbe, Abbruch, Wiederverwendung, Tod, verspätete Netzwerkereignisse, Warnreserve sowie Schaden/Heilung/Schilde, Bewegungsziel, Cooldown und Ereignisfolge bei 30/60/120 FPS. Der Negativlauf `--negative-control` deaktiviert den Austausch absichtlich und scheitert an der Prüfung auf verbliebene alte kosmetische Kerne.

Die älteren Aiming-/Geometrie-Harnesses erhalten einen ausdrücklich deaktivierten Partikeladapter; der echte neue Backend-Code wird im Multiplayer-VFX-Harness geprüft. Alte Atlas- und Stapel-Erwartungen werden nicht als Abnahmekriterium für den neuen Look verwendet.

Hier ist kein nativer Unity-Editor verfügbar. Die Editor-Konvertierung, Shader-Kompilierung, tatsächliche Darstellung und Leistung auf Android wurden deshalb noch nicht ausgeführt. Vor dem Zusammenführen müssen alle zehn Helden mit Basisangriff/Q/W/E/Ultimate, Schildverlust, Dash-Abbruch und Netzwerk-Replay in Unity geprüft werden; anschließend ein Android-Test mit mehreren gleichzeitigen Kämpfern. Größe, Orientierung, Transparenz und Bildrate sind die offenen visuellen Abnahmepunkte. Gameplay-, Balance-, Kollisions- und Netzwerk-Produktionsdateien werden in dieser Änderung nicht bearbeitet.
