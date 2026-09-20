# DIMENSIONS FRONT — KI Second Pass und Visual Clarity

Basis: `5825bb53ae18068efb4a429667c7d599c12eb9b5`, Branch `dev/vertical-slice`.
Gezielte Änderungen auf dem aktuellen Stand; keine Dateien aus älteren Versionen zurückgesetzt.

## KI-Dateien und Verhalten

- `BotTargetSensor.cs`: konstante Wahrnehmungsgewichtung statt zufälliger Neubewertung bei jedem Scan; zeitversetzter erster Scan; Positions-Snapshot für Bewegung; Sichtprüfungen im Scan und vor Aktionen statt bei jedem Property-Zugriff. Gebäude mit Health blockieren Sicht, Helden/Minions nicht. Zeitlich begrenzte Sperre nach abgebrochener Verfolgung, mit Nahbereichsausnahme zur Selbstverteidigung.
- `MobaBotMover.cs`: Fernkampf-Abstand mit getrennten Ein-/Austrittsschwellen; Turmumgehung hält ihre Seite 1,4 Sekunden; Recovery hält die Seite über mehrere Versuche und wechselt erst nach wiederholtem Scheitern. Bestehende Fortschrittsmessung, freie Richtungsprüfung, Route Reset und Respawn Reset bleiben. Kein Teleport ergänzt.
- `MobaBotBrain.cs`: Mindestdauer für Rückzug und kein erneuter Einstieg bei weiter bestehender sichtbarer Unterzahl.
- `MobaBotCombat.cs`: Bewegung/Facing nutzt beobachtete Position; vor einem tatsächlich fälligen Angriff werden Sicht und echte aktuelle Reichweite geprüft. Fehlgeschlagene Sichtprüfung wird höchstens nach 0,12 Sekunden wiederholt. Schaden, Angriffstakt und bestehende Attack Delivery bleiben unverändert.
- `HeroBotAbilities.cs`: selbstzentrierte offensive Flächenfähigkeiten werden ohne Ziel innerhalb des Radius zurückgehalten. Support-Heilung berücksichtigt Verletzungsschwere; Schutzfähigkeiten werden bei erkannter Feindnähe eingesetzt. Vor offensiven Entscheidungen wird Sicht geprüft. Cooldowns werden weiterhin ausschließlich vom bestehenden Ausführungssystem verwaltet.
- `BotLanePlan.cs`: ein fehlendes Objective wird ebenfalls bis zum nächsten Scan gecacht; dadurch keine wiederholte Szenensuche in jedem Frame bei leerem Ergebnis.

## Schwierigkeitsgrade

Die bestehenden Profile wurden bewusst nicht numerisch umgeschrieben. Der zweite Pass stabilisiert ihr Verhalten.

| Anzeige | Basis-Reaktionszeit | Bestehende Ausrichtung |
|---|---:|---|
| EASY | 0,75 s | langsam, mehr verpasste Gelegenheiten, schwächere Positionierung und Zusammenarbeit |
| MEDIUM | 0,38 s | ausgewogene Reaktion, Fehler und brauchbares Teamplay |
| HARD | 0,20 s | starke Bedrohungsbewertung, Positionierung und Ability-Disziplin |
| EXPERT | 0,11 s | höchste Entscheidungsqualität, Ziel- und Objective-Gewichtung |

Persönlicher Rhythmus und Scanintervalle kommen zur Basis-Reaktionszeit hinzu. Kein Frame-genaues Reagieren versprochen. `Normal` bleibt der interne Enum; `MEDIUM` bleibt die Anzeige. `DF_BotDifficulty` bleibt unverändert. Kein HP-, Damage-, Speed-, Range- oder Cooldown-Bonus.

Die sechs vorhandenen Rollenpräferenzen bleiben bestehen: verwundete Ziele für Zhaev, Frontnähe für Auren/Brakk, Reichweite für Sivra/Ione sowie Unterstützung für Tahla. Dieses Update verbessert insbesondere Fernkampf-Abstand, Support-Timing und leere Bruiser-/Tank-AoEs; es erfindet keine neue Rollenarchitektur.

## Visual-Dateien

