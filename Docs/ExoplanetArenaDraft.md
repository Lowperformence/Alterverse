# Zwei neue Habitat-Arenen – Implementierung

Repository: `Lowperformence/Alterverse`, Zielbranch `dev/vertical-slice`.
Grundlage: `0c1e2e83f840da01032a3432e429302c5d4baab9`.
Dieser Änderungssatz enthält beide Karten samt Hotzone-Monstern und Habitat-Guards. Er ist für den Entwicklungsbranch bestimmt; Unity-/Geräteprüfung und Veröffentlichung eines Spiel-Builds stehen noch aus.

| Arena | Habitat und Landschaft | Hotzone-Monster | Guard-Mech |
| --- | --- | --- | --- |
| WASP-76b / IRONRAIN FRONT | Eisenkruste, Kupferformationen, warme Eisenadern, dunkle Wolken unter der Arena | Ferrox: flacher gepanzerter Sechsbeiner mit zwei Scheren, ohne Skorpionschwanz | Eisenpanzerung, Kupferplatten, Hitzeschilde an den Schultern, amberfarbene Armadern |
| TRAPPIST-1e / TWILIGHT EDEN | Petrol-violetter Dämmerboden, Wasserstellen, leuchtende Pflanzen am warmen Rand, Frost und Eis am kalten Rand | Vespera: vierbeiniger Amphibienkörper, Kiemenfächer, amberfarbener Kamm, organische Leuchtknoten | Petrol-violette Panzerung, organische Kiemenplatten, leuchtende Armadern |

Die Spielfeldform, Bodenhöhen, Basen, Heilflächen, Bunker, Wege und Gameplay-Kollisionen folgen der Mond-Arena. Die warmen/kalten Zonen von TRAPPIST-1e laufen quer zur Links/Rechts-Teamachse. Farb- und Materialprofile, Randformationen, Hintergrund und Kreaturen sind eigens an die neuen Habitate angepasst.

Die Karten sind im Entwurf direkt und kostenlos auswählbar; ein Verkaufspreis wurde nicht erfunden. Die bisherigen Arena-IDs 0–10 bleiben erhalten; WASP-76b ist 11 und TRAPPIST-1e ist 12. Auswahl, Shuffle und Coop-Lobby verwenden den vorhandenen Katalog. Die Map-Sieg-Maske akzeptiert jetzt 13 Karten. Es gibt keine neuen Kaufprodukte oder Änderungen am Kaufguthaben.

## Technischer Rahmen

- Derselbe Ground-Builder und dasselbe Texturgrößen-Limit wie Moon: 144 × 128 Bodenabschnitte; 1536² bzw. 2048² Albedo/Normal je nach Grafik-Speicherklasse.
- Vorhandene, im Build enthaltene Resources-Shader und Detailtexturen; eigene Habitat-Farben werden getrennt davon gebacken.
- Acht Randregionen mit bestehenden Material-Batches und regionalem Culling. Die Details enthalten keine Gameplay-Collider oder zusätzlichen Lichter.
- Auch die zusätzliche SpaceKit-Deko verwendet eigene Habitat-Paletten und Sets: geschlossene Eisenregen-Pods/Metallformationen bzw. Dämmerpflanzen und kalte Felsränder. Neue Karten fallen hier nicht auf das Moon-Set zurück.
- Ein 256²-Hintergrundbild aus numerischen Wolkenmustern; mipmapped, CPU-Kopie nach dem Upload freigegeben. Keine Partikelwolken oder Postprocessing-Abhängigkeit.
- Ferrox/Vespera werden pro animiertem Gelenk und Material gebündelt. Glatte Körper-Grundgeometrie: 325 Vertices, 528 Dreiecke. Im Editor-Check: höchstens 24 Render-Batches und weniger als 24.000 Vertices pro Monster.
- Guard-Habitatdetails: vier zusätzliche Mesh-Batches, die an bestehenden Schulter-/Unterarmteilen hängen. Die vorhandene Bein-/Arm-Animation und das Angriffssystem werden weiterverwendet. Guard-Werte bleiben bei 520 HP und 13 Angriffsschaden; Radius/Leash/Cooldown bleiben bestehen.
- Beide Hotzone-Monster verwenden die bestehende HotZoneTank-AI und Kampfwerte. Ferrox erhält Scherenanimationen und Eisen-Projektilfarben; Vespera Kiemenanimationen und Bio-Projektile. Die bestehende Attack-Event-Replikation präsentiert dieselben Bewegungen auf Coop-Clients.
- Erzeugte Meshes, Materialien und Hintergrundtextur werden mit ihren Besitzern freigegeben. Projektweite geteilte Meshes/Resources werden nicht zerstört.

## Ausgeführte Prüfungen

- 75 Commerce-/Profil-/Social-/Billing-Unit-Tests bestanden, einschließlich Map-Auswahl und Map-Siegen nach Neustart für beide neuen IDs.
- Alle 21 geänderten/neuen C#-Dateien syntaktisch mit dem C#-Parser geprüft.
- Numerische Prüfung der Monster-Grundgeometrie: endliche Koordinaten, nach außen orientierte und nicht degenerierte Dreiecke.
- Das Patch wurde gegen die exakten Originaldateien der oben genannten Grundlage geprüft und in eine separate Prüfkopie angewendet.

Die vollständige Python-Testentdeckung konnte drei Module wegen hier fehlender Abhängigkeiten (`PyJWT`, `google-cloud-firestore`) nicht laden. Diese Module sind nicht als bestanden ausgewiesen. Es wurde kein Live-Kauf oder echter Cloud-Account-Vorgang ausgeführt.

## Noch im Unity-Projekt prüfen

Hier ist kein Unity-Editor oder Android-Gerät vorhanden. Deshalb sind C#-Kompilierung im Unity-Projekt, sichtbare Spielansicht, Framezeiten und echte Online-/Coop-Verbindungen noch nicht bestätigt.

Unter `DIMENSIONS FRONT/Checks` wurden Checks für den Katalog und die Mond-Geometrie, beide Monsterkörper samt Attack-Bindung und beide Habitat-Guards ergänzt. Der vorhandene Check `All planet landscape details (Play Mode)` prüft die beiden neuen Varianten ebenfalls.

Vor einer Veröffentlichung: beide Karten für ZERO/LUMEN starten, Hotzone-Belohnung für beide Teams auslösen, Guard- und Monsterangriffe ansehen, Mapwechsel und Match-Neustart durchführen und den Android-/Tablet-Build testen. Beide Coop-Teilnehmer benötigen den neuen Build. Die Serveränderung an der Sieg-Maske muss zusammen mit bzw. vor dem neuen Client bereitgestellt werden, damit der bisherige Server die neuen Map-Siege akzeptiert.
