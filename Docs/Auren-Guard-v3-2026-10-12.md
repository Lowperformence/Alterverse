# Auren Guard v3: Bildseite der markierten Standardhaltung

Der Screenshot image(20261012-171000).png zeigte weiterhin einen Hammerkopf hinter/über dem Kopf. Die vorherigen Guard-Werte kippten die Waffe entlang der positiven Körper-Rechtsachse. Bei der Heldenvorschau steht die Kamera auf +Z, blickt nach -Z und der Held ist um -24 Grad gedreht. Die Bild-Rechtsachse liegt damit entgegengesetzt zur Körper-Rechtsachse. Ein Test der Körperpose ohne diese Kamera bestätigte deshalb eine falsche sichtbare Seite.

Die rechte Hand wird jetzt vor dem Oberkörper zur gegenüberliegenden Seite gebracht (relativer Schulteroffset -0.60, -0.53, +0.42). Der Schaft zeigt leicht schräg nach oben, zur markierten Bildseite und nach vorne (Achse -0.20, +0.95, +0.20 vor Normalisierung). Die bestehende Griffbindung bleibt erhalten. Vorschau und Schlag-Rückkehr verwenden weiterhin dieselben gemeinsamen Guard-Funktionen. Der alte Tank und seine Geometrie bleiben bestehen.

## Validierung

Der neue C#-Regressionstest enthält nun ausdrücklich die Kamera-Bildachse und die -24-Grad-Vorschauausrichtung. Die alte v2-Achse scheitert am Test "Hammer head tilts toward screen-right of the front portrait". Die korrigierte Version besteht alle 311.427 Assertions und 1.728 Pipeline-Auswertungen, einschließlich stabiler Griffbindungen, 360-Grad-Drehungen, verkürzter Unterarme und gleicher Haltung bei Schlagbeginn/-ende.

Eine separate Blender-Projektion aus der vorhandenen Tank-Ressourcengeometrie wurde zur Prüfung der seitlichen Anordnung verwendet. Diese Ersatzprojektion verwendet die Ressourcen-Anker und ist keine Unity-Ausführung oder Bestätigung der vollständigen FBX-Passform.

## Live-Check

In Play Mode den Tank auswählen und `DIMENSIONS FRONT/Checks/Auren marked guard` aufrufen. Die Konsole gibt unter `AUREN GUARD v3 CHECK` aus:
- den tatsächlich geladenen Pose-Status;
- die aktuelle Schaftachse in Actor-Koordinaten;
- die gemessene Differenz zwischen Hammerkopf und Kopf;
- ob der Griff tatsächlich an der Hand hängt.

Die Versionsmeldung des Laufzeit-Posecodes lautet `AUREN GUARD v3`. Editor- und Android-Sichtprüfung sind hier nicht ausführbar. Die tatsächliche sichtbare Haltung muss anhand des Live-Projekts überprüft werden.
