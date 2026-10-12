# BRAKK — Neubau P01

Stand: 12.10.2026. Auftraggeber: Chris Radic. Erstellung: KI-unterstützt in ChatGPT/Codex.

## Ergebnis und Status
Eigenständiger neuer Entwurf für einen Faust-Nahkämpfer. Kein altes Modell, kein Screenshot, keine fremde Charaktervorlage wurde als Eingabe verwendet. Der Name und die Rolle stammen aus dem Auftrag. Die alte Spielfigur wurde weder geprüft noch verändert.

P01 ist ein Konzept- und Modellierungsprototyp, keine fertige Spielfigur. 59 getrennte Meshes bilden Körper, Gesicht, Kleidung, Panzerung und Schlaghandschuhe ab. Das konzeptionelle Ziel ist ein detaillierter stilisierter Held; die OBJ-Dateien sind bewusst ein einfacher Volumenblockout. Konzeptbild und Blockout haben unterschiedliche Detailstände. Bei Abweichungen gilt die nummerierte Bauteilspezifikation hier; Maße sind Modellierungsziele.

## Eigenes Design
Brakk ist ein gedrungener erwachsener menschlicher Kämpfer mit breiten Schultern, kräftigen Unterarmen und tiefer Kampfhaltung. Zielhöhe einschließlich Haar: etwa 1,88 m. Schulterbreite mit Panzerung: etwa 0,95 m; Handschuhe verbreitern die Silhouette. Gesicht: breiter flacher Nasenrücken, trapezförmiger Kiefer, kurze asymmetrische dunkle Haare, freie Augen und freie Mundpartie. Kein Vollhelm.

Warme dunkle Haut, ockerfarbene gesteppte Arbeitsweste, petrolfarbenes Unterhemd und Hose, matte graphitfarbene Panzerung, bernsteinfarbene Dämpferelemente. Wiederkehrende Details: drei getrennte Knöchelplatten, diagonale Lüftungsrillen, versetzter Brustverschluss. Keine Logos, fremden Embleme oder Franchise-Vorgaben.

## Bauteile und Aufbauplan
Links/rechts beziehen sich auf den Charakter. Blender: Meter, Z nach oben, Gesicht nach -Y. Ursprung am Boden zwischen den Füßen.

| ID | Baugruppe | Aufbau von innen nach außen | Detailauftrag |
|---|---|---|---|
| 01 | Kopf und Gesicht | Kopfvolumen, Kiefer, Nase, Augen, Haar | Gesichtsloops um Augen/Mund; Lider, Ohrmuscheln, Lippen und kurze Haarsträhnen erst im Detailmodell |
| 02 | Offener Kopfschutz | Polster, Stirnbügel, zwei Wangenplatten | Augen und Mund frei; verdeckte Scharnierstifte, 4-mm-Fugen; separates Mesh |
| 03 | Unterhemd | Körper, petrolfarbenes Gewebe | Halsbündchen und Armlochsaum, gespannte Falten am Brustkorb |
| 04 | Kleidung Torso | Unterhemd, ockerfarbene Weste, Seitenriemen | Vorderhälften getrennt; Steppnähte, Saum, Rückenfalte; keine Schulterblockade |
| 05 | Brustpanzer | Westenträger, linke/rechte Platte, versetzter Verschluss | 6–10 mm optische Plattenstärke, Kantenfase, drei diagonale Rillen |
| 06 | Rückenpanzer | Textilpolster, Rückenplatte, Riemen | Rückenmitte frei beweglich; kurze Segmente statt starrer ganzer Wirbelsäule |
| 07 | Gürtel | Hüftgurt, Schnalle, kurze Seitenschlaufen | getrennte Schnalle; keine fremden Logos |
| 08 | Arme/Schultern | Oberarm, Polster, kurze Schulterkappe | zweiteilige Kappe; Freiraum bei 120° Armhebung |
| 09 | Unterarme | Ärmel, Ellbogenpolster, Unterarmträger | Ellbogenbeuge freihalten; Handschuh über beweglichen Ring anschließen |
| 10 | Faustwaffen Träger | Innenhandschuh, Handgelenkring, Handrücken- und Handflächenplatte | Griffhand anatomisch; Daumenkanal, Handflächenöffnung und Innenfutter ausmodellieren |
| 11 | Faustwaffen Schlagmodul | zwei seitliche Dämpfer, drei Knöchelsegmente, äußere Schale | sichtbare Stößel, bernsteinfarbene Puffer, verdeckte Befestigungen; fiktive Spielmechanik |
| 12 | Hose/Beine | Hüfte, Oberschenkel, Unterschenkel, Stoff | Knie- und Hüftfalten, Schrittreserve; mit Körper riggen |
| 13 | Oberschenkelpanzer | Gurt, seitliche kurze Platte | Becken und Knie nicht überbrücken |
| 14 | Knie | Textilpolster, schwimmende Knieschale | Patellaform, zwei Gurtanschlüsse; Beugeprobe |
| 15 | Schienbein | Stoff, schmale Frontplatte | dreiteilige optische Segmentierung; Knöchel frei |
| 16 | Stiefel | Fußkörper, Schaft, Zehenkappe, Sohle | flache breite Standfläche, Profil, Schnürung und Fersenstütze |

