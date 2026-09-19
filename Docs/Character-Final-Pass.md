# DIMENSIONS FRONT — Charakter-Pass

Status: **CODE READY** (Quellcode-/Syntaxprüfung; keine Unity-Kompilierung oder visuelle Freigabe).

| Hero | Sichtbare Änderungen |
|---|---|
| Zhaev Null | Asymmetrische Kapuze, Wangenplatten, hohe gebrochene Schulter, Klingenrücken und Handschutz |
| Auren Kest | Helmgrat, Halskragen, breite Schulterkappen, geschichteter Schild, kompakter Ankerhammer |
| Sivra Quell | Haarsilhouette, Zielschulter, Gewehrgehäuse, Magazin, Laufstreben, Mündungsbremse und Ziellinse |
| Ione Vaska | Dreiteilige Krone, hoher Kragen, lange Stoffbahnen, Fokusstein und mitbewegte Orbitlinsen |
| Tahla Venn | Kopfschmuck, weichere Schulterkonturen, Stoffeinsätze, gefasster Stabemitter und zusammenhängende Schildprojektoren |
| Brakk Orun | Flacher schwerer Helm, asymmetrische Schultern, geschichteter Schlagarm mit drei Kolben, breite Gebrauchsspuren |

Alle sechs: individuell proportionierter, kantig modellierter Brustkorb statt rotationssymmetrischem Torso; Gürtel, Schienbeinschutz und Stiefelkappen. Stoff bleibt matt, dunkle Panzerung wenig metallisch, helle Metallflächen erhalten stärkere Materialtrennung. Emission ist auf kleine Energieflächen begrenzt. VOIDFALL verändert weiterhin ausschließlich die Darstellung.

Die bestehenden benannten Bewegungspivots bleiben bestehen. Zusätzliche Rüstung an Beinen, Stiefeln, Schlagarm, Klinge und schwebenden Teilen bewegt sich mit diesen. Tahla-Projektorlicht folgt jetzt seiner bewegten Fassung. Zusammenfassung pro Material und Bewegungspivot erfolgt einmal beim Aufbau; keine neuen Lichtquellen, Collider oder Partikelsysteme. Geometrie und Materialressourcen werden vom bestehenden Besitzer freigegeben. Renderkosten und Clipping müssen in Unity gemessen werden; keine FPS-Zusage.

Freigabe in Unity: alle sechs Standard-/VOIDFALL-Modelle aus Spielkamera und Menü prüfen, Bewegung/Attacke/Treffer/Tod/Respawn durchlaufen, Flügel und Waffen auf Clipping kontrollieren, zehn gleichzeitige Heroes auf Android profilieren. Keine Gameplay-/Balancewerte geändert.
