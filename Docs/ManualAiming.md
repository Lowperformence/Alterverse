# DIMENSIONS FRONT – manuelles Zielen

Stand: 2026-10-11. Basis des Gesamtumbaus: `dev/vertical-slice`, Commit `c9f60174fd4d6e99557843e41c080ca30a907484`.

## Analyse des bisherigen Systems

`PrototypeHUD` verarbeitet EnhancedTouch und den linken Bewegungsjoystick. Bisher löste die Angriffstaste einen Soft-Target-Angriff aus; Halten wiederholte Grundangriffe, Skills verlangten das Loslassen nahe der ursprünglichen Taste. `PlayerHeroAbilities` und `BasicAttack` erfassten über `SoftTargeting.Acquire` ein bevorzugtes Ziel. `HeroAttackDelivery` speicherte dessen `Health` und wandte nach Windup/Flugzeit Schaden an. Bewegte Ziel-Transforms wurden auch an die Projektil-VFX übergeben. Dadurch konnten Ziele trotz Ausweichen getroffen werden.

Die bestehenden Quellen bleiben maßgeblich: `HeroAbilityKit` definiert die 32 Skills mit Power, Cooldown, Reichweite, Radius und Windup; `HeroCombatProfile`/`HeroIdentity` liefern Grundangriffswerte. `HeroAbilityExecution` enthält Schaden, Kontrolleffekte, Heilung, Schilde, Dash und Spezialmechaniken. `Health.Active` ist die vorhandene Zielregistry; Hero-/Minion-Kollisionen hängen nicht zwingend von aktiven Unity-Collidern ab. `HeroLocomotion.FrameStartPosition` erlaubt kontinuierliche Trefferprüfung auch bei schneller Bewegung. Objective-Collider und `GameplayCollision.SightMask` bleiben für Oberflächen und Hindernisse maßgeblich.

## Bedienung und gemeinsame Architektur

- Linken Finger bewegen, rechten Finger auf einer **Skilltaste** halten und ziehen. Bewegung läuft unabhängig weiter.
- Für Skills erscheint die Vorschau sofort. Loslassen außerhalb der Abbruchzone führt den Skill aus, auch wenn kein Gegner getroffen wird. Loslassen in der sichtbaren Abbruchzone oder ein verlorener/abgebrochener Touch verwirft die Skill-Geste ohne Cooldown.
- **Standardangriff:** Antippen startet bei einem gültigen Soft Target sofort einen Angriff mit bestehendem Windup. Gedrückthalten wiederholt den Angriff jeweils nach dem bestehenden Cooldown. Er verwendet weder manuelles Ziehen noch Kegel-/Linienvorschau oder Abbruchzone. Die bisherige automatische Zielwahl mit Heldenpriorität, Entfernungs-/Richtungswertung und Sticky-Ziel ist wiederhergestellt; sie kann auch Gegner seitlich oder hinter der Figur auswählen. Loslassen, Verlassen der Taste (mit 20-Pixel-Toleranz) und verlorener Touch stoppen Wiederholungen; Loslassen erzeugt keinen zusätzlichen Schuss. Eine während Cooldown oder ohne Ziel begonnene gehaltene Berührung greift an, sobald Cooldown und gültiges Ziel es erlauben. Ohne geeignetes Ziel wird kein Cooldown verbraucht.
- Ein kurzer **Skill-Tap** verwendet die Blickrichtung mit begrenzter Zielhilfe. Skills haben keine Mindesthaltezeit. Standardangriffe richten sich auf ihr automatisch gewähltes Soft Target aus; tatsächliche Kollisionen und Reichweiten entscheiden weiterhin über Treffer.
- Stun, Tod, Pause, Fokusverlust, deaktivierte Steuerung und Safe-Area-Wechsel unterbrechen gehaltenes Zielen. Cooldowns werden erst bei erfolgreicher Ausführung beziehungsweise Client-Anforderung gestartet.
- Tastatur-Taps und Mausgesten nutzen dieselbe Aiming-/Ausführungslogik.

