# Epic Toon FX – individuelle Angriffsgestaltung

40 Aktionen von acht Helden verwenden eigene Angriffskonzepte. Die [Konzeptliste](Hero-VFX-Concepts-2026-10-24.md) beschreibt Form, Atmosphäre, Farbe und gekaufte Vorlagen; die [Einzelcheckliste](Hero-VFX-Checklist-2026-10-24.md) führt die tatsächlich beobachteten Runtime-Schlüssel. Auf ausdrücklichen Nutzerwunsch behalten Nerok und Andromeda Timmy vollständig ihre ursprünglichen Effekte. Timmys ursprünglicher dreifacher Klauenhieb, Scratch-Klauen, Düsen und Staubwolke sind wiederhergestellt. Neroks Anker, Kette und Wasser-Ultimate bleiben erhalten. Die bevorzugten alten Schildkörper bleiben ebenfalls erhalten.

Die bisherige pauschale Familienzuordnung war zu klein und zu ähnlich. Die neue Gestaltung verwendet pro Basisangriff/Q/W/E/R einen eigenen Konzeptschlüssel. Vorbereitung, Hauptform und Treffer verwenden dazu passende Varianten. Schwertwellen, gekreuzte Hiebe, Gravitätsachten, spitze Nadeln/Dornen, rotierende Klingenringe, Runenkränze, Blätter-/Heilblüten und Kollapswirbel erzeugen unterschiedliche Silhouetten. Bestehende Strahlen behalten ihre genaue Funktion.

## Größe und Phasen

- Neue kosmetische Effekte werden auf Nutzerwunsch **um 200 Prozent vergrößert: Faktor 3** gegenüber der bisherigen Bibliotheksskalierung.
- Hauptkompositionen der Ultimates erhalten zusätzlich Faktor 1,4. Ihre Vorbereitungen verwenden vier Elemente, ihre Treffer drei gefächerte Elemente. Die unterschiedlichen vorhandenen Zeitpunkte bleiben unverändert.
- Strahlen wachsen nur in der Breite. Ihre Länge, Ursprung und Endpunkt bleiben korrekt. Exakte Warnränder behalten den tatsächlichen Gameplay-Radius.
- Der bisherige kosmetische Renderer wird ausgeschaltet, wenn die neue Komposition zeichnet. Mehrere native Schnitte, Runen oder Partikellagen gehören dabei zu einer gemeinsam verwalteten Komposition und erzeugen keine zusätzlichen Gameplay-Treffer.
- Neroks und Timmys alte Effekte sowie die alten Schildkörper werden nicht durch den neuen Größenfaktor verändert.

## Gekaufte Werkzeuge und Emission

33 Quellfamilien bilden **119 individuelle Phasenvarianten**: 39 native Hauptformen, 40 Vorbereitungen und 40 Treffervarianten. Der einzige explizite Prismastrahl wird weiterhin als exakte Runtime-Geometrie gezeichnet. Insgesamt enthält die erzeugte Bibliothek 152 Einträge. Die Konzepte verwenden neben den vorhandenen Quellen nun auch SwordWave, SwordSpin, SharpMissile und die deutlich gerichtete ToonPunch-Vorlage. Farbpaletten werden weiterhin aus den tatsächlichen Helden-/Fähigkeitsfarben übernommen.

Die Paketquellen enthalten Geschossteile, die ausschließlich über zurückgelegte Distanz emittieren. Beim manuell getakteten Pool fehlten damit Schweife auch bei Vorlagen, deren runder Kern bereits sichtbar war. Alle solchen Systeme werden in den erzeugten Kopien auf begrenzte Zeitemission mit Startpartikel umgestellt und verwenden Weltraum-Simulation: Bereits emittierte Schweifpartikel bleiben hinter dem fliegenden Kopf zurück. Die Distanzemission wird abgeschaltet, damit Bewegung nicht doppelt emittiert. Statische Schnitt-, Runen- und Flächensysteme behalten lokale Simulation.

Eine komplette Komposition bleibt auf höchstens 96 Partikel begrenzt; gekreuzte Hiebe und Runenkränze vervielfachen diese Obergrenze nicht. Der bestehende Cache ist auf 160 Instanzen begrenzt und recycelt freie Varianten. Erzeugte Vorlagen enthalten nur visuelle Komponenten, keine Demo-Scripts, Audioquellen, Lichter oder Collider. Original-Prefabs und gekaufte Texturen werden nicht überschrieben.

## Einrichtung

