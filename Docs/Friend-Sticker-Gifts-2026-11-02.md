# Freunde und Sticker-Geschenke: Konto-Backend und Android gemeinsam aktualisieren

`FriendStickerService` verwendet jetzt echte serverseitige Freundes-IDs. Anfragen landen beim anderen Konto; nur der Empfänger kann sie annehmen. Die bisherige lokale Bestätigung ausgehender Anfragen und das automatisch erzeugte Dankeschön im eigenen Posteingang sind durch den Serverablauf ersetzt. Bestehende Clan-Funktionen und Sticker-Grafiken bleiben erhalten.

Die sechs Sticker kosten weiterhin 50, 100, 250, 500, 1000 beziehungsweise 2500 Shards. Der Empfänger löst sie weiterhin für 20 Prozent des Kaufwerts ein. Der Server bucht zuerst verdiente, danach gegebenenfalls gekaufte Shards ab. Empfangene Sticker können auch später und nach einem Neustart eingelöst werden.

## Bereitstellung

1. Die **aktuell eingesetzte** vollständige Node-`index.js` aus dem Konto-Backend exportieren und als `current-index.js` sichern. Keine ältere Datei über inzwischen eingespielte Katalog-/Kontokorrekturen kopieren.
2. Im Projektordner ausführen:

   ```powershell
   node Server/commerce/node/enable-friend-sticker-gifts.mjs current-index.js updated-index.js
   node Server/commerce/node/split-index-five.mjs updated-index.js Builds/AccountGifts/parts
   ```

3. Die fünf Dateien `index-teil-1-von-5.txt` bis `index-teil-5-von-5.txt` **unverändert in dieser Reihenfolge** in dieselbe `index.js` des Cloud-Editors einfügen. Jeder Teil setzt den vorherigen fort; die Teile sind keine einzelnen JavaScript-Module. Die Rekonstruktion wurde bytegetreu geprüft. Das vorhandene `package.json`, Servicekonto, Firestore-Projekt und andere Backend-Konfiguration bleiben bestehen.
4. Das Konto-Backend neu bereitstellen. Die bisherige Revision erhält den Zusatz `.friend-gifts.1`. Anschließend muss `/social/friend/state` mit einem gültigen Unity-Konto `stickerProtocolVersion: 1` liefern.
5. Den passenden Projektstand ziehen und das signierte Android-AAB neu bauen. Ein AAB allein installiert keine Backend-Routen. Ein Edgegap-Spielserver-Update installiert ebenfalls keinen Konto-Backend-Code.

Der Patch ergänzt nur den vorhandenen authentifizierten Social-Dispatcher und fügt das Geschenkmodul an. Unerwartete Ausgangsdateien und eine doppelte Installation werden abgewiesen. Eine zusätzliche SDK-Abhängigkeit wird nicht benötigt.

## Speicherung und Wiederholungen

Freundschaften verwenden die bestehenden `dimensions-front-social-friend`-Einträge. Blockierungen und Kontosperren werden geprüft. Versand setzt eine angenommene Freundschaft voraus; Selbstgeschenke und frei erfundene lokale IDs werden abgewiesen.

Geschenke und Versandbelege liegen unter `players/<account>/sticker-gifts` beziehungsweise `sticker-operations`. Die Absenderabbuchung, der Empfänger-Posteingang und der Operationsbeleg werden in einer Firestore-Transaktion geschrieben. Die Einlösung markiert das Geschenk und aktualisiert den Empfänger ebenfalls atomar. Reads stehen vor Writes. Gemeinsame Kontomarker serialisieren konkurrierende Listen-/Posteingangsänderungen; vorhandene Revisionsprüfungen verhindern das Zurückschreiben eines alten Guthabens.

Der Client speichert eine offene Versand-ID kontogetrennt unter einem von der Profilsynchronisation ausgeschlossenen `DF_Commerce*`-Schlüssel. Nach unklarer Antwort oder Neustart wird dieselbe Operation erneut abgefragt. Kontowechsel löschen den angezeigten Freundes-/Geschenkcache und verwerfen Antworten aus der alten Sitzung. Der Client bucht beim Klicken keine Shards lokal ab oder gut.

## Prüfungen

```sh
python -m unittest discover -s Tools/FriendGifts -v
node Tools/FriendGifts/test_patch.mjs
python Tools/FriendGifts/Regression/run.py --dotnet-root DOTNET_ROOT
python Tools/ClanChat/Regression/run.py --dotnet-root DOTNET_ROOT
PYTHONPATH=Server/commerce python -m unittest discover -s Tools/ClanRestore -p 'test_standalone.py' -q
```

Die Node-Tests führen den vollständigen gepatchten Konto-Handler mit einem Firestore-Testersatz aus, der Transaktionsabbrüche, Wiederholungen und Reads-nach-Writes prüft. Sie prüfen echte Anfragen zwischen getrennten Testkonten, alle Stickerpreise, Guthabenaufteilung, verlorene Antworten, gleichzeitige Abbuchungen und Einlösungen sowie authentifizierte HTTP-Routen und Kontotrennung. Die C#-Prüfungen kompilieren die echten Dienste mit Unity-Ersatzobjekten und prüfen ihre Transport-/Retry-Pfade. Das ersetzt weder einen nativen Unity-/Android-Build noch einen Test gegen das bereitgestellte Firestore-Projekt.

Abschließend mit zwei echten Testkonten: Anfrage senden, auf dem zweiten Gerät annehmen, einen Sticker schicken, Empfänger-App neu starten, einmal einlösen, erneut versuchen und beide Guthaben prüfen. Das neue Backend muss vor diesem Gerätetest erreichbar sein. Die gespeicherte lokale Freundesliste aus dem bisherigen Prototyp autorisiert keine echten Geschenke; echte Freunde müssen sich gegenseitig bestätigen.