`ManualAiming` besitzt genau eine **Skill-Geste** (Slots 0–3) und liefert einen `AimPose` aus Richtung, Bodenposition und optionalem Support-/Homing-Ziel. `PrototypeHUD` besitzt die Touch-IDs. `AbilityRangePreviewOverlay` und `PlayerHeroAbilities` konsumieren diese gemeinsame Pose und den `AimSpec` aus `AimingSettings`. Die Standardangrifftaste (HUD-Slot 4) ruft bei Press/Hold direkt `BasicAttack.TryAttack()` auf. Dort wählt `SoftTargeting.Acquire` wieder automatisch einen Gegner und erzeugt die zu diesem Zeitpunkt feste Angriffsrichtung. Ein erfolgreicher Angriff verlängert die bisherige Sticky-Dauer über `RegisterOffensiveAction`. Normale Projektile erhalten dennoch keine Zielreferenz zum Verfolgen. Die gemeinsame Treffer-, VFX- und Multiplayer-Ausführung bleibt erhalten.

`AimGeometry` berechnet ebene Kegel, Kreise, Reichweiten-Clamping, begrenzte Zielhilfe und kontinuierliche Projektilkontakte. `HeroAttackDelivery` verwaltet Windups und freigegebene Geschosse in einem begrenzten Array. Die bisherigen Bot-Methoden bleiben Adapter: Sie frieren die gewählte Richtung beim Cast ein und verwenden anschließend dieselbe geometrische Trefferprüfung. Es gibt keinen parallelen Soft-Target-Schadenspfad für diese Angriffe.

## Trefferverhalten

- **Nahkampf:** Zum Kontaktzeitpunkt wird der nächste gültige Gegner innerhalb von Reichweite und Richtungskegel getroffen. Normale Einzelangriffe beschädigen nicht automatisch alle Gegner im Kegel. Gegner außerhalb werden verfehlt; Wände blockieren.
- **Projektil:** Der Kegel ist eine Ausrichtungshilfe. Das Geschoss bewegt sich mit eigener Geschwindigkeit und Breite geradlinig. Der erste tatsächliche Gegnerkontakt trifft; Hindernisse oder maximale Flugstrecke beenden das Geschoss. Relative Sweeps berücksichtigen Zielbewegung und verhindern Tunneling. Endet ein Geschoss innerhalb eines Frames an Wand/Reichweite, wird spätere Bewegung dieses Frames nicht als Treffer gewertet.
- **Strahl:** Die schmale Linie wird nach dem bisherigen Windup einmal geprüft. Sivras Prism Shot behält seinen blauen Laser. Ausweichen während des Windups bleibt möglich; der Strahl verfolgt danach weder Schütze noch Gegner.
- **Boden-AOE:** Der rechte Daumen setzt den Mittelpunkt innerhalb der Cast-Reichweite. Die Bodenposition bleibt nach Release fest. Treffer entstehen erst nach dem bestehenden Windup gegen Gegner, die dann tatsächlich in der Fläche stehen.
- **Selbst-AOE:** Mittelpunkt ist die Spielerposition beim Cast, entsprechend der bisherigen Ausführung. Keine Umwandlung in gerichtete oder frei platzierbare Angriffe.
- **Nerok:** Anchor Recall trifft durch Geschosskollision und zieht erst danach. Wand-, Pull-, Schild- und Passive-Prüfungen bleiben erhalten. Abyssal Maelstrom bleibt drei Sekunden aktiv und wendet unverändert sechs halbe-Sekunden-Schadensticks an; Pull und Flächenzugehörigkeit werden fortlaufend geprüft. Manuelle Platzierung behält die Sichtprüfung gegen Wände.
- **Sivras Ultimate:** Drei Geschosse mit ursprünglichem Gesamtschaden und Abstand von 0,18 s, alle in derselben Release-Richtung. Alle drei Queue-Plätze werden vor dem Cooldown reserviert. Stun verwirft noch nicht freigegebene Schüsse; bereits fliegende Geschosse bleiben bestehen. Tod beendet die bisherigen castergebundenen Effekte.
- **Support:** Tahlas W/E wählen nur geeignete verbündete Helden im gerichteten Auswahlbereich, einschließlich eigener Figur, wenn berechtigt. Der gewählte Verbündete erhält einen Vorschau-Marker. Gegner können diese Effekte nicht empfangen.
- **Homing:** Keine vorhandene offensive Attacke erhält standardmäßig Homing. Nur eine explizite Konfiguration mit `aimType = Homing` **und** `targetingMode = Homing` darf einen gültigen erfassten Gegner verfolgen. Auch sie benötigt einen tatsächlichen Kontakt und wird von Hindernissen/Reichweite begrenzt; bei Zielverlust wird kein anderes Ziel erworben.
- **AOE-Kegel:** `AreaCone` ist als konfigurierbare Flächenmethode vorhanden und geprüft. Keine bestehende kreisförmige Attacke wird ohne Override in einen Kegel umgewandelt.

