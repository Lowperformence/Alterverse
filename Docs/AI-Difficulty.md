# DIMENSIONS FRONT — Bot Difficulty

## Bedienung und Speicherung

**Settings → BOT DIFFICULTY → EASY / NORMAL / HARD.** Standard: **NORMAL**. Die drei 54 Einheiten hohen Schaltflächen nutzen das vorhandene Menüdesign. Play zeigt die gewählte Stufe vor dem Start. Kein Expert-Modus: drei Stufen reichen für diesen Pass.

`BotDifficultySettings.Selected` speichert sofort unter `DF_BotDifficulty` in PlayerPrefs und ruft `Save()` auf. Fehlende/ungültige Werte ergeben NORMAL. Jede Bot-Brain übernimmt beim Erstellen ihre Stufe; eine Menüänderung gilt für das nächste Match, für **beide Bot-Teams**. Ein laufendes Match wird nicht nachträglich umgestellt. Matchende, Rückkehr und Respawn löschen die Einstellung nicht. Geräte-Neustart-Persistenz ist noch praktisch zu prüfen.

## Eine Architektur, drei Profile

`BotDifficultyProfile` enthält ausschließlich Verhaltensparameter. Die vorhandenen Komponenten Brain, Sensor, Mover und HeroBotAbilities verwenden diese gemeinsam.

| Parameter | EASY | NORMAL | HARD |
|---|---:|---:|---:|
| Wartezeit vor neuem Ziel, Sekunden | 0,60 | 0,35 | 0,22 |
| Faktor für Persönlichkeits-Entscheidungsintervall, Scans und Fähigkeitserwägung | 1,65 | 1,00 | 0,80 |
| Objective-Auswertung, Sekunden | 2,00 | 1,00 | 0,55 |
| Genauigkeitsgewicht der Ziel-/Gefahrenbewertung | 0,35 | 0,65 | 0,90 |
| Anpassung persönlicher HP-Rückzugsschwelle | −0,04 | 0 | +0,03 |
| Gruppierungsgewicht × persönlicher Teamfokus | 0,35 | 0,60 | 0,85 |
| Gefahrenerinnerung / starke Zielbindung, Sekunden | 1,5 | 3 | 5 |
| Offensive Fähigkeitsentscheidung auslassen | 28 % | 15 % | 7 % |
| Positionsgewicht | 0,30 | 0,60 | 0,90 |

Die Zielwartezeit beginnt bei der Beobachtung; Scan-/Entscheidungsintervalle kommen hinzu. Das sind keine garantierten gemessenen Reaktionszeiten. Persönliche Reaktionsintervalle haben weiterhin mindestens 0,15 Sekunden. Verpasste Fähigkeitsgelegenheiten verschieben nur die Entscheidung; echte Cooldowns bleiben identisch. Schutz-/Heilentscheidungen bleiben auch auf EASY aktiv.

Höhere Zielgenauigkeit gewichtet sichtbare verwundete Gegner stärker und verringert zufällige Bewertungsfehler. Rückzug berücksichtigt eigene Lebenspunkte, lokal beobachtete zahlenmäßige Unterlegenheit und kürzlich selbst erlittenen Schaden. Fernkampfrollen halten je nach Stufe mehr Abstand, setzen sich mit der bestehenden Bewegung zurück und suchen bei Unterzahl zeitweise Nähe zu beobachteten Verbündeten. Nahkämpfer bleiben an ihre Reichweite gebunden. HARD berücksichtigt zusätzlich grobe lokale Gegnergruppen für geeignete Ultimates; Brakks Selbst-AoE wartet auf Nahdistanz.

## Rolle + Persönlichkeit + Schwierigkeit + Situation

Alle fünf bestehenden Persönlichkeiten bleiben erhalten: Balanced, Aggressive, Defensive, Supportive, ObjectiveFocused. Es wurden keine drei getrennten Systeme oder sieben nur nominelle neue Persönlichkeitstypen angelegt.

