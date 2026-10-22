# UFO-Testhut für 1 Shard

Der zusätzliche Hut wurde in Blender 5.0.1 modelliert: fliegende Untertasse, Schielaugen, türkiser Ring und zwei schiefe Antennen. Er ist ein kaufbares Testobjekt für **1 Shard**, SKU `ufo_test_hat`, Besitzschlüssel `DF_HeadwearOwned_UfoTestHat`, kosmetische ID 8. Die BETA-Cappy bleibt die Premium-Abschlussbelohnung auf Stufe 50; das Avatar bleibt auf Stufe 25.

Nach dem Kauf erscheint der UFO-Hut im Inventar unter Cosmetics > Skins. Der Kauf schaltet ihn frei, rüstet ihn aber nicht automatisch aus. Pro Held kann jeweils ein Hut gewählt und gleichzeitig mit vorhandenen Flügeln getragen werden. Der Hut hat keine Kollision und keinen Kampf- oder Fortschrittsbonus.

## Blender und Laufzeitmodell

- Quelle: `ArtSource/Headwear/UfoTestHat/create_ufo_hat.py`.
- FBX zum Öffnen/Importieren: `ArtSource/Headwear/UfoTestHat/UFO-Testhut.fbx`.
- Das tatsächliche Blender-Modell hat 2.579 Vertices, 4.984 Dreiecke und sechs Materialien. Die Laufzeit lädt seine exportierten Meshdaten aus `Resources/Headwear/UfoTestHat.json` und erstellt URP-kompatible Materialien. Es wird keine zweite, im Spielcode nachgebaute UFO-Geometrie verwendet.
- Pivot: Mitte der Kopföffnung. Blender: +Z oben, -Y vorne. Kopföffnung 0,290 × 0,261 m. Der kanonische Unity-Export verwendet +Y oben und +Z vorne sowie eine normalisierte Kopföffnung. Er wird wie die Cappy an der tatsächlichen Kopfgeometrie angepasst und an den Kopfknochen gehängt.
- Die Shop-Miniatur ist ein echter Blender-Render desselben Modells.
- Native Blender-Datei und GLB sind als separate Modelldateien bereitgestellt. Die Kamera/Lichter/Boden der Blender-Vorschau sind nicht Teil der Mesh-Exporte.

Neu erzeugen und prüfen:

```sh
blender --background --python ArtSource/Headwear/UfoTestHat/create_ufo_hat.py -- --output /tmp/ufo-hat
blender --background --python ArtSource/Headwear/UfoTestHat/validate_ufo_hat.py -- /tmp/ufo-hat
```

Die Blender-Prüfung bestätigt Pivot, Geometrie, Übereinstimmung des Unity-JSON und Einheiten/Achsen nach erneutem Import von FBX und GLB. Die Passform auf den echten zehn Helden ist damit noch nicht bestätigt.

## Kaufserver – vor einem echten Kauftest erforderlich

Der Python-Server des Repositories verwendet den ergänzten `ShardCatalog.json` automatisch. Der aktuell separat betriebene Node-Cloud-Run-Server hat einen eigenen `ITEMS`-Katalog. Dort muss innerhalb von `const ITEMS = { ... }` genau dieser Eintrag ergänzt und die Änderung bereitgestellt werden:

```js
  ufo_test_hat: item(1, [w('DF_HeadwearOwned_UfoTestHat')]),
```

Alternativ ergänzt `Server/commerce/node/enable-ufo-test-hat.mjs` diesen einen Eintrag mit Layout-/Preisprüfung und Sicherungskopie:

```sh
node Server/commerce/node/enable-ufo-test-hat.mjs /pfad/zum/aktuellen/index.js --write
```

Das Werkzeug ersetzt keine anderen Serverfunktionen und führt kein Deployment aus. Die Testversion der App und der aktualisierte Serverkatalog müssen beide bereitgestellt sein. Ein unbekannter SKU auf dem alten Server kann den echten Kauf noch nicht abschließen. Dieser Entwurf deployt weder App noch Cloud Run oder Edgegap.

## Prüfen

```sh
python Tools/Headwear/Regression/run.py --dotnet-root "$DOTNET_ROOT"
python -m unittest discover -s Server/commerce -p test_ufo_test_hat.py -v
node Server/commerce/node/test-ufo-test-hat.mjs
node --experimental-vm-modules Tools/Headwear/Regression/node_ufo_purchase.mjs /kopie/des/aktuellen/index.js
```

Die Node-Prüfung patcht die übergebene Serverquelle ausschließlich im Arbeitsspeicher und führt die echten Handler mit simuliertem Firestore/Auth aus. Sie verändert keinen Live-Server. Geprüft: ein verdienter Shard, ein gekaufter Shard, kein Guthaben, parallele Wiederholung, erneuter Kauf ohne Doppelabbuchung, Server-Neustart, Ausrüstung und Kontotrennung. Die Python-Ledger-Prüfung deckt denselben neuen SKU ab. Die gemeinsame C#-Prüfung lädt das echte Blender-Mesh und prüft Freischaltung, Hutwechsel, Flügelkombination, Profilladen und Netzwerk-ID.

In Unity im vorhandenen `DIMENSIONS FRONT > Headwear > BETA Cappy fitting studio` den Eintrag **UfoTestHat** auswählen. Der Kopf-Anheftungstest prüft nun beide Hüte auf allen zehn Helden. Anschließend jeden Helden von vorn, hinten und der Seite sowie unter Bewegung prüfen.

Für den Kauftest ein Testkonto mit mindestens einem Shard verwenden: Kauf im Shop, Guthaben und Besitz kontrollieren, im Inventar ausrüsten, App neu starten und dasselbe Konto verbinden. Anschließend denselben Artikel erneut öffnen; es darf keine zweite Abbuchung geben. Konto wechseln und prüfen, dass der Hut nicht übernommen wird. Vor Veröffentlichung fehlen Unity-/Android-Passform und ein echter Kauf mit dem aktualisierten Live-Server.
