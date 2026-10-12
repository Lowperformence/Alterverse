# Auren Kest: markierte Standardhaltung

Am 12.10.2026 wurde entschieden, den bestehenden Tank zu behalten. Diese Änderung setzt ausschließlich seine markierte Standardhaltung um.

Der Schildarm wird stärker angewinkelt und nach vorne vor den Oberkörper gebracht. Die Hammerhand steht angewinkelt neben dem Körper; der Schaft zeigt diagonal nach oben, zur Waffenseite und leicht nach vorne. Der Hammerkopf steht damit neben dem Kopf. Die bestehende feste Kalibrierung dreht Hand und angebundene Waffe gemeinsam; Griffkontakte und Knochenlängen bleiben erhalten. Schlag und Rückkehr verwenden dieselbe neue Schaftachse, damit der Hammer an beiden Übergängen seine Haltung beibehält.

Betroffen sind nur der Kaelon-Guard-Zweig in MeleeBasicAttackAnimation und die Guard-Ausrichtung in SiegeTankAppearance. Die bestehende Tankgeometrie und Animationsquellen werden nicht ersetzt. Der neue Auren-Crest-Entwurf wird nicht integriert.

## Prüfung

Der bestehende UpperBodyRegression-Lauf wurde um Prüfungen der tatsächlich extrahierten Hammer-Ausrichtungsmethoden ergänzt. Bestanden: 310.346 Assertions, 1.728 Pipeline-Auswertungen; 360-Grad-Drehungen, wiederholte Kalibrierung ohne Drift, Griffkontakte, Vorwärtsposition und Beugung beider Arme, vollständige und halbierte Unterarme sowie Start und Ende des Hammer-Sweeps in identischer neuer Guard-Pose. Die übrigen vorhandenen Profil- und Lebenszyklusprüfungen bestanden ebenfalls.

Ausführung:
`python Tools/Movement/UpperBodyRegression/run.py --mono-root <Mono-Installation>`

Der Test verwendet Transform-/Mathematik-Doubles und prüft keine gerenderten Unity-Meshes oder echten Kampfclips. Unity- und Android-Sichtprüfung sind noch offen.

## Vorgabe

Die hochgeladenen Spiel-Screenshots und ihre Markierung wurden visuell gelesen. Eingabedateien und SHA-256:
{"01-image-markup-preview-file_00000000706882469d442a5ff598d74b.png": "c2b5b0e2b12dfbc9a85089a10d278702535731e6204d2350afa9b80d08497ffc", "image(20261012-164522).png": "7a25233b16ae2bf89b8c46336fc8e6db7029a2911a4845c4e71729a31645e512", "image-edit-target-943fee953b3e41cf.png": "7a25233b16ae2bf89b8c46336fc8e6db7029a2911a4845c4e71729a31645e512"}

Die im Chat erzeugte Bildbearbeitung ist eine Posevorlage, kein Screenshot einer ausgeführten Unity-Änderung.
