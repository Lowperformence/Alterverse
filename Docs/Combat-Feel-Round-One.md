# DIMENSIONS FRONT — erste Combat-Feel-Runde

Stand: 20.09.2026. Ausgangscommit: `ccadf6aaad971eedc0118a90d4462b04fc527844` auf `dev/vertical-slice`.

## Ergebnis und Umfang

Der vollständige Quellcode-Audit und die Matrix für **30 vorhandene Angriffe** stehen in [Combat-VFX-SFX-Matrix.md](Combat-VFX-SFX-Matrix.md). Sie enthalten pro Held Basic/Q/W/E/Ultimate, tatsächliche Funktion, Startup, Form, Flug, Impact, Ausklang, Farben, Klangprofil und Intensität.

Die erste Umsetzungsrunde verbessert die Basisangriffe, ausgewählte vorhandene Skill-Effekte, Warnflächen, Schildkontakt und den räumlichen Audiomix. Zusätzlich wurde auf ausdrücklichen Wunsch des Nutzers das sofortige Treffen geändert. Das ist eine echte Änderung des Trefferablaufs, keine reine VFX-Änderung.

| Held | Basic-Ausholen | Neue Darstellung / Treffer |
|---|---:|---|
| Zhaev Null | 0,10 s | Schmaler Klingenschnitt nach Ausholen, bestehende Void-Signatur |
| Auren Kest | 0,20 s | Kantiger Hammerdruck statt gemeinsamem Schwertbogen |
| Sivra Quell | 0,08 s + Flug | Schmaler Bolt aus der Mündung, Treffer erst nach Flug |
| Ione Vaska | 0,14 s + Flug | Kompakte arkane Kugel aus dem Orb, eigene Fluggeschwindigkeit |
| Tahla Venn | 0,12 s + Flug | Spitzes Schadens-Seed statt Heilblüte; Ursprung an der Stabspitze |
| Brakk Orun | 0,16 s | Linearer Faustimpuls und kurzer Druckring statt Schwertbogen |

Bisher sofortige offensive Einzelfähigkeiten erhalten ebenfalls Ausholen und, bei Fernkämpfern, Flugzeit. Sivras Ultimate startet nach 0,22 s und verschießt weiterhin vier Schüsse mit 0,18 s Abstand. Zhaevs verzögerte Ultimate und die bestehenden Flächen-Warnzeiten bleiben erhalten. Heilung, Schilde und Dashes aktivieren weiterhin wie bisher.

Die neu verzögerten Angriffe behalten das ursprünglich ausgewählte Ziel. Projektile folgen diesem Ziel visuell; es sind keine frei gezielten Skillshots mit neuer Kollisionsphysik. Ein ungültiges oder außerhalb der bisherigen Reichweite liegendes Ziel bekommt keinen Treffer. Tod, Deaktivierung oder Betäubung des Angreifers brechen ausstehende neue Treffer ab. Cooldowns werden nicht erstattet. Die bestehenden Flächenangriffe behalten dagegen ihre bisherigen Statusprüfungen bei Auflösung.

## Effekte und Audio

- Unterschiedliche Nahkampfformen für Zhaev, Auren und Brakk; Brakks Q nutzt ebenfalls den Faustimpuls.
- Tahla-Schaden verwendet die neue schmale Seed-Geometrie; Heilung behält breite Petals/Plus-Symbole.
- Bestehende kugelförmige AoE-Abfragen erhalten kreisförmige Warnränder. Die 48-seitige Außenkante ist umschrieben, sodass sie zwischen Eckpunkten nicht unter den Radius fällt (maximal ca. 0,215 % Überdeckung an den Ecken).
- Schildkontakt nutzt die tatsächliche Absorption aus `Health.Damaged`; er funktioniert auch bei einem brechenden Schild. Bei reinem Schildschaden entfällt der generische körperliche Partikelimpuls.
- Projektile und Schaden benutzen dieselbe Flugzeit. Flugdarstellung verfolgt das bewegte Ziel. Ausholphase und Kontaktpose sind getrennt.
- Vorhandene individuelle Hero-Cast/Impact-Clips bleiben erhalten. Release-Sounds der neuen Angriffe kommen beim Loslassen, Impact-Sounds nur bei bestätigtem Schaden/Absorption.
- Dezentes Links/Rechts-Panning und Distanzabschwächung über der Spielfläche; UI-Sounds werden bei Wiederverwendung einer Stimme wieder zentriert.
- Sivras Salven-Limiter ist kurz genug für vier hörbare Releases. Keine neuen Audioquellen und keine neu erzeugten Sounddateien.

## Geänderte Dateien

- `Scripts/Combat/HeroAttackDelivery.cs` + Meta: neu; gemeinsamer Treffer-Scheduler für Spieler und Bots, acht wiederverwendete Einträge pro Held.
- `Scripts/Combat/HeroCombatRules.cs`: Basic-Anfrage und tatsächliche Auflösung getrennt; Schadens-/Buff-/Lifesteal-Berechnung beibehalten.
- `Scripts/Characters/HeroAbilityExecution.cs`: bisher sofortige offensive Ziele und Salve an den Scheduler angebunden; Abbruch auf Lebenszykluswechsel.
- `Scripts/UI/HeroAttackCatalog.cs`: neue Ausholzeiten sichtbar.
- `Scripts/Visuals/HeroSkillVfx.cs`: unterschiedliche Kontaktformen, Schadens-Seed, Schildkontakt, Flugparameter, korrekte Warnform.
- `Scripts/Visuals/HeroVfxPool.cs`: Seed-Mesh, bewegte Flugziele, Projektilabbruch, umschriebene Kreisgeometrie.
- `Scripts/Visuals/HeroVisualMotion.cs`: neue Basic-Ausholphase, Kontakt/Recovery, passendere Release-Points.
- `Scripts/Combat/CombatFeedback.cs`: Absorptionsfeedback über tatsächliches Schadensereignis.
- `Scripts/Audio/HeroCombatAudio.cs` und `DimensionAudio.cs`: räumlicher Mix, Salven-Cue-Abstand und wiederverwendbares Panning.
- `Editor/AttackDeliveryRegression.cs` + Meta: neuer Play-Mode-Prüfpunkt.
- `Editor/HeroOverhaulRegression.cs`: bestehender Spieler/Bot-Vergleich wartet jetzt auf Ausholen/Flug und berücksichtigt die bereits vorhandene Bot-Reaktionszeit.
- Beide Combat-Dokumente: Audit, vollständige Matrix, Umsetzung und Testgrenzen.

