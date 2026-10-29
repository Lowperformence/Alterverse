# Void Knight V3 – Bauplan für Blender

Stand: 29. Oktober 2026 · Rüstung und Helm · Konzept für ein animiertes Spielmodell

## Entwurfsziel

Void Knight bleibt ein athletischer Schwertkämpfer zwischen Tank und Assassine. Die V3 vertieft seine vorhandene Identität: geschlossener schwarzer Helm, violettes V-Visier mit senkrechtem Mittelsteg, facettierter Brustkern, kräftige Schultern und eine schmale, bewegliche Körpermitte. Mehr Detail entsteht durch klar aufgebaute Schichten und funktional wirkende Anschlüsse.

Die zwei neuen Zeichnungstafeln zeigen die gesamte Rüstung mit Vorder- und Rückansicht sowie den Helm mit Front, Seite, Rückseite, Draufsicht und einer vergrößerten Zerlegung. Diese Anleitung legt die Bauteile und den Modellierablauf fest. Die Zeichnungen sind künstlerische Konstruktionsreferenzen; Maße, verdeckte Anschlüsse und Kollisionsfreiheit müssen beim Modellieren auf dem tatsächlichen Spielkörper abgestimmt werden.

## 1. Verbindliche Gestaltung

- **Silhouette:** geschlossene, überlappende Panzerung; breiter Schulterbereich; schlanke Taille; klar getrennte Ober- und Unterschenkel. Zusätzliche Details dürfen diese Grundform nicht verwischen.
- **Formensprache:** wiederkehrende V-Keile, gestufte Kanten, facettierte Einfassungen und nach hinten laufende Platten. Kleine Anbauteile folgen derselben Richtung wie die großen Flächen.
- **Drei Ebenen:** tragende dunkle Unterstruktur, darüber Panzerplatten, darin eingelassene Leuchtmodule. Unterstruktur und Dichtungen liegen sichtbar tiefer als die Außenhaut.
- **Schwarz bleibt dominant:** breite Flächen sind fast schwarz; Graphit und Titan erscheinen an Trägern und Fasen. Die hellen Reflexe der Zeichnungen nicht als silberne Grundlackierung übernehmen.
- **Licht:** V-Visier, Brustkern, Schultern, Unterarme, Schienbeine und ein schmaler Rückenstreifen. Kleine Gravuren überwiegend dunkel lassen.
- **Oberflächen:** gezielte Wartungsöffnungen, versenkte Schraubensitze, wenige sichtbare Haltezungen, feine Vertiefungen und leichte Abnutzung an exponierten Kanten.
- **Symmetrie:** die Grundgeometrie ist spiegelbar. Abweichende Kratzer und Zeichen entstehen später in den Texturen.

## 2. Maßsystem und Referenzpose

Die Höhe **H** ist die vorhandene Höhe des Spielcharakters einschließlich Helm. Körper, Rig und Anschlusspositionen des bestehenden Modells haben Vorrang. Für einen unabhängigen Prototyp kann H vorläufig auf 1,90 m gesetzt werden; das ist ein Entwurfsmaß, keine aus dem Bild gemessene Größe.

| Bereich | Startwert für den Blockout | Zweck |
| --- | --- | --- |
| Gesamtkörper | ungefähr 7,5 Kopfhöhen | vorhandene heroische Proportionen erhalten |
| Gepanzerte Schulterbreite | etwa 0,30–0,34 H | kräftig, aber noch beweglich |
| Außenhöhe des Helms | etwa 0,13–0,14 H | Kopf bleibt proportional zum Körper |
| Breite des Brustkerns | etwa 0,045–0,055 H | aus Spielentfernung erkennbarer Fokus |
| Stärke großer Außenplatten | zunächst 4–8 mm bei H = 1,90 m | erkennbare Ränder und Überlappungen |
| Stärke kleiner Abdeckungen | zunächst 2–4 mm | feinere Details ohne dicke Aufbauten |
| Sichtbare Hauptfasen | zunächst 1–3 mm | Lichtkanten für schwarze Flächen |

