# Nerok – eigene Rüstung, ursprüngliche Figur erhalten

Auftraggeber: Chris Radic. Erstellung: 12.10.2026, ChatGPT-unterstützte Modellierung in Blender 4.0.2.

## Verbindlicher Umfang

Der Nutzer begrenzte den ursprünglichen Gesamtumbau am 12.10.2026 auf Kleidung und ergänzte: „Gebe ihm eine von dir angefertigte Detailierte Rüstung“. Kopf, Gesicht, Körper, Waffen, Enterhaken und Bewegungen sollen dem Stand von gestern entsprechen. Der neue vollständige Haifischentwurf wird nicht in das Spiel übernommen.

Die Umsetzung ergänzt ausschließlich eine getrennte Rüstungsschicht. CyberShark/Model.bytes, AnchorProjectile.bytes, AnchorChain.bytes, SharkSkin.shader, JacketFabric.shader, alte Blender-Figur, Angriffsicons, Sounds und Animationsdateien werden in dieser Änderung nicht ersetzt. Die bisherige Kleidung bleibt als Grundlage erhalten. Es entsteht kein neuer Helm, Kopf oder Waffenmodell.

Bestätigter Ausgangsstand: Lowperformence/Alterverse, dev/vertical-slice, Commit 3dc8d779893c33ce0826c0cc1fa21eefcf4c294b vom 11.10.2026. Der spätere Ausgangscommit 2b6898b91bf71623fe18180344b87eafa5c5ebb0 ändert nur Ranked-Dateien; Neroks Dateien sind gegenüber dem gestrigen Stand unverändert. Git-Blob-ID des vorhandenen Model.bytes: 9688d9805bdabe41e5890a8a26e60f042b760c5e. Diese ID belegt die Repository-Version, nicht eine neue Lizenz.

## Neues Design und Bauteile

88 einzeln benannte Meshteile bilden eine maritime Rüstung aus dunklem Stahl, graugrünen Keramikeinlagen, gebürstetem Kupfer und braunen Lederriemen. Jeder neue Teil ist im parts.json aufgeführt. Kantenrahmen und Einlagen sind echte getrennte Geometrie; Nieten, Kupferrippen, Schultergurte und Schnallen sind modelliert.

| Gruppe | Form und Details | Anschluss |
| --- | --- | --- |
| Brust | Vier überlappende Kielsegmente mit Stahlrahmen, Keramikeinlagen, Kupferkanten und Nieten | Chest |
| Seiten/Rücken | Geteilte Platten und Kupferleisten; Freiraum um bestehende Flosse und Schwanz | Chest |
| Schultern | Drei gestaffelte Segmente je Seite mit Einlagen und eigenen Nieten | UpperArmL/R |
| Armschienen | Gefasste Keramikflächen, zwei Kupferrippen und umlaufende Lederriemen | ForearmL/R |
| Hüftschutz | Zwei getrennte Tassets, damit Becken und Schwanz frei bleiben | ThighL/R |
| Beinschienen | Zwei überlappende Schalen je Bein mit Einlagen und Kupfernieten | ShinL/R |

Der exportierte Rüstungsmesh umfasst 6.584 Vertices und 12.816 Dreiecke. Es werden keine Vertices an Head, Hand oder WeaponGrip gebunden. Das Spiel fügt einen eigenen SkinnedMeshRenderer hinzu und verwendet die bestehenden Knochen und Formanpassungen. Körpermesh, Waffenrenderer und Animationslogik bleiben im bisherigen Code erhalten. Die Ergänzung besteht aus einem Aufruf und einer neuen Methode in CyberSharkAppearance.cs.

## Dateien und Reproduzierbarkeit

- ArtSource/Heroes/NerokArmor/NerokArmor.blend: bearbeitbare Teile, eigene prozedurale Materialien, UVs und Armatur mit bestehenden Anschlusskoordinaten.
- NerokArmor.fbx: Rüstung und Armatur für Austausch/Weiterbearbeitung; ohne eigene Bewegungsclips.
- Assets/Alterverse/Resources/CyberShark/Armor20261012.bytes: eigenständige komprimierte Rüstungsdaten.
- Tools/NerokArmor/build_armor.py: vollständige deterministische Formdefinition und Export.
- render_armor.py: Software-Renderer der tatsächlichen Rüstungsvertices mit Vorder-, Rück-, Dreiviertel- und aufgefächerter Baugruppenansicht. Dies sind Quellenmodellansichten, keine Unity-Screenshots. Die Spielanpassung auf den vorhandenen Rig erfolgt beim Laden.
- parts.json, build-report.json und Prüfberichte: Bauteilregister und konkrete Prüfergebnisse.
- SHA256SUMS.txt: Integritätswerte der neuen Dateien; kein unabhängiger Zeitstempel.

