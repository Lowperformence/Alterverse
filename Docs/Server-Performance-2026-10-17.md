# Server-Performance: Transport und laufender Matchbetrieb

## Änderungen

- Die Matchkennung wird direkt mit FNV-1a über dieselben UTF-16-Zeichen berechnet. Der bisher pro Aufruf erzeugte StringBuilder und Ergebnistext entfallen. Es gibt keinen Cache für veränderliche Matchpläne; Änderungen am Roster und Rundenwechsel werden weiterhin sofort erkannt.
- Owner-Pose und Hero-Snapshot verwenden die bereits durch `EnsurePoseRound` geprüfte Kennung erneut. Ein Welt-Snapshot berechnet sie einmal für alle Daten- und Löschpakete.
- Der Welt-Snapshot verwendet seine Objektliste erneut. Der zusätzliche periodische Welt-Scan auf dem Server entfällt, weil unmittelbar vor jedem Versand bereits ein vollständiger Scan erfolgt. Client-Suche und Versandintervall bleiben erhalten.
- Während eines eingerichteten Matches entfällt die ungenutzte Suche nach `PrototypeBootstrap` in jedem Serverframe. Vor Installation des Matchplans bleibt die Unterdrückung aktiv; die Menübereinigung läuft weiterhin.

## Messung und Verhalten

Kontrollierter .NET-8-Test, kein Unity-/Android- oder nativer Edgegap-Lasttest:

| Prüfung | Bisher | Optimiert |
|---|---:|---:|
| Temporärer verwalteter Speicher für 20.000 Kennungsberechnungen, normales 8er-Roster | 34.240.000 Byte | 0 Byte |
| Laufzeit derselben Messung in dieser Umgebung | 69,15 ms | 38,19 ms |
| Kennungsberechnungen für 110 Weltobjekte in 19 Paketen | 19 | 1 |
| Bootstrap-Szenensuchen über 60 Frames mit aktivem Matchplan | 60 | 0 |

Die Zeitmessung hängt von Laufzeit, Compiler und Last ab. Sie beschreibt nur diese Funktion, keine prozentuale Verbesserung des gesamten Servers.

- 9.937 Lobby-/Identitäts-/Ranked-Prüfungen, darunter 8.000 Vergleiche mit der bisherigen Hashberechnung über veränderte Pläne, Unicode, leere/null Werte, Ganzzahlgrenzen und mehrere Zahlformate.
- 207 Transport-/Bewegungs-/Scoreboard-/Heilungsprüfungen einschließlich Paketgrenzen, zuverlässiger Löschmeldungen, frischer HP-/Schildwerte und Rundenkennung.
- 41 Dedicated-Server-Prüfungen einschließlich Bootstrap-Sperre vor dem Match und fortgesetzter Menübereinigung.
- Gegenproben mit alter Kennungsberechnung, altem Weltversand und alter Bootstrap-Suche erkennen jeweils den vorherigen Mehraufwand.

Spielmodus bleibt 4v4. Tickziel, Netzwerk-Sendeintervalle, Trefferberechnung, Fähigkeiten und Ressourcenbuchung werden nicht geändert.

## Live-Stand und nächste Messung

Der jüngste sichtbare v0.1.7-Server `f104cd585a59` in Mailand war bei der Prüfung beendet. Er hatte 0,50 vCPU und 1 GiB RAM. Seine Containerlogs waren abgelaufen; historische Metriken lieferten einen Abruffehler. Eine aktuelle CPU-Sättigung ist damit nicht belegt.

Diese Quellcodeänderungen erfordern einen neuen Dedicated-Server-Build. Wegen der ebenfalls hinzugekommenen Arena-, Charakter- und Darstellungsänderungen Android und Server vom selben gemeinsamen Stand bauen. Vor Aktivierung den nativen Unity-Start auf UDP 7777 prüfen. Anschließend Server-FPS, p95-/Max-Frametimes, CPU, RTT/Paketjitter und Geräte-Frametimes in einem realen 4v4-Match messen. Native Lastreserve für zehn Teilnehmer ist noch nicht nachgewiesen.