Die Werte werden zusammen skaliert und anschließend optisch angepasst. Bewegungsabstände entstehen aus den Animationen; ein gleichbleibender kleiner Spalt in der Ruhepose garantiert keine Kollisionsfreiheit.

Im vorhandenen Projekt direkt auf dessen Bindepose modellieren. Ohne vorhandenes Rig: symmetrische A-Pose mit leicht abgesenkten Armen und entspannten Händen verwenden. Blender-Arbeitskonvention für einen neuen Prototyp: Z nach oben, X links/rechts, Blickrichtung −Y. Beim Export die tatsächliche Spielkonvention berücksichtigen.

## 3. Rüstungsbaugruppen

Die A-Nummern entsprechen den Bezeichnungen der Rüstungstafel. Eine Baugruppe kann mehrere Objekte enthalten. Die Zahl der Objekte wird nicht mit der Zahl der sichtbaren Platten gleichgesetzt.

| ID | Baugruppe | Geometrie und Detailaufbau | Bewegungsprinzip |
| --- | --- | --- | --- |
| A00 | Basisanzug | Dunkles Trägermesh; dehnbare Gelenkfelder; Rippen und feines Gewebe hauptsächlich als Normal- und Roughnessdetail. | Deformierbares Körpermesh. |
| A01 | Helm | Eigenständige, austauschbare Baugruppe H01–H12. Halsöffnung an Kragen anpassen. | Starr am Kopf. |
| A02 | Kragen | Seitlich geteilte Halsfassung, vorderer Keil, hintere Abdeckung und dunkle innere Dichtung. | Am oberen Rumpf; Freiraum zum Kopf. |
| A03 | Brust | Tragende Innenschale, zwei spiegelbare Hauptplatten, separate untere V-Spitze, eingefasster Sitz für A17. Seitliche Anschlüsse unter den Schulterplatten. | Starr am Brustkorb. |
| A04 | Rücken | Getrennte Schulterblattpanzer, mittlerer Träger und gestufte Wirbelsäulenabdeckung; schmaler eingesetzter Lichtkanal. | Obere Platten am Brustkorb; untere Segmente an Rumpfbewegung anpassen. |
| A05 | Schulter | Aufhängungsbrücke, innere Schutzschale, Leuchteinsatz und drei gestufte Außenplatten pro Seite. Befestigungspunkte innen modellieren. | Eigene Schulterführung; Oberarmhebung separat prüfen. |
| A06 | Oberarm | Äußere Hauptschale, hintere Schließplatte, tiefer liegende seitliche Träger. Innenarm bleibt beweglich. | Hauptsächlich starr am Oberarm. |
| A07 | Ellbogen | Knöcherner Mittelkeil, seitliche Drehpunktabdeckungen und kurze hintere Lamelle. | Eigene Führung im Gelenkbereich; kein starrer Steg über die Armbeuge. |
| A08 | Unterarm | Rückenpanzer und innere Schließschale; herausnehmbarer violetter Einsatz mit eigener dunkler Fassung. Handgelenköffnung klar abschließen. | Starr am Unterarm. |
| A09 | Hand | Handrückenplatte, Knöchelkappen, kleine Fingersegmente und weicher Handschuh darunter. | Starre Teilplatten pro Hand-/Fingersegment; Handschuh deformiert. |
| A10 | Bauch | Drei separate V-Lamellen auf dunklem Träger. Überdeckung so planen, dass bei Vorbeuge keine Unterkante in die nächste Platte sticht. | Segmentierte Rumpfführung. |
| A11 | Gürtel | Beckengurt, zentrale Schlossplatte und seitliche Anschlussblöcke. Verdeckte Halter für die Hüftplatten. | Starr am Becken. |
| A12 | Hüfte | Vorderer Schutzkeil und unabhängige linke/rechte Hüftplatten, jeweils mit tiefer liegender Halterung. | Eigene Führung; Sitzposition, Sprint und hohe Schritte prüfen. |
| A13 | Oberschenkel | Breite Außenplatte, kleinere Vorderlamelle und kurze seitliche Abdeckung; keine geschlossene Metallröhre bis in die Leiste. | Starr am Oberschenkel. |
| A14 | Knie | Zentrale Knieschale, seitliche Gelenkkappen, kleine obere Anschlussplatte. Drehpunkt an das tatsächliche Rig anpassen. | Eigene Knieführung; Kniekehle bleibt frei. |
| A15 | Schienbein | Große vordere Schale, eingesetzter violetter Kanal, separate Wadenabdeckung und Knöchelabschluss. | Starr am Unterschenkel. |
| A16 | Stiefel | Zehenkappe, überlappende Ristsegmente, Fersenschale, Knöchelfassung und dunkle Sohle. | Zehen-/Fußbewegung berücksichtigen; Rist nicht ungeteilt überbrücken. |
| A17 | Brustkern | Facettierter violetter Kern, Außenfassung, dunkler Lichtträger und mechanische Rückhalterung. | Starr innerhalb A03; separat austauschbar. |
| A18 | Energiefugen | Eigenständige schmale Einsätze in vertieften Kanälen. Einfassung und dunkler Hintergrund verhindern überstrahlte Kanten. | Jeweils an der tragenden Panzerplatte befestigt. |

