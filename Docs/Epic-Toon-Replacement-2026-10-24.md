# Epic Toon FX: individuelle Angriffe und verbindliche Größen

Bibliotheksversion **6** erzeugt 170 Vorlagen: 51 gekaufte Quellfamilien und 119 individuelle Haupt-, Vorbereitungs- und Treffervarianten für 40 Aktionen von acht Helden. Nyras exakter Q-Strahl bleibt Runtime-Geometrie. Neroks komplette Originalgestaltung und Timmys Klauen/Staub bleiben erhalten. Auf den neuesten Nutzerwunsch ersetzen zwei gekaufte Flamethrower Timmys frühere gestapelten Düsen-Layer. Die bevorzugten alten Schildkörper bleiben bestehen; Schildwerte werden bereits unter den Lebensbalken angezeigt.

## Größen und Treffer

Die frühere pauschale Vergrößerung ist für Angriffe aufgehoben. Der Editor misst die Bounds jeder konvertierten Gesamtkomposition einschließlich versetzter Kopien und Schweife. Zur Laufzeit wird diese Größe unabhängig von alten Mesh-Skalierungen auf den tatsächlichen Angriff begrenzt:

- Nahkampf liest echte Basisreichweite beziehungsweise AimSpec-Reichweite und Kegelwinkel. Der Shader begrenzt die sichtbaren Fragmente zusätzlich auf diesen Kegel. Der Hiebschweif kann nicht mehr über den Bildschirm reichen.
- Geschosse verwenden die tatsächliche Geschossbreite. Schweife liegen entlang des bereits zurückgelegten Flugsegments und bleiben innerhalb der maximalen Reichweite.
- Flächenfähigkeiten lesen ihren tatsächlichen Radius. Ultimates erhalten reichere Kompositionen und nutzen mehr ihrer vorhandenen Fläche, ohne den Radius zu erhöhen.
- Portal-Sprungeffekte wachsen von 0,85 auf 1,275 Welteinheiten Radius (+50 Prozent). Andere Portale werden innerhalb ihrer vorhandenen Wirkungsfläche stärker ausgefüllt; der echte Radius bleibt die Obergrenze.
- Treffer an einzelnen Gegnern sind höchstens 0,18 m Radius, bei Ultimates 0,24 m, mit reduzierter Deckkraft und höchstens 18 Partikeln. Mini-Schwertkontakte, leichte gerichtete Treffer und kleine Magie-/Blitzfunken ersetzen die großen Trefferausbrüche. Die zusätzliche alte PlayerHit-Sichel und der opake Comic-Stern werden bei umgestellten Heldenangriffen nicht darübergelegt. Nichthelden verwenden einen ruhigen Mini-Kontakt.
- Keine `2D Alternate`-Vorlagen. Text-Texturen wie `text_*`, `pow`, `swoosh` und `crack` werden in konvertierten Kopien deaktiviert. Physische Bodenrisse bleiben erlaubt.

Der native Renderer ersetzt den alten kosmetischen Renderer exklusiv. Bei leerer/ungeeigneter nativer Emission bleibt ein einzelnes Ersatz-Mesh. Warnränder, Start, Flugweg, Kontakte, Lebensdauer und Abbruch behalten ihre bestehenden Besitzer und Zeitpunkte. Es werden keine zusätzlichen Schadensereignisse oder Netzwerkpakete erzeugt.

## Konkrete Zuordnung

| Einsatz | Vorlage / Gestaltung |
|---|---|
| Bombenflugzeug | `NukeConeExplosionFire`: gelb-orange Feuerfontäne, brauner Rauch, ursprüngliche Palette. Ein Effekt am unveränderten 3,24-m-Radius; der frühere Zusatzring entfällt. Meteore behalten ihre eigene bestehende Darstellung. |
| Traveler Standard / Q | `LightningMissileYellow`: kompakter einzelner Zeitblitz beziehungsweise parallele Blitzform mit Mündungsakzent. Flugmechanik bleibt erhalten. |
| Traveler Sprung | `SpinPortalGold` an Absprung und tatsächlicher Landung. |
| Traveler E / R | Zeitacht mit Lightning-Nova beziehungsweise Lightning-Nova-Kollaps mit Wirbelkern. |
| Auren Ultimate | Magischer Explosionskranz mit Runensiegeln innerhalb Kest Citadel. |
| Ione Ultimate | Implosionswirbel mit mystischem Explosionskern. |
| Tahla Standard / Q | Fliegende Blattpartikel statt einer nur umgefärbten SharpMissile-Nadel. |
| Tahla Heilzauber | `HealOnceBurst` im Heilblütenmotiv am tatsächlichen Ziel. |
| Tahla Ultimate | Natureruption mit Blattakzent innerhalb Wildsurge. |
| Brakk Standard / E / R | Schwerer Hammerbogen, eigener rotierender Klingenring, Feuer-Nova-Eruption. Die Flächenauflösung verwendet jetzt das eigentliche Konzept statt einer allgemeinen Familie. |
| Heilplatte | Ruhiges `HealField`, begrenzt auf den vorhandenen Heilradius. |
| Heil-Pod Mitte | `HealOnceLoop` nur solange verfügbar; `HealNova` beim Aufnehmen; sofortiges Ende des Ruheeffekts beim Verbrauch. |
| Timmy Jetpack | Ein `FlamethrowerAdditiveBlue` je Düse, an den echten Sockeln befestigt, nach hinten gerichtet, mit unverändertem Sprung-/Nachglühzeitpunkt. Scratch behält seine drei Originalklauen. |

