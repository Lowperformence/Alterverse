# Einzelcheckliste der Heldenaktionen

45 Aktionen von neun Helden werden auf neue Epic-Toon-Effekte geprüft. Neroks fünf Einträge sind ausdrücklich vom Austausch ausgenommen; seine ursprüngliche Darstellung einschließlich Anker, Kette und Ultimate ist wiederhergestellt. Die Schildkörper behalten den vom Nutzer bevorzugten alten Look. Ihre Cast-/Kontakt-Hinweise können neue Effekte verwenden.

`Code geprüft` bedeutet: Der C#-Harness hat die tatsächliche Basisangriffs-/Fähigkeitsausführung durchlaufen und beobachtet, dass zugehörige neue Effekte erzeugt werden, der alte kosmetische Renderer dabei ausgeschaltet bleibt und die Partikel explizit gestartet werden. Dies ist kein Nachweis für native Darstellung, Farbeindruck, Größe oder Bildrate in Unity. Die eigene Spalte für diesen Sichttest bleibt offen.

Die Funktion kommt aus dem echten Aiming-Katalog. `Dash` wird hier entsprechend dem Nutzer als Sprung geführt. Kein Bewegungscode wird geändert. Effektfamilien sind die während einer vollständigen Aktion beobachteten Familien; unterstützende Selbstheilung und Treffer-Feedback können zusätzlich vorkommen.