### Wiederverwendbarer Plattenaufbau

Jede große Panzerplatte besitzt eine klare Außenkontur, eine Hauptfläche, eine gerichtete Lichtfase und eine tiefer liegende innere Kante. Eine zweite, schmalere Stufe ist nur dort nötig, wo sie in den Referenzansichten sichtbar bleibt. Innenhalter sitzen an wenigen nachvollziehbaren Punkten. Feine Gravuren folgen dem Plattenverlauf und hören vor Schraubensitzen und Gelenkfugen auf.

Ein geeignetes Objektmuster ist beispielsweise `VK_A05_Pauldron_Main.L`, `VK_A05_Pauldron_Frame.L` und `VK_A05_Pauldron_Emitter.L`. Rechte Gegenstücke enden auf `.R`; zentrale Teile erhalten keinen Seitensuffix.

## 4. Helm im Detail

Die Außenform bleibt geschlossen. Der Ausdruck entsteht durch Stirnkeil, V-förmige Visieröffnung, Wangenwinkel und Kinnspitze. Das Visier ist ein eingesetztes Bauteil mit eigener Fassung. Der senkrechte Mittelsteg darf beim Beleuchten nicht zu einem breiten weißen Balken werden.

| ID | Bauteil | Aufbau und Vorgehen |
| --- | --- | --- |
| H01 | Innenhaube | Einfache gepolsterte Grundform mit freier Gesichtsöffnung; Gewebestruktur und Nähte als Textur. Dünne Dichtung zum Tragrahmen. |
| H02 | Tragrahmen | Durchgehende innere Schale mit Stirnauflage und seitlichen Anschlusssitzen. Nur im Sichtbereich detailliert ausarbeiten. |
| H03 | Kronenplatten | Drei nach hinten laufende Hauptplatten mit gestuften Rändern. Getrennte Geometrie für echte Überlappungen; feine Linien einbacken. |
| H04 | Stirnkeil | Symmetrischer Mittelkeil mit Doppelfase und vertieftem Kanal. Er definiert den Übergang vom Scheitel zum Visier. |
| H05 | Hinterkopf | Hintere Schale in wenige Segmente teilen; schmale Wartungsklappe und kleine eingelassene Lüftungsschlitze. |
| H06 | Schläfen | Spiegelbare seitliche Servicemodule mit flacher Ringfassung, geschützten Schraubensitzen und inneren Führungszapfen. |
| H07 | Wangen | Je Seite drei kurze überlappende Platten auf einem gemeinsamen inneren Steg. Die vordere Kante fasst den Gesichtspanzer. |
| H08 | Gesichtspanzer | Hauptgesichtsfläche zunächst als spiegelbare Halbschale bauen. Visieröffnung und senkrechten Schlitz sauber aussparen; danach Dicke und Kanten definieren. |
| H09 | V-Linse | Separater V-förmiger Einsatz mit schmaler senkrechter Fortsetzung. Leicht nach hinten versetzt; Außenrand sitzt unter der Fassung. |
| H10 | Lichtträger | Eigenes rückwärtiges Emissionsband mit dunkler Abschirmung. Nicht mit der Linse verschmelzen; Intensität unabhängig steuerbar halten. |
| H11 | Kinnschutz | Kleiner zentraler Schutz mit seitlichen Anschlüssen und vertieftem Unterseitenlüfter. Freiraum zum Halskragen bewahren. |
| H12 | Nackenlamellen | Drei kurze überlappende Rückplatten zwischen Hinterkopfschale und Halsöffnung. In der Rückansicht ablesbar; als eigene Bauteile anlegen, auch wenn ihr H12-Text in der Explosionsansicht fehlt. |

