# Kartenmaterialien: Sand, Erde, Beton, Stein und Wasser

Ausgangspunkt der Materialarbeit: `7b2cbc18387b69fcabcc729d7b737bf87a6ef036`. Integrierte aktuelle Movement-/Shop-Änderungen: `76f3938bb03d6cf1a8d2ad9ea5f28f907b88da3d`, Branch `dev/vertical-slice`.

## Sichtbare Änderungen

- Sand bekommt zufällig verteilte, abgerundete Körner als eigenes Materialdetail. Die feine Schicht wiederholt nicht länger die großflächigen Dünen derselben Textur in kleinerem Maßstab.
- Erde bekommt unregelmäßige Aggregate, Zwischenräume und verdichtete Stellen. Die vorhandenen braunen Bauwerksflächen und die vorhandene Bodenpalette bleiben erhalten.
- Die gemeinsamen Gebäudesockel bekommen erstmals ein Betonmaterial mit Zementkörnung, Zuschlag und kleinen Poren. Vorhandene Bunkermodelle mit ihren eigenen Beton-/Normaltexturen bleiben unverändert.
- Fels bekommt eine feine Mineralstruktur unter den bereits vorhandenen großen geologischen Mustern. Nasser Stein erhält eine geringere Reliefstärke; Metall, Rinde, Blätter und Eis erhalten keine zusätzliche Sand-/Steinkörnung.
- Die vier vorhandenen Wasserflächen der Erde bekommen bewegte Normalen, ruhigere Ufer, flache Bankfarben, zurückhaltende unterbrochene Uferdetails und den gemeinsamen URP-Licht-/Reflexionsweg. Wasser bleibt undurchsichtig und schreibt Tiefe wie die bisherigen Zylinder. Die Bankfarben suggerieren flachere Bereiche anhand des vorhandenen Zylinderradius; sie messen keine echte Wassertiefe. Es gibt keine neue Refraction-, Bildschirmkopie- oder Transparenzsortierungsabhängigkeit.
- **PSR J1719-1438 b:** Der ursprüngliche durchsichtige Kristallboden ist wieder die einzige durchgehende Bodenfläche. Die spätere facettierte Lit-Auflage und die zusätzliche nahtlose Prismenschicht werden nicht mehr gebaut; zurückgebliebene Renderer dieser Schichten werden ausgeblendet. Der Boden behält seine ursprüngliche Farbe, Alpha- und Fresnelwerte. Er bekommt weder die später hinzugefügten Bodendatentexturen noch die zusätzliche Probe-Reflexionsschicht. Andere Kristalle behalten ihre bisherigen Reflexionen. Die frühere Bindungsprüfung wurde korrigiert: Sie verlangt nun ausdrücklich den klaren Boden statt geologischer Bodentexturen.
- `EarthForestPolish` hat bisher alle 0,75 Sekunden die vorhandenen Wasser-Renderer gesucht und ihre Materialien durch `DF_Earth_Water_FlatUntextured` ersetzt, einschließlich `_SPECULARHIGHLIGHTS_OFF`. Dieser überschreibende Pass und sein zusätzliches Material entfallen. Das Wasser wird beim Bau des bestehenden Kartenmaterials einmal eingerichtet und vom vorhandenen Arena-Lebenszyklus freigegeben.

## Kartenzuordnung

Die vorhandenen 33 planetenspezifischen Boden-/Stein-/Makrotexturen werden nicht ersetzt. Die neue Feinstruktur wird zusätzlich nach Materialart gebunden.

| Karte | Neue Feinstruktur des Bodens | Besonderheit |
|---|---|---|
| Moon | Sand, zurückhaltend | Feiner Regolith, vorhandene Krater bleiben |
| Mars | Sand | Körner unter den vorhandenen Dünen |
| GreenPlanet | Erde | Organische Bodenaggregate |
| Noxara | Stein | Bestehende mineralische Geologie |
| Kryon | keine zusätzliche Bodenkörnung | Eis-/Frostboden bleibt erhalten |
| Earth | Erde | Braune Bodendetails und vier animierte Wasserflächen |
| Pyraxis | Stein | Bestehende Vulkan-/Lavageologie |
| Neptune | Stein mit geringerem Relief | Nasse Oberflächen, kein pauschales Wasser auf Felsen |
| Dark00 | Stein, zurückhaltend | Dunkle Bodenpalette bleibt |
| DiamondCore | keine zusätzliche Bodenkörnung | Ursprünglicher durchsichtiger Kristallboden wiederhergestellt |
| GJ504b | Sand | Magenta-Palette und Dünen bleiben |

