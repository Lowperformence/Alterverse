# 360°-Skybox im Offline-Match testen

Die neue räumliche Weltraumkulisse ist zunächst ein ausdrücklich zuschaltbarer **Editor-Test**. Sie startet nie automatisch und wird nicht mit der AAB oder dem Server ausgeliefert. Der bestehende Match-Hintergrund, das Hauptmenü und das Heldenpodest bleiben unverändert.

## Testen

1. Aktuellen Stand von `codex/epic-toon-replacement-20261024` holen und Unity kompilieren lassen. Das separate ZIP muss nicht importiert werden.
2. In Unity den Play-Modus starten und ein **Offline-Match** öffnen.
3. **Tools → Dimensions Front → Skybox 360 → Offline-Match testen** öffnen.
4. **Neue 3D-Kulisse testen** anklicken. Im Game-Fenster mit dem Helden zum Map-Rand laufen: Sterne und Nebel umgeben die Arena, fünf echte Planeten einschließlich eines Ringplaneten verschieben sich räumlich hinter ihr.
5. **Originalhintergrund wiederherstellen** anklicken oder das Testfenster schließen. Auch Play-Ende, Skript-Neukompilierung, Rückkehr ins Menü und Match-Wechsel entfernen die Vorschau.

Die Testobjekte erscheinen unter `DF_MatchWorld/DF_Skybox360_EditorPreview`. Sie werden nicht in Szenen gespeichert. Das Fenster muss für den Test geöffnet bleiben. Online-Matches sind für diese Vorschau gesperrt.

## Umsetzung und Grenzen

- Unitys Standard-Shader `Skybox/Panoramic` mit genau einem Pass, ausschließlich als lokale Skybox der Match-Kamera. Keine Blits, Render-Texturen, neue Kamera, Vollbild-Overlays oder Änderungen an `RenderSettings`.
- Fünf separate Planeten teilen ein Sphere-Mesh (3.968 Dreiecke); der Ring hat 2.560 Dreiecke. Keine zusätzlichen Lichter, Schatten oder Collider. Die tatsächliche Perspektivkamera des Spiels bleibt in Position, Projektion, Sichtweite und Culling unverändert.
- Objekte liegen mit Abstand hinter der Arena innerhalb der vorhandenen Kamerasichtweite. Dadurch deckt die Spielgeometrie sie mit dem normalen Tiefentest ab. Ein neuer Kamera-/Menübesitzer wird beim Aufräumen nicht überschrieben.
- Bilder und Code liegen unter `Assets/Alterverse/Editor/Skybox360Preview`; es gibt keinen Spielstart-Hook und keine PlayerPrefs. Das Öffnen des Fensters aktiviert nichts.
- Der Texturimporter erfasst ausschließlich den neuen Testordner. Panoramen maximal 2.048 Pixel, Planeten 1.024 Pixel; Mipmaps, sRGB, keine CPU-Kopie.
- Alle Bilder wurden eigens prozedural erstellt; keine zusätzlichen Asset-Kaufkosten. Herkunft und SHA256 stehen im benachbarten `manifest.json`. Das ursprüngliche Generator-/360°-Vorschaupaket liegt in [DF_Skybox360_Prototype.zip](https://chatgpt.com/library?file_id=libfile_8f4d75bd328481918276adf0021ca794&account_id=personal).

## Prüfung

Der vollständige Editor-Code wird mit API-Fixtures kompiliert. Geprüft werden Kamerawiederherstellung, Besitzerwechsel, fehlende Bilder/Shader, unerwartete Shader-Passanzahl, Fehler beim Mesh-Aufbau, Ressourcenfreigabe, Fenster-/Play-/Menü-/Netzwerkwechsel, Mesh-Indizes und die Tiefenlage der Planeten. Ohne `UNITY_EDITOR` enthält die Datei keinen Code. Die importierten PNGs werden vollständig dekodiert und per Hash geprüft.

```bash
python Tools/Visuals/Skybox360PreviewRegression/run.py --dotnet-root /path/to/dotnet-sdk
```

Diese Prüfungen ersetzen **keinen nativen Unity-Render- oder Android-Test**. Hier steht kein Unity-Editor zur Verfügung; die visuelle Prüfung im echten Match steht aus. Die neue Kulisse ist deshalb noch nicht für reguläre Matches oder Android aktiviert. Erst das Bild im tatsächlichen Spiel prüfen; anschließend kann die Einbindung für einen gezielten Android-Test erfolgen.