Aus Repository-Root:

    blender -b --python Tools/NerokArmor/build_armor.py
    blender -b --python Tools/NerokArmor/validate_blender.py
    python3 Tools/NerokArmor/render_armor.py

Blender benötigt seine FBX-Erweiterung und NumPy; der Software-Renderer benötigt NumPy und Pillow. Es gibt keine externe Texturdatei.

## Herkunftskette und Rechtebasis

Nutzerauftrag → eigens festgelegtes Rüstungsdesign → ChatGPT-unterstützt erzeugter Modellcode → Blender-Meshteile → Blender-/FBX-Quelle → eigenes Spielresource → begrenzte Spielanbindung.

Neue sichtbare Rüstungsgeometrie und Materialdefinitionen werden ausschließlich durch den beigefügten Code erzeugt. Es werden keine Stockmodelle, fremden Rüstungsbilder, Logos, heruntergeladenen Texturen oder vorhandenen Haifischkörper importiert. Die Modellierungsfunktionen stammen aus dem vorherigen, in dieser Sitzung neu geschriebenen Generator; sie werden für Rüstungsteile wiederverwendet. Die vorhandenen Knochenkoordinaten stammen aus dem eigenen Projektvertrag (Tools/UmbralMage/build_umbral_mage.py). Das vorhandene Spielrig ist eine getrennte Abhängigkeit und wird dadurch nicht neu lizenziert.

Für diese Rüstung wurde keine neue KI-Bildgeneration verwendet. Vollständige technische Gestaltungsvorgaben stehen im Modellcode: Maßpositionen, Schichtungen, Umrisse, Kantenbreiten, Materialfarben und Zuordnung zu Knochen. Maßgebliche Nutzereingaben sind die beiden oben dokumentierten Umfangsvorgaben. Die vorherigen Gesamtcharakter-Konzeptbilder sind verworfene Vorschläge und keine Bildvorlage des erhaltenen Kopfes oder Körpers.

Offizielle vertragliche Rechtequelle: https://openai.com/de-DE/policies/eu-terms-of-use/, geprüft am 12.10.2026, Seitenstand 16.01.2026, Abschnitte Content / Ownership of content / Similarity of content. Im Verhältnis zu OpenAI weist sie dem Nutzer Rechte am Output zu, soweit gesetzlich zulässig. Sie garantiert keine Einzigartigkeit oder Freiheit von Rechten Dritter. Dieses Dokument stellt keine amtliche Lizenzurkunde aus und behauptet weder CC0 noch eine fremde Lizenz für die neu erzeugten Dateien. Die bestehende Figur und andere weiterverwendete Projektassets benötigen weiterhin ihre eigene Herkunftsdokumentation. Eine Marken- oder juristische Ähnlichkeitsprüfung wurde nicht durchgeführt.

## Prüfung und tatsächlicher Status

Geometrieprüfung: endliche Koordinaten, Einheitsnormalen, gültige Indizes, nicht kollabierte Dreiecke, ausschließlich Rüstungsbones und getrennte Resource. Nach Entfernen der zusätzlichen Rüstungsmethode und ihres Aufrufs entspricht CyberSharkAppearance.cs bytegenau dem zuvor gelesenen Ausgangscode. Kein vorhandenes Model.bytes wird mitgeliefert oder ersetzt.

Blender prüfte echte Armature-Verformung in 27 Konfigurationen und importierte den FBX anschließend erneut. UVs, Knochen, Teilanzahl und Gewichtssummen wurden kontrolliert. Die Prüfung kann keine vollständige Kollisionsfreiheit mit dem vorhandenen Körper nachweisen.

Unity 6000.6.1f1 ist in dieser Umgebung nicht verfügbar. Projektkompilierung, sichtbare Passform am alten Charakter, alle Spielanimationen, Materialkompatibilität, mobile Performance und Multiplayer wurden daher nicht in Unity geprüft. Die Rüstung liegt als gebauter und geprüfter Asset-/Einbaustand vor; die abschließende Spielabnahme ist offen. Vor Übernahme in den gemeinsamen Spielbranch die bestehende Nerok-Menüprüfung ausführen und Idle, Run, Attack, Skills, Hit, Death sowie die Flossen-/Schwanzfreiheit visuell prüfen.