Normale offensive Posen geben keinen Gegner-Transform an die Projektil-VFX weiter. Die bestehenden VFX, Animationen, Sounds, Farben und Namen bleiben erhalten; Flugpfad und Lebensdauer werden an den geometrischen Angriff gebunden. Beim Kontakt/Blockieren beendet eine Delivery-ID nur die zugehörigen Flug-VFX, ohne andere schnelle Angriffe abzuschneiden.

## Vollständige Zuordnung

Reichweiten und Radien unten sind die unveränderten Katalog-Ausgangswerte in Spieleinheiten. Kegelwinkel sind volle Öffnungswinkel. Bei Projektilen bezeichnet der Winkel die Zielhilfe, nicht die Schadensfläche. **Die Grundangriffstabelle beschreibt die Treffergeometrie; Standardangriffe zeigen keinen Kegel an.** `Self` und `Ally` bewahren ihre Utility-Mechanik.

| Held / interne ID | Grundangriff | Reichweite |
| --- | --- | ---: |
| Zhaev Null / Veyr | Nahkampf, 80° | 2,25 |
| Auren Kest / Kaelon | Nahkampf, 80° | 2,3 |
| Sivra Quell / Nyra | Projektil, 45° | 6,8 |
| Ione Vaska / Elyra | Projektil, 45° | 6,1 |
| Tahla Venn / Mira | Projektil, 45° | 6 |
| Brakk Orun / Rook | Nahkampf, 80° | 2,5 |
| THE TRAVELER / Traveler | Projektil, 45° | 6,2 |
| Nerok Abyss / Nerok | Nahkampf, 80° | 2,6 |

