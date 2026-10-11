# Angriff: Zeit bis zum Treffer

Stand: 11. Oktober 2026. Untersuchte Spielquellen: Commit `c41d6711304519e9441d0070e2338aaabd26d86b`, Branch `dev/vertical-slice`.


**Update: Korrektur umgesetzt.** Gerichtete Skills erfassen jetzt sofort einen Gegner im ganzen sichtbaren Kegel (Nahkampf 70°, Fernkampf 35°), ohne zusätzlichen Zielstrich. Der Zielpunkt wird beim Release eingefroren und beim Abschuss vom aktuellen Ursprung einmal angepeilt. Die beiden unten dokumentierten Ursachen wurden als Regressionen behoben: ein Gegner bei 16° innerhalb des neuen 35°-Kegels verbindet nach schneller oder langer Geste; Mira Q verbindet auch nach dem vorgegebenen Seitwärtsweg. Gegner können weiterhin ausweichen, Wände blockieren, AOE trifft mehrere Gegner. V5 überträgt den festen Punkt und `HasAimPoint`. Stand nach weiterer Lauf-Prüfung: **1771 Assertions bestanden**. Die nachstehenden Tabellen und ursprünglichen Miss-Befunde beschreiben die erste Untersuchung vor dieser Korrektur.

**Weitere Lauf-Prüfung:** Automatische Fernkampf-Grundangriffe aktualisieren ihr ausgewähltes Ziel jetzt einmal beim Abschuss und halten begrenzt vor; manuelle Skills bleiben beim Release-Punkt. Auch Nahkampf-Skills richten ihren erfassten Punkt vom aktuellen Abschussursprung aus. Reproduktion, Sättigungsfehler und V5-Prüfung stehen in [MovingAttackConnectionAudit.md](MovingAttackConnectionAudit.md).

## Ergebnis der ersten Prüfung

Die Zielerfassung enthält keine Haltezeit oder Wartephase auf den Soft-Target-Marker. `PrototypeHUD` verarbeitet auch die letzte Fingerposition im `Ended`-Ereignis. `ManualAiming.Release()` prüft die Zielgeometrie synchron nochmals und stellt den Angriff sofort in die Ausführung. Der Marker ist keine Voraussetzung für den Treffer.

Im Fixture-Test lösten Gesten mit 0, 50, 150, 750 und 1000 ms Haltedauer denselben Angriff aus. Nyra E gegen einen 4 m entfernten Gegner, der erst beim Loslassen in das 8°-Assist-Fenster tritt, traf bei 60 FPS jeweils nach **233 ms ab Loslassen**. Es gab weder ein zwischenzeitliches `Moved`-Ereignis noch ein `ManualAiming.LateUpdate()` vor dem Loslassen. Bei 30 FPS betrug die Zeit ebenfalls 233 ms, bei 120 FPS 217 ms.

Das ist ein Test der unveränderten produktiven Kampf-, Ziel- und HUD-Logik gegen deterministische Engine-Fixtures. Er misst Spielzeit bis zum ersten Schadens-/Supporteffekt, keine CPU-Ausführungszeit, Android-Touchlatenz, Renderlatenz oder echte Netzwerkverzögerung. Unity Editor und Android-Gerät stehen in dieser Umgebung nicht zur Verfügung. Movement, Health, Coroutines, Physikabfragen und Netzwerktransport werden durch Fixtures ersetzt.

## Werte bei 60 FPS

Stationärer Gegner bei **2 m**, freie Sicht, kein Stun, kein Cooldown. Standardangriffe zählen ab **Drücken**, Skills ab **Loslassen**. Angaben sind Millisekunden bis zum ersten tatsächlich aufgelösten Effekt, auf ganze Millisekunden gerundet.

| Held / ID | Standard | Q | W | E | Ult |
| --- | ---: | ---: | ---: | ---: | ---: |
| Zhaev / Veyr | 167 | 133 | 217 | sofort: Dash-Start | 333 |
| Auren / Kaelon | 233 | 300 | sofort: Schild | 450 | 550 |
| Sivra / Nyra | 133 | 133 | sofort: Dash-Start | 183 | 283: erster Schuss |
| Ione / Elyra | 217 | 383 | 467 | sofort: Dash-Start | 683 |
| Tahla / Mira | 200 | 233 | sofort: Heilung | sofort: Schild | 400: Support |
| Brakk / Rook | 183 | 183 | sofort: Heilung/Schild | 300 | 500 |
| THE TRAVELER | 150 | 200 | sofort: Dash-Start | 383 | 683 |
| Nerok | 200 | 383 | sofort: Schild | 300 | 1150: erster Schadenstick |