- Hero-Rolle: bestehende sechs individuellen Skill-Entscheidungswege, Waffenreichweite, Support-Heilung, Tank-Schutz, Assassin-Dash und Fernkampfpositionierung.
- Persönlichkeit: Aggression beeinflusst Hero-Zielpriorität; Vorsicht bestimmt Basis-Rückzugsschwelle; Teamfokus beeinflusst Gruppierung; Objective-Fokus beeinflusst Rotationsbereitschaft; persönliche Reaktionszeit bleibt Basis des Entscheidungsintervalls.
- Schwierigkeit: bestimmt die Ausführung dieser Entscheidungen. Unabhängige Bot-Zufallsfolgen variieren Bewertungsfehler und Gruppierungsentscheidungen auch auf HARD, ohne Unitys globalen Gameplay-Zufall zu verändern.
- Situation: lokale Feind-/Verbündetenzahl, HP, erlittene Schäden, aktuelles Ziel, Distanz, Rolle, aktive öffentliche Planeten und vorhandene Lane-/Turmschutzlogik.

Gedächtnis speichert begrenzte Gefahr und Zielbindung, keine unsichtbaren Live-Gegnerpositionen. Ziele außerhalb derselben rollenabhängigen Sichtdistanz oder hinter soliden Hindernissen sind nicht angreifbar. Ein bereits bekanntes Ziel wird nach Sichtverlust verworfen; Neuaufnahme braucht erneut Wartezeit. Physische Einheiten zählen nicht als undurchsichtige Wände. Die Wahrnehmungsprüfung benutzt wiederverwendete Raycast-Puffer; bei vollem Puffer wird konservativ keine Sicht angenommen. Öffentliche Objective-/Strukturinformationen und Turm-/Telegraph-Sicherheitsregeln sind auf allen Stufen gleich.

Keine Änderungen an HP, Schaden, Bewegungsgeschwindigkeit, Angriffs-/Fähigkeitscooldowns, Minions, Front-Balance oder Belohnungen. Kein Aim-Assist-Bonus und keine Teleportbewegung. Dies ist eine kompakte Erweiterung der vorhandenen Heuristiken, keine Behauptung einer vollständig menschenähnlichen oder bereits ausbalancierten KI.

## Prüfung

Hier ausgeführt: Syntaxprüfung aller 210 lokalen C#-Dateien ohne Fehler, Whitespaceprüfung und Vergleich der unveränderten Kampf-/Bewegungs-/Fähigkeitswerte gegen den vorherigen Commit. **Keine Unity-Kompilierung, keine Play-Mode-Matches, keine Geräte-/Performanceprüfung.** Keine Stufe wurde hier im Spiel getestet.

Bestehender Editorcheck `DIMENSIONS FRONT > Checks > Bot and minion regression (Play mode)` erweitert:
- fehlende/ungültige Einstellung → NORMAL, gespeicherter Wert wird zurückgelesen;
- alle drei Stufen × fünf Persönlichkeiten: Stufe erfasst, Persönlichkeit erhalten, Kampfwerte unverändert;
- verzögerte Zielaufnahme, Abbruch bei Sichtwand bzw. Reichweitenverlust;
- Zielwechsel-/Todchecks berücksichtigen Reaktionszeit statt Sofort-Neuziel;
- vorheriger Einstellungswert wird im `finally` wiederhergestellt.

Diese Unity-Checks wurden vorbereitet, **nicht ausgeführt**. Danach EASY-, NORMAL- und HARD-Match mit allen Rollen durchspielen, Objectives/Grouping/Rückzug/Respawn und zwei aufeinanderfolgende Matches prüfen. Einstellung ändern, App beenden und neu starten; auf kleinem Smartphone Touchfläche/Lesbarkeit prüfen. Fairness, Schwierigkeitsabstand, Sichtprüfungskosten und Kiting müssen durch echte Spielsitzungen/Profiler bestätigt werden; erst danach Feinabstimmung der zentralen Profile.
