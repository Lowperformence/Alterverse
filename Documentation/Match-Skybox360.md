# 360°-Hintergrund in regulären Matches

Die neue Weltraumkulisse wird automatisch beim Match-Start geladen, **online und offline**. Es gibt keinen Vorschau-Schalter und keinen notwendigen Menüaufruf. Nach dem Pull Unity außerhalb des Play-Modus fertig kompilieren/importieren lassen und die AAB wie gewohnt neu bauen. Dafür ist kein neuer Dedicated Server nötig.

Der Standard-Himmel `Skybox/Panoramic` zeigt ein echtes Rundumpanorama aus Sternen und Nebeln. Fünf separate 3D-Planeten einschließlich eines Ringplaneten liefern Parallaxe beim Bewegen. Jede Arena erhält eine andere Ausrichtung im Sternen-/Nebelhimmel. Hauptmenü und Heldenpodest bleiben unverändert.

Die Planeten stehen weit hinter der Spielfläche, benutzen den normalen Tiefentest und werfen keine Schatten auf die Arena. Es gibt keine neue Kamera, keine Vollbildüberlagerung, keine Blits und keine Änderungen an Kamera-Position, Projektion, Culling, Sichtweite oder globaler Beleuchtung. Ein gemeinsames Sphere-Mesh: 3.968 Dreiecke, Ring: 2.560 Dreiecke.

## Android-Build

- Alle sechs Texturen liegen in `Assets/Alterverse/Resources/MatchSkybox360/Textures` und gehören zum Player-Build. Die ursprünglichen Textur-GUIDs bleiben beim Umzug erhalten.
- `MatchSkybox360Assets` erzeugt nach dem Import und vor jedem Client-Build ausschließlich die zugehörigen Materialien unter `Resources/MatchSkybox360/Generated`. Die Materialien referenzieren die Standard-Shader und Texturen ausdrücklich, damit Unity sie bei der Android-Paketierung berücksichtigt. Der generierte Ordner bleibt lokal reproduzierbar und wird nicht versioniert.
- Der Texturimporter erfasst nur diesen Ordner: Android ASTC 4×4 mit Qualität 90 für den Himmel (größere, helle Sternkerne); Planeten weiterhin ASTC 6×6 mit Qualität 70. Mipmaps, maximal 2.048 Pixel für das Panorama und 1.024 für die Planeten, keine CPU-Kopie.
- Fehlt eine Bild-/Shader-Abhängigkeit, stoppt die Build-Prüfung mit einer konkreten Fehlermeldung. Im laufenden Spiel bleibt bei fehlenden Skybox-Materialien der vorhandene Foto-/Prozeduralhintergrund als Rückfall erhalten.

`CosmicArenaBackdrop` besitzt die Kulisse. Beim Match-Ende werden die Kamera wieder freigegeben und eigene Meshes/Materialinstanzen zerstört. Ein späterer Kamera-/Menübesitzer wird nicht überschrieben. Ein Dedicated Server lädt und erzeugt keine der neuen Himmel-/Planetenressourcen.

## Prüfung und Herkunft

Alle Texturen wurden eigens prozedural erstellt; keine zusätzlichen Asset-Kaufkosten. Herkunft, Bilddimensionen und SHA256 stehen im Ressourcen-Manifest. Der Quellgenerator ist im ursprünglichen [Skybox-Paket](https://chatgpt.com/library?file_id=libfile_8f4d75bd328481918276adf0021ca794&account_id=personal) enthalten.

```bash
python Tools/Visuals/MatchSkybox360Regression/run.py --dotnet-root /path/to/dotnet-sdk
```

Die Prüfung kompiliert den vollständigen Laufzeit- und Editor-Code mit API-Fixtures und kontrolliert Material-Abhängigkeiten, automatische Einbindung, Wiederherstellung, Kamerabesitzerwechsel, Mesh-Geometrie, Ressourcenfreigabe und den Server-Zweig. Das ersetzt keinen nativen Unity-/Android-Render-Test. Hier steht kein Unity-Editor zur Verfügung; die Sichtprüfung auf dem Gerät bleibt offen. Die reguläre Einbindung ist trotzdem wie beauftragt aktiviert.

## Sichtbare Sterne auf Android

Die ursprünglichen Sternkerne waren bereits bei 4.096 Pixeln meist kleiner als ein Bildpunkt. Beim Verkleinern auf 2.048 Pixel blieben in einer Lanczos-Vergleichsrechnung nur vier Bildpunkte über 50 % sRGB-Helligkeit; bei 1.024 Pixeln keiner. Der neue Offline-Generator vergrößert und verstärkt ausschließlich die Sterne. Nebelfarben, Projektion, Laufzeit-Skybox-Shader, Kamera, Planeten und Server bleiben dabei unverändert. Der Himmel erhält zusätzlich die feinere ASTC-4×4-Kompression. Eine echte ASTC-/Android-Sichtprüfung ist damit noch nicht ersetzt.

Der Generator liegt in `Tools/Visuals/MatchSkybox360Stars/rebuild.py` (numpy, Pillow, moderngl/glcontext und EGL erforderlich). `visibility.py` prüft helle Sternpunkte nach Verkleinerung auf 2.048 und 1.024 Pixel.