Bei Dashes meint „sofort“ den akzeptierten Start des Dashes, nicht dessen vollständige Ankunft. Die Bewegungs-Fixture ersetzt die native Dash-Bewegung. Nyra Ult feuert drei getrennte Geschosse mit 180 ms Abstand; die Tabelle zeigt nur den ersten Treffer. Neroks Wirbel aktiviert sich nach 650 ms, sein erster Schadenstick folgt weitere 500 ms später. Danach bleiben die bestehenden sechs Halbsekunden-Ticks erhalten.

| Fernkampf-Standard | bei 2 m | bei 4 m | bei 95 % Reichweite |
| --- | ---: | ---: | ---: |
| Nyra | 133 ms | 183 ms | 250 ms bei 6,46 m |
| Elyra | 217 ms | 300 ms | 383 ms bei 5,795 m |
| Mira | 200 ms | 267 ms | 333 ms bei 5,70 m |
| Traveler | 150 ms | 217 ms | 283 ms bei 5,89 m |

Nerok Q benötigt bei 5,89 m etwa **600 ms**. Das erklärt merklich spätere Verbindungen bei weit entfernten Gegnern auch ohne verzögerte Zielerfassung.

## Woher die Zeit stammt

1. **Input:** Die Geste wird im HUD-Update verarbeitet. Zwischen echter Hardware-Eingabe und diesem Update kann eine Frame-/Touch-Verzögerung liegen; diese wurde nicht am Gerät gemessen.
2. **Zielerfassung:** `Begin`, `Drag` und `Release` prüfen aktuelle Gegnerpositionen synchron über `Health.Active`/`AcquireAssisted`. Es gibt kein Einrasten über mehrere Frames und keine Abhängigkeit vom Zeichnen der Markierung. Die 0,5-s-Wiederholung in `TargetIndicatorHUD.ResolveTargeting()` betrifft ausschließlich das Finden des Player-/Targeting-Components, nicht das Erfassen jedes Gegners.
3. **Ausholzeit:** Standardangriffe verwenden 80–220 ms, gerichtete Skills ihre bestehende Ausholzeit. Nahkampf und Strahlen prüfen den Treffer am Ende dieser Phase.
4. **Projektilflug:** Ein Geschoss legt seine Strecke mit der konfigurierten Geschwindigkeit zurück und trifft beim ersten tatsächlichen Körperkontakt. Gegen einen stationären Heldenkörper ist die ideale Zeit näherungsweise `windup + max(0, Distanz - (0.40 + projectileWidth/2)) / projectileSpeed`.
5. **Frame-Grenzen:** Windup-Ende und Projektilkontakt werden in Updates geprüft. Der Abschuss setzt die erste Flugprobe auf die aktuelle Spielzeit. Dadurch runden beide Phasen auf Frames; bei 60 FPS sind zusammen bis ungefähr 33 ms gegenüber der idealen kontinuierlichen Zeit möglich. Die Tests laufen zusätzlich bei 30 und 120 FPS.
6. **AOE:** Die vorhandene Ausholzeit beträgt meist 300–680 ms. Treffer hängen von der Position beim Einschlag ab; stationäre Dauerflächen behalten ihre Tick-/Mitgliedschaftsprüfung.
7. **Multiplayer:** Der Client sendet den Cast zum Host. Der Host beginnt die autoritative Ausführung beim Empfang; das Ergebnis muss anschließend zurück zum Client. Diese Transport- und Synchronisationszeiten kommen zu den Tabellenwerten hinzu und wurden hier nicht in einer echten Session gemessen.

## Zwei reproduzierte Ursachen für Fehlschüsse

**Breite Vorschau, schmale Trefferbahn.** Nyra E zeigt einen 45°-Kegel. Ein stationärer Gegner bei 20° und 4 m steht darin, aber außerhalb der maximalen Zielkorrektur von 8° und außerhalb der tatsächlichen Geschosslinie. Er wird bei sofortigem Loslassen genauso verfehlt wie nach einer Sekunde Halten. Längeres Zielen behebt diese geometrische Abweichung nicht. Das entspricht der bislang implementierten Korridor-Vorschau, passt aber nicht zur später geäußerten Erwartung, genau einen Gegner aus dem gesamten markierten Kegel zu erfassen.

