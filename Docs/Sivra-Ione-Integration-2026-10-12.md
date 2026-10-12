# Sivra und Ione: Übernahme am 12.10.2026

Diese Übernahme aktiviert nur Sivra Quell (Nyra) und Ione Vaska (Elyra) auf `dev/vertical-slice`. Ausgangsstand: `ee0577a65db8e04ab9b7c17de09eb89a5019a873`. Die geprüften Assets kommen unverändert aus `ef8c9f91e6b0dae0d2a0e0ab169e205a480c578e` (PR #24). Der Sammel-PR wurde dafür nicht gemergt.

## Ergebnis

- Neue Köpfe und detaillierte Rüstung mit getrennten Bauteilplänen für beide Helden; vorhandene Körpergrundformen, Skelettverträge und Animationsquellen bleiben erhalten.
- Iones Fokus zeigt nach der bestehenden Laufzeitanpassung waagerecht nach vorne (+Z). Die kanonischen Ressourcenkoordinaten bleiben für den Waffenfitter unverändert.
- Sivras vorhandenes Gewehr erhält die geprüften flachen Serviceelemente; Griff- und Mündungsmarker bleiben erhalten.
- Die alten zusätzlichen Gliedmaßen- und Stiefeldetails werden nur bei erfolgreich geladenen Sivra-/Ione-Modellen übersprungen. Die anderen Helden behalten ihre bisherigen Darstellungspfade.
- Kein globaler HeroConcepts/StyleVersion-Schalter wird aktiviert. Ulti-Sounds und die übrigen Heldenassets werden nicht geändert.

## Nachweise

Die Konzeptbilder, exakten Prompts, Kopfquellen, Blender-/FBX-Dateien, Modellansichten, OBJ-Teile, maßhaltigen SVG-Projektionen und Herkunftsakten sind mit diesem Stand im Repository enthalten.

- Baupläne: `ArtSource/Heroes/BuildPlans/Sivra/` und `Ione/`.
- Herkunft: `ArtSource/Heroes/Concepts/Provenance/Sivra.json` und `Ione.json`.
- Modellprüfungen des ursprünglichen Blender-Exports: `ArtSource/Heroes/ConceptModels/validation-report.json` und die beiden `BuildPlans/*/validation.json`.
- Erneute Ressourcenprüfungen dieser Übernahme: `ArtSource/Heroes/ConceptModels/Sivra-Ione-integration-checks.json`.
- Reproduzierbare Ressourcenprüfung: `python Tools/HeroConcepts/validate_sivra_ione.py` vom Repository aus.

## Tatsächlich geprüft

Beide gzip/JSON-Ressourcen erneut eingelesen: endliche Geometrie und Normalen, gültige Dreiecke und Knochenzuordnungen, Hash-Abgleich gegen die archivierten Exportprüfungen. Pro Held wurden 27 synthetische Gelenkrotationen auf endliche Positionen und starre Bindungen geprüft. Iones umgerechnete Fokusachse ist [0, 0, 1]. Front-/Rückansichten der tatsächlichen Modelle wurden geprüft. Die geänderte C#-Datei wurde syntaktisch geprüft.

Die ursprünglichen Blender-/FBX-Rundlaufprüfungen werden als vorhandene, unveränderte Exportnachweise mitgeführt. Eine erneute FBX-Prüfung konnte in dieser Umgebung nicht abgeschlossen werden. Synthetische Gelenkrotationen sind keine tatsächlichen Spielclips oder Clippingprüfung.

Unity-Editor, tatsächliche Kampfanimationen im Spiel und Android-Geräteprüfung sind weiterhin offen. Dieser Commit ist eine Quellcode-/Asset-Integration, kein neu gebautes APK.

## Rechte

Die vorhandenen Rechteakten bleiben maßgeblich. Diese Übernahme ergänzt die dokumentierte Herkunft; sie erklärt nicht sämtliche historischen Designreferenzen oder Fremdassets als rechtlich geklärt. Die offenen Punkte in den Einzelakten bleiben ausdrücklich bestehen.
