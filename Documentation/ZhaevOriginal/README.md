# Zhaev Null — eigener Neubau V1

Auftrag: Chris Radic, 12.10.2026. Spielzuordnung: Assassine Zhaev Null, interne ID Veyr. Ausgangsbranch für die Projektzuordnung: art/hero-concepts-provenance-20261012, c1c1185d64d3366ee4d000f291f8b4e26cdad4ea.

## Entwurf und Bauteile

Schlanker technischer Assassine, geschlossenes Gesicht, dunkles Gunmetal, violette Energie. Geteilte Brustpanzerung, drei bewegliche Bauchlamellen je Seite, Rückenmodule, kleine Schulterkappen, Vambraces, Kniekappen und Schienbeinschutz. Zwei eigenständig konstruierte asymmetrische Null-Klingen mit Griff, Knauf, Schutzbügel, Kern, Schneidfase und Energieleiter. Unter dem abnehmbaren Helm liegt ein separat modellierter Kopf mit Augen, Nase und Kiefer. Der Unteranzug besteht aus getrennten Rumpf-, Ärmel-, Handschuh-, Hosen- und Stiefelteilen.

104 identifizierte Bauteile, 5.068 Quellvertices und 9.720 Dreiecke inklusive des verdeckten Kopfes. 19 neu angelegte Knochen; Namen entsprechen der vorhandenen Engine-Schnittstelle. Standardmaterialien ohne heruntergeladene Texturen. Jedes Teil hat eine ID, Kategorie, Material und Knochenzuordnung in parts.json und im nativen Blender-Modell.

## Enthaltene Ergebnisse

- Zhaev.blend: modularer editierbarer Quellbau, einschließlich Kopf unter Helm und selbst gesetzter Animationsschlüssel.
- Veyr.fbx: drei zusammengeführte sichtbare Meshes (DF_Anatomy, DF_WeaponL/R), neues Rig, neun Clips. Verdeckter Kopf bleibt nur in der Quelldatei.
- Zhaev.obj / Zhaev.mtl: modulare Geometrie mit Objekt-IDs; dieses Format enthält kein Rig.
- Orthographic.svg: aus Meshvertices abgeleitete Ansichten.
- Assembly-exploded.svg: gemeinsame Sprengansicht mit versetzten Bauteilgruppen.
- Exploded-armor/weapon/head/helmet/clothes.svg: einzeln dargestellte Teile, IDs, Maße und Bindung. Einzelteile sind für Lesbarkeit verschieden skaliert; Maßangaben sind verbindlich.
- front.png / back.png: native Renderings des tatsächlich exportierten Modells.
- geometry-validation.json / native-validation.json / provenance.json: konkrete Prüfungen und SHA-256-Dateiwerte.

## Aufbau

Erst Root, Hüfte, Wirbelsäule, Brust und Kopf einsetzen. Dann linke/rechte Arm- und Beinketten montieren. Unteranzug, Handschuhe und Stiefel an die entsprechend benannten Knochen hängen. Panzerteile darüber setzen; Kopf und Helm getrennt behalten. Klingen starr an WeaponGripL/R, Griffmitte nahe HandL/R. Die Pose des FBX ist die Startpose; Bewegungssteuerung, Kamera und tatsächliche Bodenkontakte müssen in Unity geprüft werden. Arme und Beine bestehen aus starr gewichteten Segmenten, deren Fugen die Biegung ermöglichen; dies ist kein organisch glatt deformierender Haut-Sculpt.

## Herkunft und Rechteangaben

Die neue sichtbare Modellgeometrie wird ausschließlich aus hier gespeicherten Zahlen, Formfunktionen und Programmcode aufgebaut. Kein fremdes Mesh, keine fremde Textur, kein heruntergeladenes Rig und keine Motion-Capture-Datei werden im Builder gelesen. Frühere Veyr-Geometrie wird nicht importiert. Die bestehenden Namen und violette Klassenidentität werden zur Projektzuordnung verwendet. Modellbau und Dokumentation wurden KI-unterstützt in ChatGPT/Codex auf Nutzerauftrag erstellt. Das ist kein Beleg rein menschlicher Urheberschaft.

Dieses Paket enthält keine externen Charakter-Assets. Blender/bpy und die CI-Werkzeuge sind Bauwerkzeuge; ihre Softwarelizenzen werden nicht als Lizenz des erzeugten Modells ausgegeben. Alte Sounds, kosmetische Skins, Logos und übrige Spielassets sind nicht neu erstellt und nicht durch diesen Modellnachweis abgedeckt. Historische Nachweislücken bleiben separat bestehen. Eine Dokumentation kann die Erstellung nachvollziehbar machen; sie ist keine Garantie für Exklusivität, Schutzfähigkeit oder das Ausbleiben von Ansprüchen. Es wird keine erfundene Lizenzbescheinigung und kein erfundener Downloadbeleg beigelegt.

## Abnahmezustand

Python-Geometriechecks: ausgeführt. Native Blender-/FBX-Abnahme ist nur mit vorhandenem native-validation.json belegt. Unity-/Android-Abnahme: nicht ausgeführt. Nach erfolgreicher nativer Abnahme kopiert der Build das neue FBX in die Veyr-Resource auf diesem isolierten Art-Branch. Der Modellmarker DF_ZhaevOriginalV1 überspringt die alten zusätzlichen Helmstreifen, Detailgeometrie, Handvergrößerung, Stiefelersatz und Materialornamente. Bewegungssteuerung, Angriffslogik, Skins und externe Laufanimationen des Spiels bleiben bestehende Integrationsabhängigkeiten; die eigene Run-Animation im FBX ersetzt nicht automatisch die separate LocomotionAnimationLibrary. Die Integration ist vorbereitet, aber ohne Unity-/Android-Prüfung nicht als fertig getesteter Spielcharakter freigegeben.
