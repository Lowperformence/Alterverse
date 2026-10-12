# Ione Vaska — Bauteilkonzept und Blender-Aufbau

Stand: 12.10.2026. Spiel-ID: `Elyra`. Revision: `hero-component-plans-20261012-v3`.

Nachtrag Schulterausrichtung: Die aktuelle Laufzeitressource dreht seit `ione-outboard-shoulders-20261012-v1` die sechs Lamellenpakete auf die jeweilige Schulteraußenseite und setzt sie auf die Gelenke. Nachweis: `Docs/Ione-Schultern-2026-10-12.md` und `ArtSource/Heroes/ConceptModels/Ione-shoulder-orientation-checks.json`. Die hier archivierten Blender-/OBJ-/SVG-Exporte und die ursprüngliche `validation.json` zeigen den vorausgehenden Exportstand; der aktualisierte Generator erzeugt die neue Orientierung bei einem erneuten Blender-Bau.

## Gestaltung

| Baugruppe | Verbindliche Gestaltung |
|---|---|
| Kopf / Helm | Weiße geschlossene Keramikmaske, violette Optiken, Stirnraute und kompakter gestützter Orbitalbogen. |
| Torso / Kleidung / Rüstung | Weiße Brustschalen über dunkler segmentierter Mechanik, drei Konstellationspunkte, getrennte Rückenplatten. |
| Arme / Hände | Große runde Schultergrundformen mit gestaffelten Keramiklamellen; Unterarmdeckel mit violetten Einsätzen. |
| Beine / Füße | Weiße Oberschenkel- und Schienbeinschalen, schwarze Gelenke, violette Hüftringe und Knierauten. |
| Waffen | Schwarz-weißer gefalteter Fokus mit violettem Emitter. Im Blender-Modell waagerecht nach vorne; kanonische Laufzeitkoordinaten bleiben passend zum vorhandenen Waffenfitter. |

## Dateien und Maßbezug

- `Ione-component-concept.png`: KI-generierte gestalterische Bauteiltafel. Sie ist keine maßhaltige Konstruktionszeichnung.
- `Ione-parts.blend`: aus dem tatsächlichen Modell zerlegte Bauteile in Montageposition, Objekte `PLAN_*`.
- `Ione-exploded.blend`: dieselben Teile räumlich auseinandergeschoben. Die Verschiebung ist nur eine Ansicht.
- `*-orthographic.svg`: echte Front-, Seiten- und Rückprojektionen der Modellgeometrie einschließlich gemessener Abmessungen.
- `*.obj`: Bauteiloberflächen in Blender-Koordinaten, Meter; Materialbindung bleibt in der Blender-Datei.
- `assembly.json`: Bauteilliste, Geometriezahlen und Grenzen in Metern sowie Millimeterabmessungen.
- `validation.json`: tatsächlich ausgeführte Export- und Rigprüfungen. Existiert erst nach erfolgreichem Bau dieses Helden.
- Einzelne neu modellierte Panzerungs- und Wartungsteile: `../ConceptModels/Ione-new-parts.obj`, Teilnamen und Vertexbereiche im Validierungsbericht.

## Aufbau in Blender

1. Bestehende Grundproportionen und Skelett als Passformreferenz verwenden. In metrischen Einheiten mit Unit Scale 1 arbeiten. Blender: X rechts, Z oben, -Y vorne. Laufzeitgeometrie: X rechts, Y oben, Z vorne.
2. Kopf/Helm, Torso, linken/rechten Arm, linkes/rechtes Bein und Waffen getrennt prüfen. Schwanz bleibt bei Nerok und Timmy eigene Baugruppe.
3. Hauptschalen zuerst auf den vorhandenen Körper setzen. Danach eingerückte Wartungsflächen, Randfasen, Lüftungsschlitze, Nähte und geschützte Lichtkanäle ergänzen.
4. Große Gelenke freilassen. Jede starre Schale nur an ihren zugehörigen Knochen binden. Keine Platte über Schulter, Ellbogen, Hüfte, Knie oder Sprunggelenk hinweg binden.
5. Außenpanzerung in Baugruppen montieren. Neue Schalen, Einlagen und Hardware sind im Generator benannt und reproduzierbar. Konzeptbild-Feinstrukturen werden nur übernommen, soweit sie in der tatsächlichen Modellgeometrie gebaut sind.
6. Waffen auf ihre bestehenden Griff-, Stütz- und Mündungs-/Fokusmarker setzen. Originalkontaktpunkte und Animationskurven erhalten. Neue flache Serviceelemente bleiben an der vorhandenen Waffenoberfläche.
7. Spielgeometrie mit demselben vorhandenen Exportvertrag erzeugen. Voransichten zeigen das echte Modell, keine Konzeptbildprojektion.
8. FBX importieren und Skelett, Gewichtsnormalisierung, vorhandene Animationen und Waffenpositionen prüfen. Modell und Teilpläne werden erst nach diesen Prüfungen committed; dann folgt der nächste Held.

## Detail- und Laufzeitstrategie

Neue Details bestehen aus flachen profilierten Schalen, eingelassenen Feldern, sparsamen Befestigungen und kurzen Lichtkanälen. Innenmechanik ist keine Pflichtgeometrie. Kleinste Gravuren gehören bei späterem Textur-Baking in Normal-/Maskenkarten. Der aktuelle Bau verwendet reale Geometrie; der Bericht nennt ihre tatsächlichen Vertex-/Triangle-Zahlen. Ein FPS-Gewinn oder Android-Budget wird ohne Geräteprofiling nicht behauptet.

## Herkunft und Nutzungsrechte

Das Bild entstand mit OpenAI-Bildgenerierung aus dem bestehenden projektinternen Modellrender. Exakter Prompt und SHA-256 stehen in `provenance.json`. Neue hier erzeugte Entwürfe und Code-gestützte Geometrie unterliegen den OpenAI-Ausgaberechten; das ist keine pauschale CC0-Erklärung. Vorhandene Basen, Animationen, Bilder und andere Fremdbestandteile behalten ihre jeweilige Rechtekette in `ArtSource/Heroes/Concepts/Provenance/Ione.json`. Siehe auch `../RIGHTS.md`.

## Abschlusskriterien

Konzepttafel archiviert; Bauteile und echte Projektionen vorhanden; Spielmodell exportiert; Rig-/FBX-Prüfungen bestanden. Unity-Editor, Kampfanimationen im Spiel, Android-Clipping und Geräteprofiling sind separat zu prüfen und werden bis zur tatsächlichen Durchführung als offen geführt.