| Held | Slot | Bestehender Name | Zieltyp | Cast-Reichweite | AOE-Radius |
| --- | --- | --- | --- | ---: | ---: |
| Veyr | Q | Null Cut | Nahkampf 80° | 3,1 | — |
| Veyr | W | Rift Scar | Projektil 80° | 3,3 | — |
| Veyr | E | Gapfold | Dash-Richtung | 4,2 | — |
| Veyr | Ult | Vacant Echo | Nahkampf 80°, Execute | 3,5 | — |
| Kaelon | Q | Anchor Strike | Projektil 80°, Stun | 3,4 | — |
| Kaelon | W | Lumen Wall | Self, Schild | 0 | — |
| Kaelon | E | Gravity Anchor | Platzierbarer AOE-Kreis | 5 | 2,6 |
| Kaelon | Ult | Kest Citadel | Selbst-AOE, Verbündetenschild | 0 | 3,8 |
| Nyra | Q | Prism Shot | Strahl/Linie, 45° Zielhilfe | 7,8 | — |
| Nyra | W | Vector Shift | Dash-Richtung | 3 | — |
| Nyra | E | Light Snare | Projektil 45° | 6,8 | — |
| Nyra | Ult | Sightline Break | Drei Projektile 45° | 7,8 | — |
| Elyra | Q | Orbit Fracture | Platzierbarer AOE-Kreis | 7,3 | 2 |
| Elyra | W | Invert Field | Platzierbarer AOE-Kreis | 6,5 | 2,6 |
| Elyra | E | Phase Fold | Dash-Richtung | 1,8 | — |
| Elyra | Ult | Impossible Center | Platzierbarer AOE-Kreis | 7 | 3,5 |
| Mira | Q | Resonant Seed | Projektil 45° | 6,5 | — |
| Mira | W | Growth Veil | Gerichtete Verbündetenauswahl, Heal | 6,5 | — |
| Mira | E | Venn Shelter | Gerichtete Verbündetenauswahl, Schild | 6,5 | — |
| Mira | Ult | Wildsurge | Selbst-AOE, Heal und Schild | 0 | 5 |
| Rook | Q | Wreck Swing | Nahkampf 80° | 2,9 | — |
| Rook | W | Armor Grip | Self, Heal und Schild | 0 | — |
| Rook | E | Wreckstorm | Selbst-AOE | 0 | 2,8 |
| Rook | Ult | Orun Overdrive | Selbst-AOE, eigener Schild | 0 | 3,2 |
| Traveler | Q | Temporal Beam | Projektil 45° | 7,2 | — |
| Traveler | W | Rift Step | Dash-Richtung | 3,6 | — |
| Traveler | E | Chrono Snare | Platzierbarer AOE-Kreis | 6,5 | 2,2 |
| Traveler | Ult | Paradox Collapse | Platzierbarer AOE-Kreis | 6,8 | 3,2 |
| Nerok | Q | Anchor Recall | Projektil 80°, Pull nach Kontakt | 6,2 | — |
| Nerok | W | Pressure Hull | Self, Schild und Speed | 0 | — |
| Nerok | E | Breaker Wave | Selbst-AOE | 0 | 2,8 |
| Nerok | Ult | Abyssal Maelstrom | Platzierbarer persistenter AOE-Kreis | 6 | 3,2 |

Temporal Beam behält trotz seines Namens die bestehende Orb-VFX mit Flugzeit. Sivras Q war bereits als Laser dargestellt und verwendet die Linienprüfung. Utility- und Mobilitätseffekte bleiben speziell behandelt; sie werden nicht in offensive Geschosse umgewandelt.

## Zentrale Balancing-Werte

Das Unity-Asset liegt unter `Assets/Alterverse/Resources/Combat/AimingSettings.asset`; die gemeinsame Definition ist `Combat/AimingSettings.cs`. Keine manuelle Szenenverkabelung ist erforderlich. Ohne Asset erzeugt die Komponente dieselben Defaults.

| Parameter | Ausgangswert / Bedeutung |
| --- | --- |
| `meleeConeAngle` / `rangedConeAngle` | 80° / 45°, volle Öffnung |
| `aimAssistAngle` | 8° maximale Skill-Zielkorrektur relativ zur unveränderten Daumen-/Tap-Richtung; 0 deaktiviert die Hilfe. Die wiederhergestellte automatische Standardangriffs-Zielwahl verwendet die vorhandenen Soft-Target-Regeln unabhängig davon. |
| `castRange` / `aoeRadius` | Aus bestehenden Katalogen bzw. aktuellem Grundangriffsprofil; separat pro Attacke überschreibbar |
| `projectileWidth` | 0,30, voller Geschossdurchmesser; Hero-/Minion-Körperradius bleibt 0,40 |
| Projektilgeschwindigkeit | Nyra 38, Traveler 32, Elyra 23, übrige 27 Einheiten/s |
| Nerok Q | Geschwindigkeit 18, Breite 0,45 |
| Kaelon Q | Geschwindigkeit 14, Breite 0,86 |
| Veyr W | Geschwindigkeit 18, Breite 0,45 |
| `dragRadius` / `dragDeadZone` | 96 / 10 logische HUD-Pixel, über Safe-Area-Skalierung auf Displaygröße angepasst |
| `tapSeconds` | 0,20 s ohne Überschreiten der Drag-Deadzone |
| Tap-AOE-Distanz ohne Zielhilfe | 65 % der Cast-Reichweite in Blickrichtung; beim Zurückziehen nach Drag kann der Mittelpunkt direkt auf dem Spieler liegen |
| `castOnRelease` / `allowTapCast` | Standardmäßig beide `true`; Release-/Immediate-Cast und Tap-Sperre für Skill-Gesten. Standardangriffe starten direkt bei Press/Hold und verwenden `allowTapCast`. |
| `deliveryCapacity` | 16 gleichzeitig vorbereitete/fliegende Angriffe pro Figur, begrenzt auf 8–64; volle Queue verwirft den Cast vor dessen Cooldown |
| `previewColor` | Hellgelb `(1, 0.94, 0.56, 0.22)`, transparenter Fill mit klarer Außenkante |
| `previewOpacity` | 0,75: gesamtes Zielvisier einschließlich Kreis-/Dash-Vorschauen, aller Konturen und Soft-Target-Marker hat nochmals 25 % weniger Deckkraft als zuvor. |
| `conePreviewOpacity` | 0,75: die vorherige zusätzliche Reduktion für Kegelfläche, Outlines und Projektil-/Strahllinie bleibt erhalten. Zusammen mit `previewOpacity` ergeben sich 0,5625 der ursprünglichen Deckkraft. |
| Standardangriff-Soft-Target | Vorhandene Heldenpriorität und Richtungs-/Entfernungswertung; `SoftTargeting.stickSeconds` bleibt 1,25 s und wird bei erfolgreichen offensiven Aktionen verlängert. Ziel muss gültig und in Reichweite sein. |
| `aimType` / `targetingMode` | Getrennte Vorschau-/Geometrieklasse und explizite Zielreferenzberechtigung |

