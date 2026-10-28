# Traveler Nocturne V01 — Baubeschreibung

## Eigene Formensprache

Ein verschlossener Reisender in einem schweren schwarzen Druckanzug. Gesicht vollständig verdeckt. Drei nach unten gestaffelte, leicht pfeilförmige Brustplatten; versetzte zweiteilige Helmbraue; links breitere kurze Lamellenschulter, rechts kompaktere Kappe. Zwei getrennte Umlaufgehäuse im Rücken. Schwarze Kugel mit sechs längs verlaufenden Schalen, innerem Kern, Äquatorring und zwei Polkappen. Keine farbigen Lichtstreifen, Fremdembleme oder Beschriftungen auf der Spielfigur.

Die Silhouette soll im Spiel aus mittlerer Entfernung lesbar bleiben: geschlossener Kopf, klare Schulter-Taille-Abstufung, freie Ellenbogen und Knie, schmale Armplatten, breite Stiefel. Schwarze Werkstoffe unterscheiden sich durch Rauheit und Reflexion statt durch bunte Akzente. Neutral weiße Beleuchtung für Formprüfung.

## Maßsystem

Y oben, Z vorne, X seitlich; Maße in Metern. Gesamthöhe der Figur ca. 2,02 m. Boden liegt am niedrigsten Sohlenpunkt bei Y=0,0045; beim finalen Export Root um diesen Betrag korrigieren. Prototyp ist eine neutrale Baupose mit leicht seitlich gestellten Armen.

| Baugruppe | Konstruktionsmaß / Aufbau |
|---|---|
| H01 Helmschale | Ellipsoid 0,290 × 0,380 × 0,286 m, Zentrum Y=1,835 |
| H02 Visier | 0,232 × 0,288 × 0,122 m, vor der Helmschale |
| H03 Halsanschluss | Zwei Dichtungsringe; 0,109 m Ringradius, 0,012 m Profilradius |
| T01 Brustpanzer | Drei einzelne geschlossene Polygonplatten, ca. 34 mm Bauvolumen inkl. Fase |
| T02 Druckanzug | Becken, Bauch, Brust und separate Arm-/Beinmanschetten |
| T02 Rückenbasis | 0,31 m breit, 0,36 m hoch, 0,10 m nominal tief |
| A01 Schulter | Links breiter, zwei zusätzliche Lamellen; rechts kompakte Kappe |
| A03 Unterarmplatte | 0,105 m breit, 0,18 m lang, 0,04 m Bauvolumen |
| L03 Knieschutz | 0,14 m breit, 0,125 m hoch, 0,034 m Bauvolumen |
| L05 Stiefel | Flache Sohlen, getrennte Zehenbänder und Außenkörper |
| W01 Kugelkern | 0,252 m Durchmesser, Zentrum (0,69; 1,24; 0,16) |
| W03 Kugelhülle | 0,320 m Außendurchmesser, 4 mm modellierte Wand, sechs Einzelkeile |
| W02 Äquatorring | 0,168 m Mittellinienradius, 7 mm Profilradius |

Panzerplatten besitzen modellierte Fasen. Wand-/Plattenmaße sind digitale Designmaße, keine geprüften Fertigungs- oder Schutzspezifikationen. Kugelpolöffnungen sind bewusst freigelassen. Nur W03 hat eine ausdrücklich modellierte Hohlwand; die Helmschale ist im Prototyp ein geschlossenes Volumen. Keine Behauptung physikalischer Druckdichtigkeit.

## Bauteilmontage / Explosionszeichnung