## Technische Begrenzung

Vier eigene, deterministisch erzeugte 256×256-RGBA8-Datentexturen speichern Richtung/Relief, Rauheit und neutralen Detailwert. Import: linear, Repeat, Trilinear, Mipmaps, unveränderte RGBA-Kanäle auch unter Android. Zusammen etwa **1,33 MiB** einschließlich aller Mipstufen, wenn alle vier vollständig resident sind; tatsächliche Laufzeitkosten wurden nicht gemessen. Keine externen Bildassets oder neue Fremdlizenzen.

Die Mikrodetails werden anhand des Pixelfußabdrucks ausgeblendet. Explizite Texturgradienten werden vor dem Sichtbarkeitszweig berechnet, damit Mip-Auswahl an dessen Übergängen stabil bleibt. Auf Bodenflächen ersetzt die neue Detailprobe die bisherige zweite Grobtexturprobe; auf nahe sichtbaren Felsen kommen bis zu drei triplanare Proben hinzu. Wasser benötigt keine zusätzliche Textur. Sichtbare Qualität und GPU-Zeit auf Android bleiben Pflichtprüfungen.

Die ursprüngliche Arena-Geometrie (ausgenommen die ausdrücklich zurückgenommenen späteren Kristallbodenauflagen), Poolpositionen/-größen, Kollisionen, Navigation, Heldenmodelle, Bewegungslogik, Spielbalance und sämtliche Audiodateien werden nicht geändert.

## Durchgeführte Prüfungen

- **240 Aussagen** mit echten neuen C#-Profilen und Materialbindungen: elf Karten, acht Dekorationsmaterialarten, falsche Enumwerte, deaktivierte Feinstruktur, fehlende Texturen, Wasser-Shader fehlend/nicht unterstützt, Farberhalt und aktivierte Reflexe, unveränderte Kristallfarbe/Alpha und Abschaltung beider alter Bodenauflagen. Unity-Material-/Resources-Objekte sind Testdoubles; keine Unity-Kompilierung.
- **123 Offscreen-Darstellungen** des tatsächlichen Forward-HLSL mit den echten Datentexturen, verschiedenen Maßstäben, Drehungen, Entfernungen, Auflösungen sowie allen vier Poolproportionen und mehreren Zeitpunkten. **22 Vertex-/Fragment-Stufen** inklusive regulärem und Screen-Space-Shadow-Zweig werden nach SPIR-V kompiliert und validiert.
- Bei deaktivierter zusätzlicher Feinstruktur beträgt der maximale Pixelfehler gegenüber den beiden Originalshadern **1,19×10⁻⁷**. Wasseranimation ist in beiden geprüften Auflösungen sichtbar; minimale maximale Pixeländerung zwischen t=0 und t=0,8: **0,01869**. Alle ausgegebenen Farbwerte sind endlich, alle Oberflächen bleiben opak und alle Fixtures enthalten tatsächlich gezeichnete Pixel.
- **Kristallboden:** Neun Fixtures mit unterschiedlichen Maßstäben/Drehungen stimmen bei RGBA exakt mit dem ursprünglichen Shader aus `66ce515460c44358ac1f0a3de57378d9910c67c0` überein (**maximaler Fehler 0**). Der Hintergrund behält in diesen Fixtures mindestens **69,76 %** seines Beitrags durch den Boden. Die aktuelle Kristallreaktion mit aktivierten Probe-Reflexen stimmt ebenfalls exakt mit der vorherigen Version überein. Historischer Originalshader liegt ausschließlich als Testfixture außerhalb `Assets` unter `Tools/PlanetSurfaces/Fixtures`.
- **300 bestehende Blattlicht-Fälle** sowie vier Scenery-Shader-Stufen bestanden.
- Alle **33 vorhandenen planetenspezifischen Datentexturen** erfüllen die bestehende Kanal-/Importprüfung. Die vier neuen Texturen sind bitgenau reproduzierbar, haben keine auffällige periodische Grenzstufe und korrekt orientierte Reliefgradienten (Korrelation mit dem Detailwert >0,998).
- **471 C#-Dateien** syntaktisch geprüft. Der bekannte Parserfehler bei dem gültigen lokalen Bezeichner `required` wird ausschließlich in der Parserkopie maskiert; die Android-Datei bleibt unverändert.
- Die **2.760 vorhandenen Lifecycle-Aussagen** bestanden erneut: Start/Fehlerbereinigung, Neustarts, Recall, Respawn, Pause, Runden und lokaler Gast-/Host-Kontext. Engine, Netzwerk und Geometriefabriken sind dort Ersatzobjekte.