1. Den vollständigen gekauften Ordner unter `Assets/Epic Toon FX/` behalten. Der Commit enthält keine gekauften Binärassets.
2. Play Mode beenden und den VFX-Branch pullen. Unity 6000.6.1f1 kompilieren lassen. Eine alte/unvollständige Bibliothek wird automatisch als **Version 5** neu aufgebaut. Manuell: `DIMENSIONS FRONT > VFX > Build Epic Toon replacement`.
3. `DIMENSIONS FRONT > VFX > Validate Epic Toon replacement` prüft sämtliche Quell- und Konzeptvarianten. Ein leerer zweiter Materialplatz bei deaktivierten Trails ist zulässig; Hauptmaterialien und aktive Trails müssen konvertiert sein. Fehlende Angriffskonzepte blockieren Client-Builds. Der Linux-Dedicated-Server ist ausgenommen.
4. Nach Import läuft einmal pro Bibliotheks-/Backend-Stand eine isolierte native Vorschauprüfung. Unter `Library/DF_EpicVfxReview/` entstehen `report.json`, Einzel-PNGs, `contact-sheet.png` und die beschriftete `index.html`. Der Report nennt auch Held, Konzept und Phase. Wiederholen: `DIMENSIONS FRONT > VFX > Render Epic Toon visibility review`.
5. Die Vorschau verwendet einen festen weiteren Bildausschnitt für die vergrößerten Kompositionen und repräsentative Fähigkeitspaletten. Sie prüft Partikel, Shader und tatsächlich abweichende Pixel. Portal-/Vortex-Hauptformen benötigen mindestens 24 sichtbare Pixel. Das ist keine Echtspiel-Abnahme.
6. Im echten Kampf alle 40 Aktionen aus der Spielkamera prüfen: eindeutige Silhouette, Größe relativ zum Helden, Lesbarkeit der Ultimate, Trefferzuordnung, Landung, Abbruch und Android-Bildrate. Die Checkliste lässt diese Sichtabnahmen ausdrücklich offen.

Erzeugte Dateien samt `.meta` unter `Assets/Alterverse/Resources/CombatVfx/EpicToon/` und die referenzierten gekauften Assets gehören in das private Projekt. Alte Console-Meldungen nach einem Update mit `Clear` entfernen.

## Lebensdauer und Netzwerk

Die bestehenden Slots bestimmen Start, Verzögerung, Bewegung, Zielverfolgung, Ablauf und Abbruch. Die nativen Partikel werden vor manueller Simulation explizit gestartet und anschließend pausiert. Beim Verbergen werden sie geleert. Die Zuordnung zu Held/Aktionsslot/Phase ist lokaler, verschachtelbar wiederhergestellter Präsentationskontext. Sie erzeugt keine weiteren Netzwerkereignisse und ändert kein Paketformat.

Ein nicht unterstützter alter HeroEnergy-Shader blockiert einen unterstützten Epic-Backend-Start nicht. Fehlen vorübergehend native Partikel oder ein unterstütztes Material, zeichnet genau ein Ersatz-Mesh, während die nativen Renderer ausgeschaltet sind. Die Uhr läuft weiter. Bei Erholung übernimmt ausschließlich der neue Effekt ohne Neustart. Eine einmalige Warnung nennt die betroffene Vorlage.

## Prüfung und Grenzen

- **9.679 Runtime-/Netzwerk-/VFX-Prüfungen bestanden:** echte Ausführung aller 40 umgestellten Aktionen mit eigenen Konzeptschlüsseln, exklusive Renderer, Original-Timmy/Original-Nerok, Ablauf/Abbruch, Gameplay-Parität bei 30/60/120 FPS, dreifache Skalierung, genaue Strahlenden und kein zusätzliches Netzwerkereignis durch den Gestaltungskontext.
- **321 Editor-Prüfungen bestanden:** tatsächlicher Produktionscode für Materialvalidierung, Emissionsanpassung, Portal-Ausrichtung und Erzeugung aller 152 Einträge mit kontrollierten Prefab-/Transformadaptern; maximal 96 Partikel pro kompletter Signatur, keine Veränderung gemeinsam genutzter Quellen, fehlende Phasenvarianten werden erkannt.
- **1.332 Aiming-/Skill-Entry-Prüfungen bestanden:** einschließlich der wiederhergestellten beiden Timmy-Staubschichten, je zwei ursprünglichen Ausstoßkernen pro Düse und drei Scratch-Klauen. C#-Syntaxprüfung ohne Fehler.

Hier läuft kein nativer Unity-Editor. Diese Prüfungen bestätigen weder den ästhetischen Gesamteindruck noch Android-Leistung. Der erste native Nutzerbericht für Version 2 bestand 25/29 Quellfamilien; Version 3 bestand 28/29. Daraufhin wurden Portal und Vortex als kameraorientierte beziehungsweise horizontale Flächen korrigiert. Die anschließende Nutzerrückmeldung lautete, dass die Effekte erscheinen, aber zu klein und zu gleichförmig sind. Diese Rückmeldung ist der Anlass für die neue Gestaltung; die alten Vorschautests sind keine Abnahme von Version 5.

Schadens-, Bewegungs-, Kollisions-, Reichweiten-, Cooldown- und Audio-Produktionsdateien wurden nicht geändert. Die Sound-/Timingarbeit bleibt auf Nutzerwunsch zurückgestellt.