- `Mobile_RPAsset.asset`: 2× MSAA statt deaktiviert; Render Scale bleibt 0,8. Android verwendet laut QualitySettings dieses Forward-Profil. PC-Deferred-Profil unverändert.
- `MenuHeroShowcase.cs`: mobiles Heldenporträt erlaubt 2× MSAA, einschließlich RenderTexture. Desktop-Porträt unverändert.
- `LunarSurfaceField.cs`: weniger hochfrequente Farbvariation, schwächere Kraterrand-/Auswurf-Kontraste und geringere Normalmap-Verstärkung; vorhandene Mipmaps/Trilinear-Filterung plus Anisotropie 2. Relief, Kraterpositionen und Collider unverändert.
- `LunarLighting.cs`: etwas weniger hartes Hauptlicht, dezent aufgehelltes Umgebungslicht und begrenzte Schattenstärke. Ursprünglicher Licht-/Schattenzustand wird beim Deaktivieren wiederhergestellt. Keine zusätzlichen Lichter, Schattenkaskaden oder Post-Processing-Effekte.
- `HeroSurface.shader`: breiterer, schwächerer Glanzpunkt und zurückgenommene Energiehelligkeit; Teamfarben/Silhouetten unverändert.
- `HeroVfxPool.cs`: geringere HDR-Spitzen in den bereits weich auslaufenden Effektformen. Warnring-Geometrie, Warnzeiten und reservierte Telegraph-Slots bleiben unverändert.
- `DimensionUiMotif.cs`: langsamere, schwächere dekorative Linienbewegung und ruhigere Eckakzente; Sci-Fi-Geometrie bleibt erhalten.
- `FrontEndTheme.cs`: ruhigere Panel-Kanten und begrenzte, an die Breite angepasste Einzeiler für kompakte Texte.
- `MainMenuController.cs`: getrennte responsive Spalten für Battle-Pass-Header; begrenzte Reward-Texte mit getrennten Textzeilen. Reward Preview, Icons, Beta Crown und kompakter Header bleiben; keine Milestone-Sparte hinzugefügt.

## Unveränderte Spielwerte

Keine Änderungen an Hero-Balance, Schaden, HP, Attack Speed, Movement Speed, Ability Cooldowns, Battle-Pass-Rewards, Shard Economy oder Account-/Match-/Battle-Pass-XP. KI-Entscheidungen ändern sich wie beauftragt; die zugrunde liegenden Kampfwerte bleiben gleich.

## Prüfung und Grenzen

- 23 gezielt geladene C#-Dateien mit C#-Syntaxparser geprüft: keine Syntaxfehler und keine doppelten Member/Methodensignaturen gefunden.
- `git diff --check`, Merge-Marker-Suche und YAML-Prüfung der geladenen URP-Assets bestanden.
- Geänderte Battle-Pass-Rechtecke bei 1136, 1216, 1296 und 1360 logischen Inhaltspixeln geprüft: positive Größen und getrennte geänderte Textbereiche.
- Neue Methoden/Properties gegen ihre Aufrufer und vorhandene AbilitySpec-/Execution-Schnittstellen geprüft; keine Änderungen an Difficulty-Enum/Save-Key oder Reward-/Balance-Katalogen.
- KI-Hotpaths gezielt auf zusätzliche LINQ-/Collection-Allokationen, Szenensuchen und Physikabfragen geprüft. Keine neuen Update-Allokationen hinzugefügt; Szenensuchen bei fehlendem Lane-Ziel begrenzt.
- `BotGameplayRegression.cs` um Verfolgungsabbruch, eingefrorene Positionsbeobachtung, konstante Zielbewertung und leere AoEs ergänzt. Bestehende Tests berücksichtigen nun persönlichen Reaktionsrhythmus und Telegraph-Reaktionszeit.

**Unity wurde nicht gestartet.** Kein Unity-Compile, Shader-Compile, Playmode-Test, Screenshotvergleich oder Android-Profiling durchgeführt. Die ergänzten Unity-Regressionen sind vorbereitet, nicht ausgeführt. Syntaxprüfung ersetzt keinen Unity-Compile.

Offene Validierung: In Unity Playmode `DIMENSIONS FRONT/Checks/Bot and minion regression (Play mode)` ausführen; Tower-/Crowd-Recovery und alle sechs Rollen in Matches prüfen; Menü/HUD und Texte auf kleinen Android-Displays visuell kontrollieren; 2× MSAA auf Zielgeräten profilieren. 60 FPS und vollständige Überlappungsfreiheit aller Bildschirme sind noch nicht nachgewiesen. MSAA erhöht GPU-/Speicherbandbreitenbedarf; geänderte Farb-/Lichtwerte benötigen einen visuellen Vergleich. Desktop-Kanten erhalten in diesem Pass keine neue AA-Methode.
