# Einzelcheckliste der Heldenaktionen

40 Aktionen von acht Helden erhalten individuelle Epic-Toon-Konzepte. Nerok bleibt unverändert. Timmy behält ursprüngliche Klauen und Staub; seine beiden Sprungaktionen verwenden auf den neuesten Wunsch je einen gekauften Flamethrower pro Düse. Die bevorzugten alten Schildkörper bleiben erhalten.

`Code geprüft` bedeutet: Der C#-Harness hat die tatsächliche Basisangriffs-/Fähigkeitsausführung durchlaufen und beobachtet, dass zugehörige neue Effekte erzeugt werden, der alte kosmetische Renderer dabei ausgeschaltet bleibt und die Partikel explizit gestartet werden. Dies ist kein Nachweis für native Darstellung, Farbeindruck, Größe oder Bildrate in Unity. Die eigene Spalte für diesen Sichttest bleibt offen.

Die Funktion kommt aus dem echten Aiming-Katalog. `Dash` wird hier entsprechend dem Nutzer als Sprung geführt. Kein Bewegungscode wird geändert. Effektfamilien sind die während einer vollständigen Aktion beobachteten Familien; unterstützende Selbstheilung und Treffer-Feedback können zusätzlich vorkommen.

| Held | Aktion | Funktion | Farbzuordnung | Beobachtete Effektfamilien | Code | Unity-Sichttest |
|---|---|---|---|---|---|---|
| Zhaev Null | Basisangriff | Nahkampf | Violett | Veyr_Basic_NullCutEdge, Veyr_Basic_NullCutEdge_Hit | [x] | [ ] Offen |
| Zhaev Null | Null Cut | Nahkampf | Violett | Veyr_Q_SplitRiftCut, Veyr_Q_SplitRiftCut_Cast, Veyr_Q_SplitRiftCut_Hit | [x] | [ ] Offen |
| Zhaev Null | Rift Scar | Fernkampf / Schattenriss | Violett | Nova, Veyr_W_RiftScarCrescent, Veyr_W_RiftScarCrescent_Cast, Veyr_W_RiftScarCrescent_Hit | [x] | [ ] Offen |
| Zhaev Null | Gapfold | Sprung | Violett | Veyr_E_GapfoldTear, Veyr_E_GapfoldTear_Cast | [x] | [ ] Offen |
| Zhaev Null | Vacant Echo | Nahkampf | Violett | Veyr_R_VacantEchoScythe, Veyr_R_VacantEchoScythe_Cast, Veyr_R_VacantEchoScythe_Hit | [x] | [ ] Offen |
| Auren Kest | Basisangriff | Nahkampf | Gold | Kaelon_Basic_KestGauntlet, Kaelon_Basic_KestGauntlet_Hit | [x] | [ ] Offen |
| Auren Kest | Anchor Strike | Fernkampf / Faustwelle | Violett | Kaelon_Q_AnchorShockFront, Kaelon_Q_AnchorShockFront_Cast, Kaelon_Q_AnchorShockFront_Hit, Rune | [x] | [ ] Offen |
| Auren Kest | Lumen Wall | Selbstschutz | Gold | Kaelon_W_LumenRampart_Cast | [x] | [ ] Offen |
| Auren Kest | Gravity Anchor | AOE am Zielpunkt | Blau | Kaelon_E_GravityEight, Kaelon_E_GravityEight_Cast, Kaelon_E_GravityEight_Hit, Rune | [x] | [ ] Offen |
| Auren Kest | Kest Citadel | AOE um den Helden | Gelb | Heal, Kaelon_R_CitadelSeal, Kaelon_R_CitadelSeal_Cast, Kaelon_R_CitadelSeal_Hit, Rune | [x] | [ ] Offen |
| Sivra Quell | Basisangriff | Fernkampf / Geschoss | Cyan | Nyra_Basic_QuellNeedle, Nyra_Basic_QuellNeedle_Hit | [x] | [ ] Offen |
| Sivra Quell | Prism Shot | Strahl | Blau | ExactBeam, Impact, Nyra_Q_PrismLine_Cast, Nyra_Q_PrismLine_Hit | [x] | [ ] Offen |
| Sivra Quell | Vector Shift | Sprung | Cyan | Nyra_W_VectorAfterimage, Nyra_W_VectorAfterimage_Cast | [x] | [ ] Offen |
| Sivra Quell | Light Snare | Fernkampf / Geschoss | Weiß | Nyra_E_SnareFork, Nyra_E_SnareFork_Cast, Nyra_E_SnareFork_Hit | [x] | [ ] Offen |
| Sivra Quell | Sightline Break | Fernkampf / Salve | Rot | Nyra_R_SightlineVolley, Nyra_R_SightlineVolley_Cast, Nyra_R_SightlineVolley_Hit | [x] | [ ] Offen |
| Ione Vaska | Basisangriff | Fernkampf / Geschoss | Violett | Elyra_Basic_VaskaArcaneLance, Elyra_Basic_VaskaArcaneLance_Hit | [x] | [ ] Offen |
| Ione Vaska | Orbit Fracture | AOE am Zielpunkt | Violett | Elyra_Q_OrbitFracturePair, Elyra_Q_OrbitFracturePair_Cast, Elyra_Q_OrbitFracturePair_Hit, MagicImpact | [x] | [ ] Offen |
| Ione Vaska | Invert Field | AOE am Zielpunkt | Violett | Elyra_W_InvertWell, Elyra_W_InvertWell_Cast, Elyra_W_InvertWell_Hit, Nova | [x] | [ ] Offen |
| Ione Vaska | Phase Fold | Sprung | Violett | Elyra_E_PhaseSplit, Elyra_E_PhaseSplit_Cast | [x] | [ ] Offen |
| Ione Vaska | Impossible Center | AOE am Zielpunkt | Violett | Elyra_R_ImpossibleCollapse, Elyra_R_ImpossibleCollapse_Cast, Elyra_R_ImpossibleCollapse_Hit, Nova | [x] | [ ] Offen |
| Tahla Venn | Basisangriff | Fernkampf / Geschoss | Gold | Mira_Basic_VennLeafDart, Mira_Basic_VennLeafDart_Hit | [x] | [ ] Offen |
| Tahla Venn | Resonant Seed | Fernkampf / Geschoss | Gold | Mira_Q_ResonantThorns, Mira_Q_ResonantThorns_Cast, Mira_Q_ResonantThorns_Hit, Nova | [x] | [ ] Offen |
| Tahla Venn | Growth Veil | Heilung | Grün | Mira_Basic_VennLeafDart, Mira_W_GrowthBloom, Mira_W_GrowthBloom_Cast | [x] | [ ] Offen |
| Tahla Venn | Venn Shelter | Schild | Grün / Weiß | Mira_Basic_VennLeafDart, Mira_E_ShelterPetals_Cast | [x] | [ ] Offen |
| Tahla Venn | Wildsurge | AOE um den Helden | Gold (Angriff), Grün (Heilung) | Mira_R_WildsurgeBloom, Mira_R_WildsurgeBloom_Cast, Mira_W_GrowthBloom | [x] | [ ] Offen |
| Brakk Orun | Basisangriff | Nahkampf | Orange | Rook_Basic_OrunHammer, Rook_Basic_OrunHammer_Hit | [x] | [ ] Offen |
| Brakk Orun | Wreck Swing | Nahkampf | Orange | FireNova, Rook_Q_WreckArc, Rook_Q_WreckArc_Cast, Rook_Q_WreckArc_Hit | [x] | [ ] Offen |
| Brakk Orun | Armor Grip | Selbstschutz | Orange | Heal, Rook_W_ArmorRivet_Cast | [x] | [ ] Offen |
| Brakk Orun | Wreckstorm | AOE um den Helden | Orange | Rook_E_WreckstormSpin, Rook_E_WreckstormSpin_Cast, Rook_E_WreckstormSpin_Hit | [x] | [ ] Offen |
| Brakk Orun | Orun Overdrive | AOE um den Helden | Orange | Rook_R_OverdriveEruption, Rook_R_OverdriveEruption_Cast, Rook_R_OverdriveEruption_Hit | [x] | [ ] Offen |
| THE TRAVELER | Basisangriff | Fernkampf / Geschoss | Violett | Traveler_Basic_ChronoDart, Traveler_Basic_ChronoDart_Hit | [x] | [ ] Offen |
| THE TRAVELER | Temporal Beam | Fernkampf / Geschoss | Gold | Traveler_Q_TemporalCrescent, Traveler_Q_TemporalCrescent_Cast, Traveler_Q_TemporalCrescent_Hit | [x] | [ ] Offen |
| THE TRAVELER | Rift Step | Sprung | Gold | Traveler_W_RiftStepClock, Traveler_W_RiftStepClock_Cast | [x] | [ ] Offen |
| THE TRAVELER | Chrono Snare | AOE am Zielpunkt | Violett | Nova, Orb, Traveler_E_ChronoFigureEight, Traveler_E_ChronoFigureEight_Cast, Traveler_E_ChronoFigureEight_Hit | [x] | [ ] Offen |
| THE TRAVELER | Paradox Collapse | AOE am Zielpunkt | Weiß | Nova, Orb, Traveler_R_ParadoxCollapse, Traveler_R_ParadoxCollapse_Cast, Traveler_R_ParadoxCollapse_Hit | [x] | [ ] Offen |
| Nerok Abyss | Basisangriff | Nahkampf | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Nerok Abyss | Anchor Recall | Fernkampf / Geschoss | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Nerok Abyss | Pressure Hull | Selbstschutz | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Nerok Abyss | Breaker Wave | AOE um den Helden | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Nerok Abyss | Abyssal Maelstrom | AOE am Zielpunkt | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Andromeda Timmy | Basisangriff | Nahkampf | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Andromeda Timmy | Claw Swipe | Nahkampf | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Andromeda Timmy | Stardust | AOE am Zielpunkt | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Andromeda Timmy | Jet Pounce | Sprung | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Andromeda Timmy | Scratch | Sprung | Bestand | Original unveraendert | Ausgenommen – Original | Kein Austausch |
| Void Knight | Basisangriff | Nahkampf | Violett | VoidKnight_Basic_VoidEdge, VoidKnight_Basic_VoidEdge_Hit | [x] | [ ] Offen |
| Void Knight | Rift Sweep | Nahkampf | Violett | VoidKnight_Q_RiftSweepCross, VoidKnight_Q_RiftSweepCross_Cast, VoidKnight_Q_RiftSweepCross_Hit | [x] | [ ] Offen |
| Void Knight | Null Guard | Selbstschutz | Violett | VoidKnight_W_NullGuardSeal_Cast | [x] | [ ] Offen |
| Void Knight | Rift Lunge | Sprung | Violett | VoidKnight_E_LungeBladeWake, VoidKnight_E_LungeBladeWake_Cast, VoidKnight_E_LungeBladeWake_Hit | [x] | [ ] Offen |
| Void Knight | Eventide Cleave | Kegel-AOE | Violett | VoidKnight_R_EventideCleaveFan, VoidKnight_R_EventideCleaveFan_Cast, VoidKnight_R_EventideCleaveFan_Hit | [x] | [ ] Offen |