1. T02: Druckanzugvolumen aus Becken, Bauch, Brust, Ober-/Unterarmen, Ober-/Unterschenkeln zusammenstellen. Flexible Manschetten und Rippen an den Gelenken einsetzen.
2. T03: Hüftdichtung und separate Gürtelschließe montieren. Rückenbasis an Brustsegment, Umlaufgehäuse und Kühlschlitze separat aufsetzen.
3. T01: Brustplatten von oben nach unten mit klar sichtbaren Überlappungen ansetzen. Untere Platten für die spätere Rumpfdeformation separat halten.
4. H03/H01/H02: Halsanschluss, zwei Dichtungsringe, Helmkörper, separate Schläfenstücke und Stirnleisten; Visier von vorne einsetzen.
5. A01–A04: Schulterkappen, Oberarmplatte, Ellenbogendichtung, Unterarmplatte, Handgelenksmanschette, Handschuhkörper, vier Finger und Daumen. Schulter-Lamellen links separat halten.
6. L01–L05: Oberschenkelplatte, Kniedichtung und Knieschutz, Schienbeinplatte, Stiefelkörper, Sohlen und Zehenbänder.
7. W01–W03: Kugelkern, sechs Schalenkeile, Äquatorring und beide Polkragen. Kein Griffstiel. Später vollständige Waffe am `WeaponGripR` führen, `DF_Muzzle` exakt ins Kugelzentrum.

`parts.json` enthält sämtliche Einzelteile mit IDs und Explosionsvektoren. `traveler_exploded.obj` enthält genau dieselben Dreiecke wie das montierte OBJ; nur die Bauteilpositionen wurden um die dokumentierten Vektoren verschoben. Die sechs gerenderten Geometriepanels sind technische Prototypansichten. Der generierte Konzeptbogen zeigt eine künstlerische und detailliertere Richtung, keine maßgenaue CAD-Ableitung.

## Materialien

| Material | RGB, normiert | Metallanteil | Rauheit |
|---|---|---:|---:|
| PressureWeave | 0,018 / 0,018 / 0,018 | 0 | 0,88 |
| BlackCeramic | 0,035 / 0,035 / 0,035 | 0,25 | 0,34 |
| SealRubber | 0,009 / 0,009 / 0,009 | 0 | 0,95 |
| Obsidian | 0,012 / 0,012 / 0,012 | 0,65 | 0,13 |
| BlackMetal | 0,052 / 0,052 / 0,052 | 0,80 | 0,32 |

Keine externen Texturen; Materialien sind prozedural festgelegt. Gewebestruktur ist im Konzeptbild illustriert, im exportierten Prototyp noch kein Textur- oder Normaldetail.

## Qualitätsplan für das Spielmodell

- Helm mit echter Schaleninnenseite, sauber eingefasstem Visier, modellierten Anschlüssen und kontrollierten Übergängen verfeinern. Stoffvolumen und anatomischere Stiefel an den Konzeptbogen angleichen.
- Rumpf- und Schulterplatten gewichten oder an passende Bones binden; Rippenbereiche deformierbar neu topologisieren. 143 Bauteile sind authoring-freundlich, kein Ziel für 143 Laufzeit-Drawcalls.
- Atlas-UVs mit dokumentierter Texeldichte, selbst erstellte Normal-/Rauheitsdetails, eingeschlossene Bauteile bereinigen. Keine unnachgewiesenen Fremdtexturen hinzufügen.
- Body für Laufzeit sinnvoll zusammenfassen; Waffe separat als `DF_WeaponR` führen. Rig-Bones und exakte Restpositionen aus dem tatsächlichen Zielrig prüfen, nicht nur vorgeschlagene Namen aus `parts.json` übernehmen.
- Kugelposition an die finale Handpose anpassen, sichtbaren Schwebeabstand ohne Handüberschneidung sichern. Der Prototyp zeigt eine Designposition, noch keine validierte Griffpose.
- LOD0 ca. 25–40k Dreiecke, LOD1 ca. 12–20k, LOD2 ca. 5–8k als anfängliche Budgetziele. Tatsächliche Budgets anhand Android-Profiling festlegen; aktuelle 41.294 Dreiecke enthalten viele Dichtungsringe und Fingerteile.
- Nach FBX-Export Restpose, Gelenkdeformation, Lauf-, Angriffs- und Zauberbewegungen, Shop-Farbvarianten, schwarze Kugelmaterialien und Android-Lichtbedingungen prüfen.