Die [40 Konzepte](Hero-VFX-Concepts-2026-10-24.md) und die [Runtime-Checkliste](Hero-VFX-Checklist-2026-10-24.md) führen jede Aktion einzeln.

## Standardangriff-Sounds

Nur passende Standardangriff-Bänke werden ergänzt. Auren/Brakk erhalten unterschiedliche Metallkontakte; Nyra einen Pistolenschuss, Ione einen mystischen Abschuss, Tahla einen Magieabschuss und Traveler einen Lightning-Abschuss. Im Paket gibt es keine passenden Schwert-/Klauenschwünge: Veyr, Void Knight, Timmy und Nerok behalten ihre vorhandenen Aufnahmen. Fähigkeitssounds bleiben erhalten. Cue-Indizes, Netzwerkverteilung und die bestehenden Auslösezeitpunkte bleiben gleich.

Die sechs ausgewählten WAV-Dateien wurden als PCM untersucht. Ihr hörbarer Signaleinsatz liegt bei 0–2,8 ms ab Dateianfang; kein zusätzlicher Startversatz ist erforderlich. Der Editor kopiert die Originalaufnahmen nach `Resources/Audio/EpicBasics`; falls eine Aufnahme fehlt, lädt die Laufzeit weiterhin den bisherigen Sound. Die Abhörprobe zusammen mit Animation und Gerät bleibt offen.

## Planetenwetter

| Planet | Dezente Umgebungslayer |
|---|---|
| Kryon | Leichter Schnee, kalter Randnebel |
| Erde | Leichter Regen, Windlinien |
| Green Planet / Trappist-1e | Leichter Regen, Randnebel |
| Mars | Trockener Wind, kein Regen |
| Noxara / Dark 00 | Dünner Randnebel |
| Pyraxis / WASP-76b | Warmer Wind und Dunst |
| Neptune | Bläulicher Wind und Dunst |
| Diamond Core | Windlinien |
| GJ504b / TOI-715b | Wind und dünner Dunst |
| Mond | Kein Niederschlag |

Höchstens zwei kamera-nahe Layer mit zusammen höchstens 84 Partikeln. Nebel sitzt am Rand des zentralen Kampfblicks. Wetter verwendet keine reservierten Angriffsslots, erzeugt keine Collider oder Lichter und wird bei reduziertem Dekorationsbudget entfernt. Die bisherigen Map-Dekorationen und Gameplay-Varianten bleiben erhalten.

## Einrichtung und native Prüfung

1. Vollständiges gekauftes Paket unter `Assets/Epic Toon FX/` behalten; der Commit enthält keine gekauften Binärassets.
2. Play Mode beenden, den Branch pullen und Unity kompilieren lassen. Eine veraltete Bibliothek baut automatisch als Version 6 neu auf. Manuell: `DIMENSIONS FRONT > VFX > Build Epic Toon replacement`.
3. Die Validierung prüft sämtliche Quellen/Phasen, Materialien und gemessenen Bounds. Linux-Dedicated-Server-Builds benötigen keine kosmetische Bibliothek.
4. Die native Vorschau erzeugt unter `Library/DF_EpicVfxReview/` Einzelbilder, `report.json`, `contact-sheet.png`, `index.html` und 15 gemeinsame Ansichten in **640 × 360**: acht Heldensignaturen gleichzeitig, Standard/Q/W/E/R, jeweils drei Zeitpunkte. Nyras Q ist Runtime-Geometrie und fehlt in der reinen Prefab-Ansicht.
5. Diese gemeinsamen Bilder prüfen native Kompositionen, ersetzen aber keinen Kampf aus der tatsächlichen Spielkamera. Erkennbarkeit, Überdeckung, Soundabgleich und Android-Bildrate bleiben in der Einzelcheckliste offen.

Hier ist kein nativer Unity-Editor verfügbar. Lokale C#-/Adapter-Prüfungen bestätigen Ablauf, Grenzen und Gameplay-Parität; sie sind keine ästhetische oder mobile Abnahme. Schadenswerte, Heilwerte, Reichweiten, Cooldowns, Bewegung, Kollisionslogik und Netzwerkformate werden nicht verändert.

## Lokale Verifikation

- Runtime-/Netzwerk-/VFX-Suite: 9.767 Prüfungen bestanden, einschließlich des Größenschutzes für nicht an einen Helden gebundene Vorlagen.
- Editor-Konvertierung: 367 Prüfungen mit kontrollierten Unity-Adaptern, einschließlich aller 170 Einträge, Text-Layer-Entfernung und alter Bibliotheksversionen.
- Aiming-/Skill-Entry: 1.332 Prüfungen, einschließlich Timmys neuer Düsenanzahl und unveränderter Bewegung/Schadensauflösung.
- C#-Syntaxprüfung sämtlicher lokal geladener Produktionsdateien: keine Fehler.
