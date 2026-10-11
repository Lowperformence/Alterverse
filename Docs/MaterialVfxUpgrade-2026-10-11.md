# Material-, Effekt- und Bewegungsüberarbeitung

Die bestehenden Oberflächen erhalten gebackene Materialdetails, die gemeinsamen
Helden- und Monstereffekte unterscheiden ihre Oberflächen deutlicher, und die
Standpose nutzt eine mit Rigify und Rokoko erzeugte Atemkurve. Die Integration
nutzt Unity 6000.6.1f1 / URP 17.6.0 und die vorhandenen Darstellungswege.

## Eingesetzte Werkzeuge

| Werkzeug | Tatsächlich ausgeführte Arbeit |
|---|---|
| Blender 4.5.3 LTS / Node Wrangler 3.55 | PBR-Knoten und Texturverbindungen für neun Materialfamilien und die Effekttextur |
| TexTools 1.6.2 | Base-Color-, Roughness- und Tangent-Normal-Bakes aus gepolsterten High/Low-Flächen; zwei Roughness-Bakes für FlowNoise |
| Rigify | Steuerbares Referenz-Rig mit 706 Bones, animierte Bruststeuerung und Sampling der deformierten Bones |
| Rokoko 1.4.3 | Tatsächlicher Offline-Retarget-Bake auf Hips/Chest/Head; 49 Frames, Referenzpose von der exportierten additiven Kurve abgezogen |

Die getesteten offiziellen TexTools-/Rokoko-Quellstände und Archiv-Prüfsummen stehen
in `Tools/Visuals/blender-tools.json`. Rigify und Node Wrangler sind im getesteten
Blender enthalten. Auto-Rig Pro wurde nicht installiert oder gekauft. Rokokos
Live-Streaming und Kontobibliotheken sind nicht eingerichtet; der Offline-Bake
wurde erfolgreich ohne diese Funktionen ausgeführt.

Unity Particle System und URP gehören bereits zum Projekt. Ein zusätzliches VFX
Graph wurde nicht eingebunden: Die neue Darstellung verwendet die vorhandenen
Meshes und Shader, ohne Compute-Shader als neue Android-Voraussetzung.

## Abdeckung

| Bereich | Änderung |
|---|---|
| Glas / Timmy-Visier | Transparenter URP-PBR-Shader mit erhaltenen Reflexionslichtern, Texturtönung und Emission; Timmy behält exakt 0,50 Farb-Alpha |
| Metall / Waffen / Rüstungen | Gebackene Bürstenstruktur, Normaldetails und abgestimmte Rauheit in RuntimeLit, HeroSurface und metallischen Ornamenten |
| Stoff | Feine Webstruktur für RuntimeLit und Unteranzüge; importierte Modelle erhalten sekundäre Normaldetails |
| Stein, Beton, Erde, Sand | Neue gemeinsame Mikrotexturen; bestehende planetenspezifische Geologie und Farbpaletten bleiben die Basis |
| Rinde, Blätter, Eis | Neue Detaildaten in der vorhandenen Scenery-Pipeline |
| Helden- und Monster-VFX | Gemeinsame Flow-Details; Plasma, Schilde, organische Effekte, Nebel, Laser, Wasser, Portale und Trefferhitze haben eigene Oberflächenantworten |
| Leuchtende Waffen / Veyr-Spuren | Zurückhaltende Flow-Variation der Waffen; eigener Trail-Shader mit weicher Kante und hellem Kern |
| Helden-Standpose | Gebackene Atemkurve statt rein sinusförmiger Brustbewegung; vorhandene Begrenzung und Ausblendung beim Angriff/Dash bleiben maßgeblich. Sivras/Nyras ruhiger Rifle-Hold bleibt ausgenommen |

Alle 13 Arenen verwenden die vorhandene Materialprofil-Zuordnung. Sand-Mikrodetails
betreffen Moon/Mars/GJ504b, Erde/GreenPlanet/Trappist1e verwenden Soil, die übrigen
geologischen Profile Stone. Kryon und DiamondCore behalten ihre spezialisierten
Grundflächen; ihre vorhandenen Eis-/Fels-Dekorationen erhalten passende Finishes.
Die spezialisierten Wasser-, Kristall-, Haut- und Warnflächen-Verträge bleiben
maßgeblich. Das ist eine gemeinsame Grafiküberarbeitung, kein Ersatz sämtlicher
Modelle, Animation-Clips oder UI-Grafiken.

## Laufzeit und Daten

Neun Familien verwenden je eine lineare 256²-Datenkarte und eine importierte
Normalmap. RG speichert signierte Steigung, B Rauheit, A neutrale Helligkeit.
FlowNoise ist eine lineare, periodische 128²-RG-Datenkarte. Alle Karten wiederholen
sich, besitzen Mipmaps und sind auf der CPU nicht lesbar. Die unkomprimierte
RGBA8-Obergrenze einschließlich aller Mips beträgt zusammen **6,083 MiB**; diese
Zahl ist keine gemessene Android-Speicherbelegung.

RuntimeLit/HeroSurface verwenden drei triplanare Texturzugriffe und blenden zu feine
Details über den Pixelabstand aus. Planetendetails nutzen vorhandene Shader-Zugriffe.
Hochwertige gepoolte Energieeffekte erhalten zwei Flow-Zugriffe; Battery/Überlast
überspringen diese über die vorhandene Detailqualität. Es entstehen keine neuen
Effektslots, Lichter, Kamerakopien oder laufenden Texturbakes. Die bestehenden
192 Slots und Reserven für Kern-/Warn-Effekte bleiben erhalten.