### Schichtenfolge des Visiers

Von außen nach innen: Fassung des Gesichtspanzers → dünne Linse → kleiner Abstand → Lichtträger → dunkle rückwärtige Abschirmung. Der umlaufende Sitz soll die Linsenkante verdecken, ohne die Leuchtkontur zu verschlucken.

Für Nahaufnahmen kann die Linse eine echte optische Tiefe erhalten. Für das Spiel ist eine opake Oberfläche mit gut abgestimmter Emission ein möglicher Ausgangspunkt; echte Transparenz erst einsetzen, wenn Darstellung und Kosten in der Ziel-Engine geprüft sind.

## 5. Was echte Geometrie bekommt

| Detail | Umsetzung |
| --- | --- |
| Außenkontur, Plattenüberlappung, große Aussparung | Echte Geometrie in allen relevanten Detailstufen. |
| Hauptfase und sichtbare Plattenstärke | Im Arbeitsmodell modellieren; im Spielmesh nach Kameradistanz erhalten oder vereinfacht backen. |
| Brustkern, Visier, große Energierille | Separates Bauteil mit eigener Form und Einfassung. |
| Sichtbare Gelenkkappe und große Befestigung | Echte Geometrie; kleine konzentrische Stufen gegebenenfalls backen. |
| Kleine Schraube, schmale Wartungsnut, feine Rune | Im Highpoly oder als Texturdetail erzeugen; ins Spielmesh backen, wenn die Kontur unverändert bleibt. |
| Gewebe, feine Riffelung, Mikrokratzer | Normal-, Roughness- und Farbtexturen. |
| Verdeckte Innenmechanik | Nur für sichtbare Öffnungen und Explosionsdarstellungen ausarbeiten. Im Spiel unnötige Flächen entfernen. |

Detail zuerst dort ausarbeiten, wo der Blick landet: Helm, Brustkern, Schulterkanten und Hände. Große ruhige Flächen an Brust, Rücken und Beinen geben den kleinen Details Kontrast.

## 6. Modellierablauf