**Eigene Bewegung während des Windups.** Die Richtung wird beim Cast fixiert, der Startpunkt dagegen erst am Ende der Ausholzeit aus der aktuellen Spielerposition gelesen. Ein bereits erfasster, stationärer Gegner kann dadurch verfehlt werden. Reproduktion: Mira Q, Gegner geradeaus bei 4 m, Seitwärtsbewegung während der 160-ms-Vorbereitung. Der Test liefert einen Weg mit `HeroCombatProfile.MoveSpeed * 0.88` (Standard-Angriffsbewegungsfaktor des Motors), insgesamt ca. 0,765 m bis zum Abschuss bei 60 FPS. Ohne diese Bewegung trifft Q, mit ihr verfehlt die parallel verschobene Projektilbahn den Gegner. Die native Beschleunigung/CharacterController-Bewegung wurde dabei nicht simuliert; die produktive Delivery erhält den kontrolliert vorgegebenen Bewegungsweg.

## Umgesetzte Korrektur

Die Verbindung sollte nicht von einer länger sichtbaren Markierung abhängen. Für gerichtete Einzelangriffe wird beim Loslassen sofort **ein** geeigneter Gegner aus dem dargestellten Zielkorridor ausgewählt und dessen damalige Position als Zielpunkt eingefroren. Beim Abschuss wird die Richtung vom aktuellen Ursprung zu diesem festen Punkt berechnet. Danach läuft das Projektil ohne Verfolgung und entscheidet weiter über die erste tatsächliche Kollision; der Gegner kann weiterhin ausweichen. Ohne geeigneten Gegner bleibt der manuell gewählte Schuss möglich.

Die Erfassung aus dem gesamten Kegel ist eine gezielte Änderung gegenüber der bisherigen 8°-Zielhilfe. Die schmaleren 35°/70°-Kegel erfassen maximal ±17,5°/±35° von der Mitte; derselbe `AimSpec.coneAngle` steuert Vorschau und Auswahl. `targetingMode = Cone` ist der Standard für alle elf gerichteten Skills; `Geometry` behält die begrenzte 8°-Hilfe für andere Geometrien. AOE wählt weiterhin keinen einzelnen Gegner aus und prüft alle gültigen Körper innerhalb der wirklichen Fläche. Bestehende Schaden-, Reichweiten-, Windup- und Cooldownwerte sollten für diese Korrektur unverändert bleiben.

Die erste Untersuchung änderte ausschließlich Tests und Dokumentation. Die anschließende Korrektur verändert Kegel-Erfassung und Schussausrichtung; Schaden, Reichweite, Windup, Cooldown und die bisherige 80°-Grundangriffsgeometrie bleiben erhalten. Die Laufrichtung bleibt der Start für schnelle Taps; der erste kleine Drag steuert den Kegel unabhängig davon in jede Richtung. `rememberAimDirection` ist eine optionale Alternative.

## Wiederholen

```bash
python3 Tools/Aiming/Regression/run.py \
  --mono-root /path/to/extracted-mono \
  --roslyn /path/to/roslyn/tasks/net472/csc.exe \
  --connection-timing
```

Die Ausgabe enthält CSV-Zeilen `TIMING` für alle acht Standards/32 Skills bei 30, 60 und 120 FPS, Fernkampf-Distanzproben und `GESTURE` für fünf Haltedauern. Auch ohne den Schalter werden die Assertions ausgeführt; der Schalter aktiviert nur die Messwertausgabe. Aktuelles Ergebnis: **1771 Assertions bestanden**, zusätzlich beide bestehenden Editor-Regressionssuiten. Die ehemalige 1228-Assertion-Untersuchung reproduzierte die oben beschriebenen Fehlschüsse; die aktuellen Tests erwarten stattdessen korrekt verbundene Kegel-Erfassungen und Schüsse trotz eigener Bewegung.

Für eine echte Handyprüfung die Zeitpunkte von HUD-Release, Host-Cast-Empfang, Delivery-Launch und `Health.TakeDamage` mit korreliertem Cast protokollieren und die Aufnahme gleichzeitig mit sichtbarer Markierung/Animation vergleichen. Bei Host/Client müssen Uhren oder Cast-IDs berücksichtigt werden; `Time.time` zweier Geräte kann nicht direkt voneinander abgezogen werden.