`overrides` enthält vollständige `AimSpec`-Einträge mit `HeroId` und Slot: **-1** = Grundangriff, **0/1/2/3** = Q/W/E/Ult. Der HUD-Button **4** startet den direkten Standardangriff und gehört nicht zur manuellen Skill-Geste. Beim Einrichten eines Overrides zuerst den vorhandenen Spec übernehmen und dann die gewünschten Felder ändern; ein neuer leerer Spec ersetzt sonst auch Reichweite, Radius und boolesche Einstellungen. Die geometrische Form und die spezielle Utility-Mechanik eines bestehenden Skills sollten zusammenpassen. Ein `AreaCone`-Override eignet sich für offensive AOE-Skills; Heil-/Schild-/Dash-Spezialfälle bleiben ihre jeweilige Mechanik.

`HeroAbilityKit`, `HeroCombatProfile` und `HeroIdentity` erhalten keine Änderungen an Schaden, Cooldowns, Reichweiten oder Basiswerten. Die neue Projektillogik ersetzt die bisherige zeitgesteuerte Treffergarantie bewusst durch tatsächliche Flugstrecke und Kontakte. Die neuen Trefferbreiten und mobil gewählten Kegel benötigen Balancing im Spiel.

## Multiplayer

`CoopGameplayCommands` verwendet `DF_COOP_BASIC_V4` / `DF_COOP_ABILITY_V4`. Es sendet Richtung und Bodenpunkt; eine Zielreferenz wird nur für Support oder explizites Homing benötigt. Der Host prüft Absender, Actor, Slot, endliche Koordinaten, Zustand und Cooldown und begrenzt die Bodenposition auf die erlaubte Reichweite. Er erwirbt für normale Angriffe kein neues Soft Target.

Client und Server müssen gemeinsam aktualisiert werden: V3-Clients können die neuen V4-Casts nicht ausführen. Der bestehende Cooldown-Sync korrigiert Client-Vorhersagen nach abgewiesenen Anforderungen. Ein tatsächlicher Zwei-Geräte-Test auf Latenz und VFX-Sync bleibt erforderlich.

## Tests und verbleibende Geräteprüfung