### Sprengreihenfolge der Faustwaffen
Hand → Innenhandschuh → Handgelenkring → Unterarmträger → Handflächenplatte → Handrückenschale → seitliche Dämpfer → drei getrennte Schlagsegmente. Erst die Hand modellieren, dann die Schalen anpassen. Die im Blockout stehenden Hände definieren Volumen und Einbauplatz; Finger sind noch nicht einzeln ausgearbeitet.

Zielmaße je Handschuh: rund 0,33 m breit, 0,36 m tief, 0,29 m hoch. Für das Detailmodell Schale an geschlossene Faust anpassen und Daumen separat schützen. Je Dämpfer Führungsgehäuse, Kolbenstange und Puffer als eigene Elemente anlegen. Keine technischen Belastbarkeitsangaben; dies ist eine fiktive Spielwaffe.

### Passung und Beweglichkeit
Abstände als Ausgangswerte: 8–15 mm zwischen Textil und Panzer, 20–30 mm in beweglichen Gelenkzonen. Bei Schulter, Hüfte, Ellbogen und Knie zuerst die Animation testen. Starre Rüstung an Bones binden; Kleidung und Körper gewichten. Sichtbare Schichten behalten, verdeckte Körperflächen erst nach gesichertem Outfit löschen.

## Dateien und Blender
- brakk_prototype.obj + brakk.mtl: zusammengesetzter modularer Blockout.
- brakk_exploded.obj + brakk.mtl: Baugruppen räumlich auseinandergezogen; keine physikalisch präzise Montagesimulation.
- parts.json: vollständige Geometrie und Materialzuordnung.
- build_brakk.py: reproduzierbare Erzeugung ohne externe Bibliotheken oder Assets.
- import_blender.py: erzeugt in Blender eine neue Collection mit Einzelmeshes, Materialien und Fasen. Vorhandene Szene wird nicht gelöscht.
- brakk_bauplan.svg: nummerierte Ansichten und Faustmodul-Schema.
- brakk_konzept.png: generiertes Konzept mit Einzelteilansichten. Seine automatisch erzeugten Ziffern sind keine verbindlichen Teile-IDs; die Zuordnung in diesem README und im SVG ist maßgeblich.
- PROMPT.txt: auf Brakk aktualisierte Designspezifikation. PROMPT_ORIGINAL.txt enthält den tatsächlich ausgeführten ersten Bildauftrag; PROMPT_CORRECTION.txt enthält den ausgeführten Titelkorrekturauftrag.
- RIGHTS_AND_PROVENANCE.md und SHA256SUMS: Herkunft, Rechtebasis und Integritätsnachweise.

Blender: OBJ mit Z-Up und -Y Forward importieren. Alternativ parts.json und import_blender.py zusammen speichern, Script im Texteditor öffnen und ausführen; falls __file__ dort fehlt, ROOT explizit auf den entpackten Ordner setzen. Danach als .blend speichern. Der Blender-Import wurde hier nicht ausgeführt, weil Blender nicht installiert ist.

## Umsetzung bis zur Spielfigur
1. Silhouette im Spielmaßstab abnehmen, Kopf und Faustmechanik detailliert ausarbeiten.
2. Highpoly-Sculpt; separate Fasen, Nähte, Falten, Schrauben und Dämpfer.
3. Retopologie: Zielbereich zunächst 25–45k Dreiecke für LOD0, 12–20k LOD1, 5–8k LOD2; erst im konkreten Zielgerät finalisieren.
4. UVs und PBR-Bakes: Körper/Kleidung und Rüstung/Fäuste in zwei Materialsets, zunächst je 2k; mobile Variante ggf. 1k. Keine fremden Texturen ohne Quellenbeleg.
5. Humanoides Rig mit Fingerbones; Faustmodule an Hand-/Unterarmbones, bewegliche Dämpfer an Hilfsbones.
6. Idle, Lauf, Faust links/rechts, Kombo, Treffer, Tod. Beugeproben bei 90°/120°, Faust vor Gesicht, Hocke und Lauf.
7. Export FBX oder GLB gemäß Spielpipeline; Materialien, Maßstab, Bounds, Kollisionskörper und Performance prüfen.
8. Erst nach diesen Prüfungen im Spiel den alten Brakk ersetzen. P01 verändert keine bestehenden Spielassets.

## Bereits geprüft
Python-Generator ausgeführt. OBJ-Indizes, geschlossene Einzelmesh-Kanten, endliche Koordinaten und reproduzierbare Erzeugung werden im Validierungsprotokoll festgehalten. Nicht geprüft: Blender-Ausführung, Unity-Import, Animation, Performance, Namen-/Markenrecherche oder rechtliche Einzelfallfreigabe.