Die [40 Angriffskonzepte](Hero-VFX-Concepts-2026-10-24.md) beschreiben Form, Atmosphäre, Paketquellen und Steigerung der Ultimates. Neue Effekte sind um 200 Prozent vergrößert (Faktor 3); Hauptformen der Ultimates zusätzlich um Faktor 1,4. Warnradien und Strahlenden behalten ihre Gameplay-Maße.

## Ausfallschutz

Ein nicht unterstützter alter Shader blockiert den neuen Pool nicht mehr, sofern der Epic-Shader unterstützt wird. Erzeugt ein Partikel-Prefab noch nichts Darstellbares, bleibt ein einzelner Ersatz-Renderer aktiv; gleichzeitig sind die nativen Renderer aus. Mit einsetzender Emission übernimmt der neue Effekt ohne Neustart und ohne Überlagerung. Eine Warnung nennt die betroffene Familie. Die Codeprüfung enthält diese Fehlerfälle; die Darstellung in Unity bleibt gesondert zu prüfen.

## Nächste native Prüfung

Vor dem Pull Play Mode beenden. Nach der Kompilierung wird eine veraltete Bibliothek regeneriert. Danach läuft automatisch eine isolierte native Vorschauprüfung für alle 152 Quell-/Angriffs-/Phasenvorlagen: Partikelstart, Shader-Kompilierung und Bildpixel gegenüber einem leeren Frame. Unter `Library/DF_EpicVfxReview/` entstehen Report, Einzelbilder, Kontaktbogen und eine beschriftete HTML-Übersicht. Der Menüpunkt `DIMENSIONS FRONT > VFX > Render Epic Toon visibility review` wiederholt die Prüfung. Dies verändert keine Spielszene und spielt keine Sounds. Repräsentative Farbbilder ersetzen keine Abnahme der tatsächlichen Animation, Orientierung, Größenwirkung, Trefferzuordnung oder Android-Leistung. Die 40 Echtspiel-Sichtprüfungen bleiben deshalb offen, bis echte Unity-Ergebnisse vorliegen.

