# DIMENSIONS FRONT — abschließender Code-Feinschliff

Ausgangsstand auf `dev/vertical-slice`: `75387a8244a100621b4b6f36380812f331d3718b`.
Lokaler Arbeitsstand vor Beginn sauber; die bereits gesicherten Audio-, Hero-, Menü-, Commerce- und KI-Änderungen bleiben erhalten.

## Priorität und Ergebnis

Der Codecheck konzentrierte sich auf Eingabe/Bewegung, Fähigkeiten, Rückruf/Respawn, Match-Pause, Zielerfassung, Trefferfeedback, Kamera, Audiopool/Musik und Ressourcenfreigabe der Hero-/Menüvisualisierung. Er ist keine vollständige Laufzeit- oder visuelle Abnahme. Zwei konkrete Fehlergruppen wurden korrigiert:

1. **Aktuelle Touch-Richtung beim Fähigkeitsauslösen.** Das HUD läuft vor dem Bewegungs-Update. Fähigkeiten lasen deshalb die Richtung des vorherigen Frames; zusätzlich hing die Reihenfolge von Joystick und Fähigkeitsfreigabe von der Finger-Reihenfolge ab. Nun verarbeitet das HUD alle Bewegungsereignisse vor den Kampfereignissen. Bewegung, Dash-Vorschau und Fähigkeit nutzen dieselbe kamerabezogene Berechnung aus der aktuellen Eingabe. Analoge Stärke, Deadzone, diagonales Geschwindigkeitslimit und Kollisionsbewegung bleiben erhalten.
2. **Rückruf wird zuverlässig unterbrochen.** Treffer unterbrechen unmittelbar über `Health.Damaged`, auch wenn der Schild den Schaden vollständig absorbiert oder eine Heilung im selben Frame den Lebensbalken wieder auffüllt. Deaktivieren beendet Coroutine und kosmetische Anzeige ohne Abbruchton beim Matchabbau. Ein inaktiver Held kann keinen Rückruf starten. Ein gerade abgeschlossener Kanal teleportiert nicht während einer zwischenzeitlich gestarteten Match-Pause. Kanalzeit und Bewegungsgrenze bleiben unverändert.

Weitere Dekoration, Kameraänderungen oder Kampfwert-Anpassungen wären ohne echte Gameplay-Beobachtung nicht ausreichend begründet und wurden nicht ergänzt.

## AI DIFFICULTY

- **EASY / NORMAL / HARD**, Standard **NORMAL**, erreichbar über **SETTINGS > BOT DIFFICULTY**.
- Persistenz über `PlayerPrefs` (`DF_BotDifficulty`), sofort gespeichert; beim nächsten Match von beiden Teams übernommen. Ungültige/fehlende Werte ergeben NORMAL.
- Ein System kombiniert Hero-Rolle, Persönlichkeit, Schwierigkeit und aktuelle Matchsituation. Profile beeinflussen Reaktions-/Entscheidungszeiten, Zielbewertung, Rückzug, Gruppierung, Objective-Intervalle, Positionierung, taktisches Gedächtnis und Fehlerwahrscheinlichkeit; keine erhöhten Kampfwerte oder Cooldown-Vorteile.
- Details und Parametertabelle: `Docs/AI-Difficulty.md`. **Keine Schwierigkeitsstufe wurde hier im Spiel getestet.** Menschlichkeit, Fairness, Rollenverhalten und spürbarer Schwierigkeitsabstand sind noch durch EASY-/NORMAL-/HARD-Matches zu validieren.

## Prüfstand

Ausgeführt: C#-Syntaxprüfung aller **210** lokal vorhandenen Skripte ohne Parserfehler, `git diff --check`, gezielte Prüfung der geänderten Aufrufpfade. Kein Unity-/dotnet-/Android-Ausführungswerkzeug vorhanden. Syntaxprüfung ersetzt keine Unity-Kompilierung.

Unity-Regressionen vorbereitet, **nicht ausgeführt**:

- `DIMENSIONS FRONT > Checks > Movement collision and dash (Play mode)`: erweitert um kamerabezogene Eingabe, Richtungsumkehr ohne Update dazwischen, Analog-/Diagonalstärke, Loslassen und Abbruch.
- `DIMENSIONS FRONT > Checks > Recall cancellation (Play mode)`: Schaden plus Heilung im selben Frame, Schildtreffer, Deaktivieren und erneuter Start.

Noch erforderlich: echter Mehrfingertest (beide Finger-Reihenfolgen, Richtungswechsel während Dash-Freigabe, Drag-out, Touch-Cancel, Background/Resume), Rückruf an der Pause-/Abschlussgrenze, komplette Matches und wiederholte Menü-/Matchwechsel. GPU/CPU/GC, Hitfeedback, Soundmix und visuelle Qualität müssen in Unity und auf Android beurteilt werden. Bestehende Build-, Signing-, Store- und Backend-Blocker bleiben in `Docs/GooglePlayReleaseChecklist.md` dokumentiert. Echtgeldkäufe bleiben ohne externe Konfiguration gesperrt.