Alle Pfade unter Scripts/Editor liegen in `Assets/Alterverse/`.

## Pooling und Kosten

Der vorhandene VFX-Pool bleibt bei 192 Render-Slots, davon 32 für Warnungen, mit gemeinsamem Material und MaterialPropertyBlock. Das Seed-Mesh entsteht einmal beim Aufwärmen. Neue Basics erzeugen keine Coroutine und keine neuen GameObjects/Materialien pro Treffer. Der Delivery-Controller wird einmal pro Held angelegt; danach nutzt er ein festes Array. Ohne ausstehende Angriffe endet sein Update sofort. Audio bleibt bei maximal acht vorhandenen Stimmen.

Die grundlegenden Release-Effekte belegen je nach Held drei bis vier Slots, dazu kommen Trefferfeedback und gegebenenfalls ein Schildfacet. Das sind Quellcode-Budgets, **keine gemessenen Draw Calls oder Millisekunden**. Exakte Kosten pro Q/W/E/R sowie die Leistung eines Zehn-Helden-Kampfs müssen im Unity Profiler und auf Android gemessen werden. Partikeldichte, Pool-Sättigung und transparente Überzeichnung sind weiterhin praktische Abnahmekriterien.

## Tatsächlich ausgeführte Prüfungen

- 115 geladene/geänderte C#-Dateien mit C#-Syntaxparser geprüft: keine Syntaxfehler.
- Zehn relevante unveränderte Gameplay-Dateien bytegenau gegen den Audit-Snapshot verglichen: HeroAbilityKit, HeroIdentity, HeroCombatProfile, HeroLocomotion, HeroMotor, Health, HeroCombatStatus, BasicAttack, MobaBotCombat und HeroBotAbilities.
- Methoden für Gegnerprüfung, Schaden, Intervall und Distanz sowie bestehende Flächenauflösung und verzögerte Einzeltreffer mit dem Ausgangsstand verglichen: unverändert.
- Kreisrand geometrisch geprüft: Seiten unterschreiten den angegebenen Radius nicht.
- Baseline-Ausholzeit plus maximale Flugzeit liegt bei allen sechs Basics unter dem bestehenden Basis-Angriffsintervall. Buffs und starke Frame-Einbrüche benötigen trotzdem Laufzeittests.
- Alle 53 vom vorhandenen Hero-Audio-Katalog angeforderten Clip-Pfade sind im Repository vorhanden. Das ist keine Hörprüfung.
- `git diff --check`: sauber.

**Compile-Status:** Unity-Compile nicht ausgeführt. Ein Syntaxparser ersetzt weder C#-Typprüfung noch Unity-Import/Build.

**Nicht ausgeführt:** Unity Play Mode, der neue Editor-Prüfpunkt, visueller Test, Hörtest, Android-Build, FPS-/GC-/Overdraw-Messung. Unity ist in dieser Umgebung nicht verfügbar.

## Prüfung in Unity

1. Branch aktualisieren und vollständigen Script-Import ohne rote Console-Fehler abwarten.
2. In einem entbehrlichen, laufenden Play-Mode-Match `DIMENSIONS FRONT → Checks → Attack delivery (Play mode)` ausführen. Der Prüfer testet sechs Basics, keine vorzeitigen/doppelten Treffer, Schildabsorption, Reichweite, Betäubung, Deaktivierung, Tod/Restore, DamageGate und die neu verzögerten Q-Fähigkeiten. Er verändert nur seine eigenen Testobjekte/Zeitstempel; durch normale Todesereignisse können Test-Pickups entstehen, daher anschließend das Match neu starten.
3. Jeden Helden aus normaler Top-Down-Kamera spielen: Basic, Q, W, E, Ultimate. Bei bewegten Zielen Projektilankunft und HP-Abzug vergleichen. Bei Sivra vier Salvenschüsse zählen.
4. Vor Release und während Flug außer Reichweite laufen, Ziel töten, Angreifer betäuben/töten, deaktivieren und respawnen: kein später Geistertreffer, kein falscher HP-Flash.
5. Aurens Kreisränder, Tahlas Schadens-/Heilformen und Schildbruch gesondert vergleichen; auch monochrom und bei beiden Teams.
6. Im Zehn-Helden-Kampf auf Android Profiler, GC-Allokationen nach Aufwärmen, Pool-Sättigung und 60-FPS-Ziel prüfen. Sound über Smartphone-Lautsprecher und Kopfhörer anhören.

## Noch keine vollständige Art-Abnahme

Die 30 Karten sind vollständig erfasst, aber nicht alle Q/W/E/R-Effekte wurden in dieser ersten Runde neu gestaltet. Eigener-Held-Audiopriorität, konkrete neue Soundproduktionen, alle Ultimate-Signature-Momente und endgültige visuelle Qualität bleiben weitere Arbeit. Die gesamte ursprüngliche Definition of Done ist damit ausdrücklich noch nicht als erfüllt markiert.
