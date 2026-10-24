# Epic Toon FX – aktuelle VFX-Migration

Neun Helden mit jeweils Basisangriff/Q/W/E/Ultimate werden umgestellt. Die [Einzelcheckliste](Hero-VFX-Checklist-2026-10-24.md) führt alle 50 Katalogeinträge: 45 Code-Prüfungen und fünf ausdrücklich ausgenommene Nerok-Aktionen. Der ursprüngliche Nerok-Snapshot und seine Beobachter sind nur für Nerok aktiv. Anker, Kette, Wasser-Ultimate und seine restlichen bisherigen Effekte bleiben beim alten Aufbau. Der vom Nutzer bevorzugte alte Schildkörper bleibt auch bei den anderen Helden erhalten.

Ein neuer Partikel-Effekt schaltet den bisherigen kosmetischen Mesh-Renderer seines Slots aus. Die alten zusätzlichen Cooldown-Beobachter sind für die anderen neun Helden deaktiviert. Exakte Warngeometrie und physische Granaten-/Monster-Geschossmodelle behalten ihre Form. Kein Schaden, Ziel, Bewegungspfad, Gameplay-Zeitpunkt, Cooldown oder Kollisionscode wird geändert.

## Zuordnung nach Funktion und Farbe

- Klingen und Klauen verwenden Schwert-/Klauenschnitte und passende Kontakteffekte. Timmys Claw-Prefab gruppiert drei gekaufte Mini-Schnitte als eine gemeinsame Darstellung.
- Fernkampf und Zauber verwenden bewegte Geschossvorlagen. Tahlas Angriffe verwenden NatureMissile/NatureImpact statt eines Heilsymbols. Ione verwendet ArcaneMissile/MagicImpact, Zhaevs fliegender Riss ShadowMissile.
- Faustwellen verwenden die gekaufte gerichtete SoftPunchHeavy-Vorlage. Schwere Kontakte verwenden RoundHit.
- Flächen verwenden Nova-, Feuer-Nova-, Natur-Nova- oder Runeneffekte an den vorhandenen Zielpunkten. Warnränder markieren weiterhin die tatsächliche Gefahrenfläche.
- Sprünge erhalten Effekte bei Absprung und Landung; die Landung folgt dem tatsächlichen Helden. Timmys zwei Düsen bleiben mit je einem Jet-Effekt an ihren vorhandenen Sockets befestigt.
- Heilung verwendet Heal; sie wird nicht als Angriffsprojektil verwendet. Schildkörper behalten ihre alte Darstellung.
- Helden-/Fähigkeitsfarben werden als Materialfarbe übertragen. Sivra: Basis/W cyan, Q blau, E weiß, R rot. Tahla: goldene Angriffe, grüne Heilung. Weitere Einzelwerte stehen in der Checkliste.

29 konvertierte Bibliothekseinträge decken diese Funktionen ab. Die Pakettexturen bleiben Effektmasken und werden nicht als Boden- oder Charakteroberflächen zweckentfremdet. Original-Prefabs und Projekteinstellungen werden nicht überschrieben. Die erzeugten Prefabs enthalten nur visuelle Komponenten, keine Demo-Scripts, Lichter, Audioquellen, Collider oder Subemitter.

## Einrichtung und Diagnose

1. Vollständigen gekauften Ordner unter `Assets/Epic Toon FX/` behalten. Der Commit enthält keine gekauften Binärassets.
2. Unity 6000.6.1f1 den Branch importieren und kompilieren lassen. Eine fehlende, unvollständige oder veraltete Bibliothek wird danach neu aufgebaut. Alternativ: `DIMENSIONS FRONT > VFX > Build Epic Toon replacement`.
3. `DIMENSIONS FRONT > VFX > Validate Epic Toon replacement` prüft die erzeugten Assets. Ein leerer zweiter Materialplatz bei deaktivierten Partikel-Trails ist gültig; Hauptmaterialien und aktive Trails müssen ein konvertiertes Material besitzen.
4. Nach dem Import läuft einmal pro Bibliotheks-/Backend-Stand automatisch eine native Vorschauprüfung in einer isolierten Szene: alle 29 Vorlagen werden mit der aktiven Render-Pipeline gezeichnet und gegen einen leeren Kameraframe verglichen. Die Ausgabe liegt unter `Library/DF_EpicVfxReview/`: `report.json`, je ein PNG, `contact-sheet.png` und eine beschriftete `index.html`. Nur erzeugte Partikel, unterstützte fehlerfreie Shader und tatsächlich abweichende Bildpixel ergeben PASS. Die Farben sind repräsentativ; dies prüft keine Echtspiel-Ausrichtung oder Android-Leistung.
5. Manuell wiederholbar: `DIMENSIONS FRONT > VFX > Render Epic Toon visibility review`. Die zusätzliche reine Partikelprüfung bleibt unter `Test Epic Toon emission` erreichbar.
6. Erzeugte Dateien samt `.meta` unter `Assets/Alterverse/Resources/CombatVfx/EpicToon/` und die referenzierten gekauften Assets im privaten Projekt sichern.