[Materialvergleich](../ArtSource/PlanetSurfaces/2026-10-09/material-comparison.png): kontrollierte Materialfixtures außerhalb Unity. Die Bilder verwenden Ersatzbeleuchtung und keine echte URP-Umgebung, AO, Reflexionsproben oder Nachbearbeitung. Die linke Wasserfläche ist ausdrücklich eine flache Farbkontrolle, keine Aufnahme des alten Unity-Lit-Shaders. Boden-Farbkarten und Terrainrelief werden in den Fixtures durch definierte Farbflächen ersetzt. Der Kristallvergleich verwendet ein Schachbrett hinter der Fläche, um den tatsächlichen Alpha-Ausgang des historischen und des korrigierten HLSL sichtbar zu machen; die lineare Alpha-Komposition ist eine kontrollierte Fixture. Die Bilder belegen keine komplette Karte und keine Android-Leistung.

## Reproduzieren und in Unity abschließen

```sh
python Tools/PlanetSurfaces/build_micro_finishes.py --check
python Tools/PlanetSurfaces/validate_surface_assets.py
python Tools/PlanetSurfaces/verify_materials.py --mono-root /path/to/mono --output ArtSource/PlanetSurfaces/2026-10-09
python Tools/PlanetSurfaces/render_material_regression.py --native-bin /path/to/shader-tools --baseline /path/to/7b2cbc1-checkout --output ArtSource/PlanetSurfaces/2026-10-09
```

Abhängigkeiten: NumPy, Pillow, Tree-sitter C#, Mono, ModernGL/EGL, glslangValidator, spirv-val, spirv-cross. Die Generatoren arbeiten ohne Unity/Blender.

Noch offen, weil hier weder Unity noch ein Android-Gerät verfügbar ist:

- [ ] In Unity 6000.6.1f1 importieren; `DIMENSIONS FRONT/Checks/Surface quality shaders` ausführen, danach mit Android als aktivem Buildziel wiederholen. Die Prüfung enthält jetzt beide Kartenshader und den Wassershader.
- [ ] Auf jeder der elf echten Karten `DIMENSIONS FRONT/Visuals/Validate active planet surface` ausführen. Die Prüfung kontrolliert jetzt die Feinstruktur, auf Earth die vier tatsächlichen Wasser-Renderer und auf PSR J1719 den klaren Boden ohne aktive Zusatzauflagen.
- [ ] Echte Gameplay-Kamera mit `Capture gameplay surface` aufnehmen; Lesbarkeit von Helden/Attacken, Beton, Boden und Ufern bei Bewegung beurteilen.
- [ ] Alle Karten wechseln, Match verlassen/neustarten, Hintergrund/Resume und niedrige Grafikqualität auf mindestens einem schwächeren und einem stärkeren Android-Gerät prüfen.
- [ ] Vulkan und GLES-Fallback bauen und messen: Shaderimport, Reflexionsproben, Schatten, AO, Mip-Streaming, Speicher und GPU-Frametime. Die portablen Tests ersetzen diese Prüfung nicht.
