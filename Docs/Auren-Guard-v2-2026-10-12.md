# Auren Guard v2: Vorschau und Kampfpose angleichen

Nach dem ersten Pose-Commit meldete der Nutzer keinen sichtbaren Unterschied. Im zusätzlichen Präsentationspfad standen noch die alten absoluten Armziele. SiegeTankAppearance.AlignGuard richtete den Hammer wieder auf seine alte aufrechte Waffenachse aus; ClearIdleWeaponOverlap konnte den Tankhammer außerdem wie ein anderes langes Prop nach unten drehen, wenn die Kampfpose nicht aktiv war.

Die Vorschau-/Fallback-Pose und MeleeBasicAttackAnimation verwenden jetzt dieselben relativen Handziele. AlignGuard verwendet die feste diagonale Hammerkalibrierung und berücksichtigt den Guard-Blend; Gewicht 0 lässt einen aktiven Schlag unverändert. Die allgemeine Prop-Korrektur nimmt Kaelon aus. Der Schildarm wird vor der neuen Guard-IK ebenfalls aus der gespeicherten Restpose aufgebaut, damit keine alte Armrotation zur nächsten Ausgangspose wird. Die Menüinstanz erhält die Guard unmittelbar nach dem Erfassen der Animationsbasen, vor dem Einpassen der Kamera.

## Nachweis

Der erweiterte Test kompiliert die tatsächlichen Hammer-/Handziel- und AlignGuard-Methoden aus dem Produktionscode. Mit der vorherigen AlignGuard-Methode scheitert der neue Test:
`Preview final alignment retains the marked diagonal: 0.8989222 != 1`.
Mit der korrigierten Methode bestehen alle 310.707 Assertions sowie die bisherigen 1.728 Pipeline-Auswertungen. Der Test prüft auch Gewicht 0, wiederholte Kalibrierung, 360-Grad-Drehungen, Griffkontakte und identische Guard an Anfang und Ende des Sweeps.

Das sind isolierte C#-/Transform-Tests, keine Unity-Sichtprüfung. Der bestehende Tank, seine Geometrie und die anderen Helden werden nicht ersetzt.

## Prüfung in Unity

Den aktuellen `dev/vertical-slice`-Stand laden und Play Mode neu starten. Beim ersten Aufbau des Tanks erscheint in der Konsole:
`AUREN GUARD v2: shield forward, hammer diagonal`.

Unter `DIMENSIONS FRONT/Checks/Live Mixamo pose` meldet der Tank außerdem `AUREN GUARD v2` mit seiner gemessenen Armabweichung. Fehlt v2, läuft ein anderer oder älterer Skriptstand. Die tatsächliche sichtbare Haltung bleibt vor Ort in Unity zu prüfen.