| Held | Aktion | Funktion | Farbzuordnung | Beobachtete Effektfamilien | Code | Unity-Sichttest |
|---|---|---|---|---|---|---|
| Zhaev Null | Basisangriff | Nahkampf | Violett | Slash, SwordImpact | [x] | [ ] Offen |
| Zhaev Null | Null Cut | Nahkampf | Violett | Portal, Slash, SwordImpact | [x] | [ ] Offen |
| Zhaev Null | Rift Scar | Fernkampf / Schattenriss | Violett | Nova, Portal, ShadowMissile, SwordImpact | [x] | [ ] Offen |
| Zhaev Null | Gapfold | Sprung | Violett | Portal, Smoke | [x] | [ ] Offen |
| Zhaev Null | Vacant Echo | Nahkampf | Violett | Portal, Slash, SwordImpact | [x] | [ ] Offen |
| Auren Kest | Basisangriff | Nahkampf | Gold | HeavyImpact, Punch | [x] | [ ] Offen |
| Auren Kest | Anchor Strike | Fernkampf / Faustwelle | Violett | HeavyImpact, Impact, Punch, Rune | [x] | [ ] Offen |
| Auren Kest | Lumen Wall | Selbstschutz | Gold | Rune | [x] | [ ] Offen |
| Auren Kest | Gravity Anchor | AOE am Zielpunkt | Blau | HeavyImpact, Impact, Nova, Rune | [x] | [ ] Offen |
| Auren Kest | Kest Citadel | AOE um den Helden | Gelb | Heal, HeavyImpact, Impact, Nova, Rune | [x] | [ ] Offen |
| Sivra Quell | Basisangriff | Fernkampf / Geschoss | Cyan | Impact, Missile | [x] | [ ] Offen |
| Sivra Quell | Prism Shot | Strahl | Blau | ExactBeam, Impact | [x] | [ ] Offen |
| Sivra Quell | Vector Shift | Sprung | Cyan | Impact, Smoke | [x] | [ ] Offen |
| Sivra Quell | Light Snare | Fernkampf / Geschoss | Weiß | Impact, Missile, Rune | [x] | [ ] Offen |
| Sivra Quell | Sightline Break | Fernkampf / Salve | Rot | Impact, Missile | [x] | [ ] Offen |
| Ione Vaska | Basisangriff | Fernkampf / Geschoss | Violett | ArcaneMissile, MagicImpact | [x] | [ ] Offen |
| Ione Vaska | Orbit Fracture | AOE am Zielpunkt | Violett | ArcaneMissile, MagicImpact, Portal, Rune | [x] | [ ] Offen |
| Ione Vaska | Invert Field | AOE am Zielpunkt | Violett | MagicImpact, Nova, Portal, Rune | [x] | [ ] Offen |
| Ione Vaska | Phase Fold | Sprung | Violett | Rune, Smoke | [x] | [ ] Offen |
| Ione Vaska | Impossible Center | AOE am Zielpunkt | Violett | MagicImpact, Nova, Portal, Rune | [x] | [ ] Offen |
| Tahla Venn | Basisangriff | Fernkampf / Geschoss | Gold | NatureImpact, NatureMissile | [x] | [ ] Offen |
| Tahla Venn | Resonant Seed | Fernkampf / Geschoss | Gold | Impact, NatureImpact, NatureMissile, Nova | [x] | [ ] Offen |
| Tahla Venn | Growth Veil | Heilung | Grün | Heal, NatureMissile | [x] | [ ] Offen |
| Tahla Venn | Venn Shelter | Schild | Grün / Weiß | NatureMissile, Rune | [x] | [ ] Offen |
| Tahla Venn | Wildsurge | AOE um den Helden | Gold (Angriff), Grün (Heilung) | Heal, NatureNova | [x] | [ ] Offen |
| Brakk Orun | Basisangriff | Nahkampf | Orange | FireImpact, Punch | [x] | [ ] Offen |
| Brakk Orun | Wreck Swing | Nahkampf | Orange | FireImpact, FireNova, Punch | [x] | [ ] Offen |
| Brakk Orun | Armor Grip | Selbstschutz | Orange | Heal, Rune | [x] | [ ] Offen |
| Brakk Orun | Wreckstorm | AOE um den Helden | Orange | FireImpact, Spark | [x] | [ ] Offen |
| Brakk Orun | Orun Overdrive | AOE um den Helden | Orange | FireImpact, FireNova, Spark | [x] | [ ] Offen |
| THE TRAVELER | Basisangriff | Fernkampf / Geschoss | Violett | Impact, Orb | [x] | [ ] Offen |
| THE TRAVELER | Temporal Beam | Fernkampf / Geschoss | Gold | Impact, Orb, Rune | [x] | [ ] Offen |
| THE TRAVELER | Rift Step | Sprung | Gold | Rune, Smoke | [x] | [ ] Offen |
| THE TRAVELER | Chrono Snare | AOE am Zielpunkt | Violett | Impact, Nova, Orb, Portal, Rune | [x] | [ ] Offen |
| THE TRAVELER | Paradox Collapse | AOE am Zielpunkt | Weiß | Impact, Nova, Orb, Portal, Rune | [x] | [ ] Offen |
| Nerok Abyss | Basisangriff | Nahkampf | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Nerok Abyss | Anchor Recall | Fernkampf / Geschoss | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Nerok Abyss | Pressure Hull | Selbstschutz | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Nerok Abyss | Breaker Wave | AOE um den Helden | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Nerok Abyss | Abyssal Maelstrom | AOE am Zielpunkt | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Andromeda Timmy | Basisangriff | Nahkampf | Cyan | Claw, SwordImpact | [x] | [ ] Offen |
| Andromeda Timmy | Claw Swipe | Nahkampf | Cyan | Claw, Impact, SwordImpact | [x] | [ ] Offen |
| Andromeda Timmy | Stardust | AOE am Zielpunkt | Cyan | Impact, Smoke, SwordImpact | [x] | [ ] Offen |
| Andromeda Timmy | Jet Pounce | Sprung | Cyan | Impact, Jet | [x] | [ ] Offen |
| Andromeda Timmy | Scratch | Sprung | Cyan | Claw, Impact, Jet, SwordImpact | [x] | [ ] Offen |
| Void Knight | Basisangriff | Nahkampf | Violett | HeavySlash, SwordImpact | [x] | [ ] Offen |
| Void Knight | Rift Sweep | Nahkampf | Violett | HeavySlash, Impact, SwordImpact | [x] | [ ] Offen |
| Void Knight | Null Guard | Selbstschutz | Violett | Rune | [x] | [ ] Offen |
| Void Knight | Rift Lunge | Sprung | Violett | Impact, Smoke, SwordImpact | [x] | [ ] Offen |
| Void Knight | Eventide Cleave | Kegel-AOE | Violett | HeavySlash, Impact, SwordImpact | [x] | [ ] Offen |

## Nächste native Prüfung

Nach Pull und Unity-Kompilierung wird die Bibliothek auf Version 2 regeneriert. `DIMENSIONS FRONT > VFX > Test Epic Toon emission` instanziiert jede gekaufte Effektfamilie in einer isolierten Editor-Vorschauszene, startet ihre Systeme und prüft erzeugte Partikel sowie Shader-Unterstützung. Das verändert keine Spielszene. Der Test prüft keine Pixel, keinen Farbeindruck und keine Android-Leistung; danach bleiben die 45 Sichtprüfungen im Spiel erforderlich.

## Sound-Abnahme zurückgestellt

Auf Nutzerwunsch werden zuerst die Visuals abgeschlossen. Die bisherigen Angriffssounds und ihre Auslösung werden in diesem VFX-Update nicht geändert. Der getrennte Sound-Timing-Audit bleibt zurückgestellt.