`Tools/Aiming/Regression/run.py` kompiliert die unveränderten produktiven Combat-, Kit-, Input-, Preview- und Command-Quellen mit deterministischen Unity-/NGO-Fixtures. Ergebnis: **584 Assertions bestanden**, zusätzlich beide bestehenden Editor-Regressionsläufe für Grundangriffe und All-Hero-Delivery. Abgedeckt sind alle acht Grundangriffe und 32 Skills, Winkel/Reichweiten, begrenzter Skill-Assist, Windup-/Flug-Dodge, erste Kollision, Wände, AOE-Eintritt/Austritt, Hook, Volley, Support, explizites Homing, volle Queue, Stun/Tod/Disable/Pause, Hold/Drag/Release/Cancel/Tap, parallele linke/rechte Touches, Safe-Area-Koordinaten und Netzwerk-Richtung/Bodenposition. Zusätzlich geprüft: Grundangriff bei Press, cooldowngebundene Wiederholung, Halten ab bestehendem Cooldown, keine manuelle Grundangriffsgeste, kein Zusatzschuss beim Loslassen, Bewegung parallel, Unterbrechung durch Stun/Verlassen der Taste/verlorenen Touch, anschließendes Skill-Zielen sowie Maus-Press/Hold/Release. Wiederhergestellte Soft-Target-Auswahl für alle acht Helden geprüft: seitliche Ziele, Ziele hinter der Figur, Heldenpriorität vor näheren Minions, Sticky-Auswahl, Ausschluss von Verbündeten, Zielwechsel nach Tod, keine Ausführung ohne Ziel, automatischer Beginn beim Eintritt eines Gegners in Reichweite, weiterhin möglicher Windup-/Projektil-Dodge und übertragene feste Richtung beim Client-Standardangriff. Syntaxprüfung der vorhandenen lokalen C#-Quellen: keine Parse-Fehler.

Diese Fixture-Läufe sind **kein Unity-Build**. In dieser Umgebung stehen weder Unity Editor noch Android-Gerät zur Verfügung. Shader-Import, reale VFX/Animation/Audio, Framezeit, tatsächliche Android-Multitouch-Ereignisse und mobile Ergonomie sind noch nicht visuell oder am Gerät verifiziert.

Für die Abnahme in Unity 6000.6.1f1 und auf Android:

1. Je Held Standardangriff antippen und halten: automatische Soft-Target-Auswahl und sofortiger Start bei gültigem Ziel, Wiederholung nur nach Cooldown, kein Kegel, kein Steuern durch Ziehen und kein Zusatzschuss bei Release. Heldenpriorität, Sticky-Ziel und Zielverlust prüfen. Q/W/E/Ult halten, ziehen, loslassen; die Skill-Vorschau muss sofort verschwinden. Alle Visierflächen, Linien, Outlines und Soft-Target-Marker auf nochmals geringere Deckkraft prüfen; bestehende Attacken-VFX/Sounds müssen passen.
2. Seitlich vor und nach Projektilrelease ausweichen; im sichtbaren Zielkegel, aber außerhalb der schmalen Geschosslinie stehen; Nahkampfkegelgrenzen und Reichweiten prüfen.
3. AOE nach links/rechts, bis Maximalreichweite und auf eigene Position setzen; Gegner verlassen vor Windup-Ende die Fläche. Nerok-Wirbel betreten/verlassen und sechs Ticks kontrollieren.
4. Linken Joystick durchgehend bewegen, während der rechte Finger zielt, abbricht und schnell wechselnde Skills nutzt. Abbruch darf weder Angriff noch Cooldown auslösen.
5. Tap, Stun kurz vor Release, Stun während Windup, Stun nach Projektilrelease, Tod, Pause, Fokusverlust und Geräteausrichtung prüfen.
6. 16:9, breite Displays mit Aussparung und Hochformat auf korrekte Safe-Area, Erreichbarkeit der Abbruchzone, Markergröße und Lesbarkeit prüfen.
7. Host und Client mit derselben Version starten; Richtung, feste AOE-Position, Support-Auswahl und Cooldown-Korrektur unter Latenz vergleichen.
8. Auf Mittelklasse-Android Framezeit/GC prüfen: pro Preview höchstens zwei wiederverwendete Quads; keine per-frame Mesh-Erzeugung, kein per-frame Projektil-Objekt, begrenzte NonAlloc-Physikabfragen. Trefferprüfung bleibt proportional zu aktiven Geschossen und `Health.Active`.