## Sound-Abnahme zurückgestellt

Auf Nutzerwunsch werden zuerst die Visuals abgeschlossen. Die bisherigen Angriffssounds und ihre Auslösung werden in diesem VFX-Update nicht geändert. Der getrennte Sound-Timing-Audit bleibt zurückgestellt.

## Zusätzliche Abnahme Version 6

- [x] Gemessene Bounds, echte Reichweiten/Kegel, kompakte Geschosse, getrennte Flugschweife und kleine Kontakte im Runtime-Harness geprüft.
- [x] Alle Ultimates sowie Gravity Anchor und Wreckstorm erzeugen ausdrücklich ihre Hauptkomposition; eine Cast-Variante allein genügt nicht.
- [x] Zwei Flamethrower an Timmys Düsen; drei unveränderte Scratch-Klauen.
- [x] Heil-Pod: Ruheeffekt bei Verfügbarkeit, genau ein Aufnahmeimpuls, kein Ruheeffekt während Respawn.
- [x] Wetterprofil für alle 14 Planeten, maximal zwei Layer/84 Partikel, Abschaltung bei knappem Dekorationsbudget.
- [x] Comic-Schrift-Layer entfernt, physische Riss-/Blitztexturen erhalten; sämtliche 51 Quellen existieren im gekauften Paket und verwenden keine 2D-Alternate-Pfade.
- [ ] Native Shader-/Materialkompilierung und 170 Prefab-Einträge nach dem neuen Import.
- [ ] Alle 15 gemeinsamen Ansichten in 640 × 360 auf Überlagerung und Erkennbarkeit ansehen.
- [ ] Alle 40 Aktionen in der echten Spielkamera bei mehreren gleichzeitig kämpfenden Helden und auf dem Handy beurteilen.
- [ ] Neue Standardangriff-Aufnahmen zusammen mit Hieb/Abschuss abhören; Android-Framezeit unter Last prüfen.
