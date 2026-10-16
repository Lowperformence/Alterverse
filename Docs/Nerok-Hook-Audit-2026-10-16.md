# Nerok: Haken, Zielanzeige und Attacken

## Befund und Änderung

Der Haken verwendet einen 70° breiten Auswahlkegel bei 6,2m Reichweite. Der Kegel erfasst
genau einen Zielpunkt beim Loslassen. Danach fliegt ein 0,35m breiter Haken nach 0,30s
Vorbereitung mit 18m/s geradeaus. Der Kegel ist keine flächige Trefferzone. Ein Gegner kann
daher nach dem Loslassen im Kegel bleiben und der eingefrorenen Flugbahn trotzdem ausweichen.
Die bestehende Auswahl erreicht beide Kegelränder und die maximale Reichweite korrekt.

Ein zusätzlicher Fehler ließ echte Körperkontakte neben Deckung verschwinden: Nach dem
kontinuierlichen Wand-/Körpersweep prüfte `ResolveSharkHookHit` nochmals die Sichtlinie vom
Abschussort zur aktuellen Körpermitte. Der Körper konnte seitlich mit dem Haken kollidieren,
während diese zweite Linie die Deckung berührte. Der Treffer wurde dann ohne Schaden verworfen.
Die doppelte Sichtprüfung entfällt. Die tatsächliche Projektilkollision blockiert weiterhin
an Wänden. Für das anschließende Heranziehen bleibt eine eigene freie Sichtlinie zu Nerok
erforderlich; ein Kontakt neben Deckung darf Schaden auslösen, aber nicht durch die Wand ziehen.

Beim Halten von Anchor Recall erscheint zusätzlich zum schwächeren Auswahlkegel die schmale
tatsächliche Flugbahn. Sie zeigt auf den erfassten Punkt und endet am ersten aktuellen
Körperkontakt oder Hindernis. Die Vorschau verwendet dieselben Methoden für Wandabstand und
Körperkontakt wie die Ausführung. Loslassen und Abbrechen entfernen beide Flächen. Die
Skillbeschreibung erklärt Auswahlpunkt, schmale Bahn, ersten Treffer und Ausweichmöglichkeit.

Schaden, Reichweite, Trefferbreite, Kegelwinkel, Projektilgeschwindigkeit, Vorbereitung,
Cooldowns, Pullstärke und Netzwerkprotokoll bleiben bei ihren vorhandenen Werten.

## Verifikation

`Tools/Combat/NerokRegression/run.py` kompiliert die aktuellen Produktionsquellen gegen die
vorhandenen deterministischen Engine-/Netzwerk-Fixtures. 284 Assertions bestehen. Der
Deckungsfall wurde zuerst mit unverändertem Produktionscode ausgeführt und scheiterte
reproduzierbar: 1000 statt 972 Lebenspunkte. Nach der Korrektur bestehen dieselben Fälle
bei 30/60/120 FPS.

Auch Grundangriff, Pressure Hull, Breaker Wave und Maelstrom wurden auf ihre jeweiligen
Schadens-, Schutz-, Timing-, Radius-, Team- und Cooldownregeln geprüft. Die V5-Hakenübertragung
wird mit dem Produktionshandler und einem Speicherpuffer ausgeführt. Die neue CI-Suite
wiederholt die Prüfung bei relevanten Änderungen.

## Noch im Unity-Build prüfen

- Vorschau am Gerät bei verschiedenen Kamerawinkeln: Kegel schwach sichtbar, Flugbahn auf
  dem Boden lesbar, Breite/Endpunkt am Ziel und an Wänden, keine Restflächen nach Abbruch.
- Nativer Körperkontakt an einer Deckungskante: Schaden bei echter Berührung; kein Pull
  durch Deckung. Frontale Deckung blockiert weiterhin den gesamten Haken.
- Bewegte Gegner: seitliches Ausweichen nach Loslassen bleibt möglich. Sichtbarer Haken,
  Einschlag und Beginn des Pulls sollen zeitlich passen.
- Host und Gast mit echter Latenz: gleiche Hakenrichtung, Treffer und Pull; zusätzlich
  Shield-Laufzeit und native Vortexbewegung prüfen.

Hier wurden weder Unity gestartet noch ein Android-Build oder ein echtes Mehrspielermatch
ausgeführt. Die Fixtures prüfen Shaderparameter, aber rendern keine Pixel und simulieren
keine native Unity-Physik.