Die Materialzuweisung nutzt die vorhandene Kompatibilitätsprüfung und cached deren
Ergebnis. Importierte PBR-Farb-, Normal- und Emissionskarten werden erhalten;
sekundäre Normalmaps ergänzen unbenutzte URP-Detailkanäle. Importerversion 18 löst
die Aktualisierung bereits importierter Helden aus. Editor-/Spielstart-Initialisierung
bindet die gemeinsamen Daten; fehlende Texturen ergeben einen neutralen Fallback.

## Reproduzieren

```sh
python Tools/Visuals/install_blender_addons.py --scripts-dir /path/to/blender-scripts
```

In Blender `node_wrangler`, `textools`, `rigify` und `rokoko` aktivieren. Bei einer
isolierten Installation die passenden `BLENDER_USER_SCRIPTS` und
`BLENDER_USER_CONFIG` setzen. Danach:

```sh
blender --background --python Tools/MaterialFinishes/build_finishes.py
blender --background --python Tools/Visuals/build_vfx_flow.py
blender --background --python Tools/HeroMotion/build_idle_breath.py
blender --background --python Tools/MaterialFinishes/render_review.py
```

Die beiden `.blend`-Quelldateien enthalten Material- und Bewegungsreferenzen.
`authoring-review.png` wurde in Blender gerendert und visuell geprüft. Es zeigt
Materialproben mit Studiolicht und ist kein Unity-Spiel-Screenshot.

## Ausgeführte Prüfung

| Prüfung | Ergebnis / Grenze |
|---|---|
| Gebackene Karten | 19 Karten; Größe, Nahtsprünge, normalisierte Normalen, Rauheit, lineare Import-/Mip-/Wiederholungs-Einstellungen und Hashes geprüft |
| Material-/Ressourcen-C# und Atemkurve | 2.042 Assertions auf Produktionscode mit Unity-API-Fixtures, einschließlich Caching, Texturerhalt, 0,50 Visier-Alpha, fehlenden Assets und periodischer begrenzter Bewegung |
| Forward-HLSL | 32 kompilierte und SPIR-V-validierte Stufen für acht betroffene Shader; Unity-/URP-Lichttypen wurden ersetzt |
| Shader-Assets / Preflight | 24 Projektshader, 60 Passes, lokale Includes und drei Materialreferenzen; 23 Preflight-Verhaltenstests |
| VFX-Pool | 9.346 Assertions auf Produktionsscheduler, Draw/Trace, Warnreserven, Kategorien, Alpha-/Dauerverträge und Qualitätsbudget; Meshfactory und Engine ersetzt |
| Combat/Aiming/Timmy | 2.609 Assertions; alle neun Basics, laufende Ziele, Warnflächen, Geschosskontakte und Timmy-VFX; Fixtures statt nativer Unity-Physik/GPU |
| Angriffe mit Produktionsbewegung | 5.252 Checks mit HeroMotor/HeroLocomotion; alle neun Helden bei 30/60/120 FPS, Rückwärtslaufen, Zielbindung, Ausrichtung und wiederholte Angriffe; Physik und Darstellung ersetzt |
| Guard-Arme | 239.865 Assertions / 16 Szenarien plus 24 Wrist-Szenarien; keine zusätzliche Schulter-/Wrist-Abweichung im Fixture |
| Timmy/WASP-Veröffentlichung | 203 Assertions auf Produktionskatalogen und Kauf-/Deploy-Gates; 1.900 / 900 Shards und Sperren bleiben erhalten |
| Syntax / Metadaten | C#-Syntaxbäume, eindeutige Unity-GUIDs und `git diff --check` bestanden |

Das Portable-VFX-Testprogramm verwendet jetzt Roslyn C# 9, passend zum bestehenden
Produktionscode. Der shopabhängige `AndromedaTimmyIntegration`-Editor-Menücheck wird
in Unity geprüft; die getrennte Shop-Regression testet die Produktionsfreigaben.

```sh
python Tools/MaterialFinishes/validate.py --native-bin /path/to/glslang-and-spirv \
  --mono-root /path/to/mono --roslyn /path/to/csc.exe --output /tmp/material-vfx.json
python Tools/Visuals/Regression/run.py --mono-root /path/to/mono --roslyn /path/to/csc.exe
python Tools/Aiming/Regression/run.py --mono-root /path/to/mono --roslyn /path/to/csc.exe
python Tools/Combat/TimmyMovementRegression/run.py --mono-root /path/to/mono --roslyn /path/to/csc.exe
```

## Noch notwendige Unity-/Geräteprüfung

Hier steht kein Unity-Editor und kein Android-Testgerät zur Verfügung. Es wurde
keine APK/AAB gebaut und keine Bildrate gemessen. Vor einem Release den Projektimport
in Unity 6000.6.1f1 prüfen, die echten URP-/Android-Shader kompilieren und folgende
Darstellung im Spiel prüfen:

- Alle Arenen, Helden, Skins und Waffen bei Gameplay- sowie Portrait-Abstand.
- Timmy-Visier, Fell, geschlossener Helm und blauer Rückenstreifen.
- Laser-Endpunkte, dunkle Portalmitten, 70%-Opacity-Kugeln und exakt begrenzte Warnflächen.
- Standpose, Attacken/Dash-Übergänge, Bodenkontakt und der unveränderte Rifle-Hold.
- Geschosse, Nebel, Wasser und Nahkampfspuren während dichter Kämpfe und auf Battery/Balanced/Performance.
- Vulkan/GLES-Tauglichkeit, Shaderstripping, Transparenzsortierung sowie GPU-Zeit und Speicher auf Zielgeräten.

Schaden, HP, Reichweiten, Cooldowns, Eingaben, Bewegung, AI, Spawnpunkte und Collider
wurden in dieser Überarbeitung nicht geändert. Android-ID bleibt
`com.lowperformence.dimensionsfront`.