1. **Körper und Rig prüfen.** Maße, Bindepose und vorhandene Animationen übernehmen. Helm, Schulterbreite und Fußkontakt auf den bestehenden Körper abstimmen.
2. **Blockout erstellen.** Alle A-Baugruppen mit einfachen Volumen anlegen. Vorder-, Seiten- und Rückansicht ohne Leuchteffekte prüfen. Silhouette vor Oberflächendetails festlegen.
3. **Bewegung früh prüfen.** Mit groben Platten Schwertgriff, Zweihandhaltung, Armhebung, Kniebeuge und Kopfneigung testen. Gelenklücken jetzt korrigieren.
4. **Helm als Leitbauteil ausarbeiten.** H02, H08 und H03 zuerst; anschließend H04–H07 und H11–H12. H09 und H10 erst nach Festlegung der Visieröffnung einpassen.
5. **Platten schichten.** Jeweils Träger, Hauptschale, sekundäre Abdeckung und Einsatz getrennt halten. Unnötig ineinandergreifende Kleinteile vermeiden.
6. **Dicke und Fasen definieren.** Bei flächig aufgebauten Schalen ist Mirror → Solidify → Bevel ein sinnvoller Start. Bereits geschlossene Volumenkörper benötigen keinen zusätzlichen Solidify-Schritt. Nicht einheitliche Objektskalierung vor der Dickenabstimmung bereinigen.
7. **Highpoly-Details ergänzen.** Gravuren, versenkte Sitze, Wartungsnuten und leichte Kantenabnutzung gezielt hinzufügen. Wiederkehrende Schrauben und Halter instanzieren.
8. **Spielmesh ableiten.** Hauptkonturen und Gelenköffnungen erhalten. Die Topologie an Krümmung und späterem Shading ausrichten. Starre ebene Flächen brauchen nicht künstlich überall dieselbe hohe Unterteilung.
9. **UVs und Texturen erstellen.** Vergleichbare Texeldichte für sichtbare Teile; mehr Platz für Helm und Brust. Gespiegelte UVs dürfen individuelle Zeichen nicht versehentlich spiegeln. Überlagerte Inseln für den Bake kontrolliert behandeln.
10. **Details backen und Materialien abstimmen.** Highpoly und Spielmesh eindeutig paaren; Projektionsabstände an Plattenkanten und Zwischenräumen prüfen. Normal-, AO- und Materialmasken im Zielrenderer kontrollieren.
11. **Rigging fertigstellen.** Große starre Platten über passende starre Knochenzuordnung oder Bone-Parenting führen, soweit die Exportpipeline das unterstützt. Weiche Bereiche gewichten; Schulter-, Bauch- und Hüftführung einzeln abstimmen.
12. **Im Spiel prüfen.** Silhouette, Emission, Texturlesbarkeit, Bewegung und Leistung an der tatsächlichen Kamera kontrollieren. Erst danach Detailstufen und Exportmaterialien finalisieren.

Für eine Explosionsdarstellung dieselben fertigen Bauteile auf einer getrennten Präsentationskopie verschieben. Das Referenzmodell für Rigging und Export bleibt korrekt zusammengesetzt.

## 7. Materialvorschlag

Die Werte sind Startwerte für die Gestaltung. Beleuchtung, Farbmanagement und Ziel-Engine entscheiden über das endgültige Aussehen.

| Material | sRGB-Farbidee | Metallisch | Roughness als Startwert | Einsatz |
| --- | --- | --- | --- | --- |
| Schwarze Keramik | `#12131B` | 0–0,15 | 0,32–0,48 | Breite Außenplatten. |
| Graphitmetall | `#2B2D3A` | 0,80–1,00 | 0,28–0,42 | Träger, innere Ränder und Gelenkkappen. |
| Dunkles Titan | `#667080` | 1,00 | 0,25–0,38 | Wenige Fasen, Verschlüsse und exponierte Metallstellen. |
| Flexibler Unteranzug | `#101116` | 0 | 0,60–0,80 | Gelenkfelder, Handschuhbasis und Dichtungen. |
| Amethyst | `#922DFF` | 0 | 0,12–0,25 | Kern und optische Einsätze. |
| Violettes Licht | `#B14CFF` | 0 | nach Oberfläche | Separat maskierte Emission; Stärke in der Engine abstimmen. |

Das Leuchten soll auch bei geringer Intensität eine klare Form haben. Materialunterschiede über Glanz, Roughness und Vertiefung zeigen. Kratzer sparsam verteilen; häufige helle Schäden würden die schwarze Rüstung unruhig und abgenutzt wirken lassen.