Client-Builds benötigen die gültige Bibliothek; der Linux-Dedicated-Server ist ausgenommen. Die alten Console-Meldungen bleiben nach einem Code-Update stehen, bis `Clear` gedrückt wird.

### Korrektur nach dem nativen Nutzerbericht

Der erste Nutzerbericht unter Unity 6000.6.1f1 / Direct3D11 mit Bibliotheksversion 2 bestand 25 von 29 Vorlagen. Portal und Vortex erzeugten je 14 Partikel, aber nur einen sichtbaren Pixel; ShadowMissile und Fire erzeugten keine Partikel. Die Paketquellen erklären beide Fälle: Die sichtbaren Portal-Scheiben emittieren erst nach 0,5 Sekunden, während sämtliche Systeme von ShadowMissile und Fire ausschließlich über zurückgelegte Distanz emittieren.

Bibliotheksversion 3 startet die Portal-Scheiben mit einem zusätzlichen Partikel zum Zeitpunkt null. Bei den beiden Geschossfamilien wird die reine Distanzemission der erzeugten Kopien durch begrenzte Zeitemission mit einem Startpartikel ersetzt; die Distanzemission wird dabei abgeschaltet. Dadurch hängt der Partikelstart nicht mehr von einer bereits zurückgelegten Strecke ab und die Bewegung erzeugt keine zweite Emission. Texturen, Farben, Größen, Partikelobergrenzen und die vorhandene Slot-Zeit bleiben erhalten. Die anderen 25 Familien werden nicht angepasst. Nach dem Pull baut Unity die veraltete Bibliothek automatisch neu auf und wiederholt denselben nativen Sichtbarkeitstest. Dessen Ergebnis für Version 3 steht noch aus; die Korrektur ist hier ohne nativen Unity-Editor nicht als 29/29 bestanden bestätigt.

## Lebensdauer und Prüfung

Die bestehenden Slots bestimmen Start, Verzögerung, Bewegung, Zielverfolgung, Ablauf und Abbruch. Nach Stop/Clear bzw. Pause wird das native Partikelsystem vor Simulate ausdrücklich mit Play gestartet; anschließend pausiert es wieder. Damit bleibt der Slot die einzige Zeitquelle. Beim Verbergen wird sofort geleert und wiederverwendet. Der Cache bleibt auf 160 Instanzen begrenzt, die Warnreserve getrennt und die ausgewählten Vorlagen auf höchstens 96 Partikel pro Effekt begrenzt.

Der neue Backend-Start ist vom alten HeroEnergy-Shader entkoppelt: Ist dieser nicht unterstützt, verwendet der Pool den unterstützten Epic-Shader mit einer weißen Textur für die bestehenden Warngeometrien und behält neue Partikel aktiv. Fehlen vorübergehend native Partikel oder ein unterstütztes Material, zeichnet nur ein Mesh als Ausfallschutz; die nativen Renderer sind dann deaktiviert, ihre Uhr läuft weiter. Sobald die Partikel bereit sind, wird ausschließlich wieder der neue Effekt gezeichnet. Ein dauerhafter Fehler wird einmal pro Familie protokolliert. Das ersetzt die noch offene native Sichtprüfung nicht.

Der Runtime-Harness kompiliert den tatsächlichen Runtime-Code mit kontrollierten Unity-/Netzwerk-/Partikel-Adaptern. Er führt alle neun Heldenaktionen durch, prüft expliziten Partikelstart, exklusiven Rendererbesitz, Ablauf/Abbruch, Farben, Warnreserve und identische Gameplay-Ergebnisse mit und ohne Renderer bei 30/60/120 FPS. Zusätzlich prüft er den Start bei ausgefallenem altem Shader, weiter sichtbare maßgenaue Warngeometrie, leere native Emission und die exklusive Rückkehr ohne Neustart sowie Neroks ursprüngliche Anker-Rezeptur, einzelne Kette mit Zielverfolgung und die wiederhergestellten Wasser-Layer seiner Ultimate. 10.491 Runtime-Prüfungen bestanden. Der Editor-Harness hat 58 Prüfungen für Materialvalidierung, veraltete Bibliotheken und die Emissionskorrektur der vier fehlerhaften Familien; die anderen 25 Familien bleiben unverändert. Aiming/Skill-Entry hat separat 1.330 Prüfungen bestanden. Die 29 Quell-Prefabs wurden im Upload auf vorhandene Partikelsysteme, aktive Renderer und benötigte Materialplätze geprüft.

Hier steht kein nativer Unity-Editor zur Verfügung. Die gemeldete fehlende Darstellung ist deshalb erst nach dem nativen Emissionstest und den Sichtprüfungen als behoben bestätigt. Vor dem Zusammenführen müssen Größe, Orientierung, Farbe, Transparenz, Abbruch und Android-Leistung im Spiel geprüft werden. Die Angriffssounds und ihr Timing werden in diesem VFX-Update nicht geändert; die Sound-Abnahme ist auf Nutzerwunsch zurückgestellt.