## 8. Detailstufen und Spieltauglichkeit

Ein hoch detailliertes Arbeitsmodell und ein effizientes Spielmodell entstehen aus derselben Gestaltung. Ein festes Polygon- oder Texturbudget ist ohne Kenntnis der aktuellen Szene und Zielgeräte nicht belastbar.

- **Highpoly:** alle sichtbaren Fasen, Nuten und Halter für Nahaufnahmen und Bakes ausarbeiten.
- **LOD0:** Außenkontur, zentrale Einsätze, Hauptüberlappungen und wichtige Gelenkdetails erhalten. Kleindetails überwiegend über Texturen darstellen.
- **LOD1:** kleine rückwärtige Abdeckungen und Ringstufen vereinfachen; Finger- und Stiefelsegmente zusammenfassen, soweit Animation und Silhouette das zulassen.
- **LOD2:** vor allem Körperform, V-Visier, Brustkern und große Leuchtkanäle erhalten.

Die benötigte Auflösung zunächst an Helm und Brust testen. Für die Materialentwicklung kann eine höhere Arbeitsauflösung sinnvoll sein; die exportierte Größe richtet sich nach tatsächlicher Bildschirmgröße und Speicherverbrauch. Beim Zusammenführen von Objekten Materialanzahl, Knochenzuordnung und Austauschbarkeit gemeinsam prüfen.

## 9. Vorgeschlagene Blender-Struktur

| Collection | Inhalt |
| --- | --- |
| `VK_REFERENCE` | Referenzbilder und Körpervorlage. |
| `VK_BLOCKOUT` | Frühe Volumen und Proportionskontrollen. |
| `VK_HIGHPOLY` | Detaillierte Quellbauteile. |
| `VK_GAME_LOD0` | Zusammengebautes Spielmesh. |
| `VK_GAME_LOD1` | Erste vereinfachte Detailstufe. |
| `VK_GAME_LOD2` | Entfernte Detailstufe. |
| `VK_RIG` | Armature und Steuerobjekte. |
| `VK_PRESENTATION` | Kopien für Explosionsansicht, Kameras und Licht. |

## 10. Abnahmekriterien

- Front, Seite und Rücken beschreiben dieselben Bauteile und Überlappungen.
- Der Helm sitzt in neutraler Haltung und bei Kopfbewegung frei über dem Kragen.
- Beide Hände können den vorgesehenen Schwertgriff schließen.
- Schulterplatten erlauben die vorgesehenen Angriffe, ohne in Helm oder Brust zu schneiden.
- Bauch- und Hüftplatten funktionieren bei Beuge, Drehung und hohem Schritt.
- Ellbogen und Knie haben keine starren Brücken über der Beugeseite.
- Visier, Brustkern und Leuchtkanäle bleiben bei normaler Spielkamera klar erkennbar.
- Die Rüstung ist auch mit ausgeschalteter Emission räumlich lesbar.
- Die schwarze Grundfarbe bleibt erhalten; helle Fasen ordnen die Flächen.
- Die finale Spielprüfung erfolgt mit den tatsächlichen Animationen und Zielgeräten.

## Werkzeugreferenzen

Offizielle Blender-Dokumentation zu den im Ablauf genannten Grundwerkzeugen:

- [Mirror – symmetrische Geometrie](https://docs.blender.org/manual/en/5.0/modeling/modifiers/generate/mirror.html)
- [Solidify – Schalendicke und Grenzen des Verfahrens](https://docs.blender.org/manual/en/4.5/modeling/modifiers/generate/solidify.html)
- [Bevel – nicht destruktive Kantenfasen](https://docs.blender.org/manual/en/3.2/modeling/modifiers/generate/bevel.html)

Die konkrete Modifier-Konfiguration richtet sich nach der installierten Blender-Version und dem jeweiligen Bauteil. Die Gestaltung, Objekttrennung und Maßverhältnisse sind Entwurfsentscheidungen für Void Knight V3.
