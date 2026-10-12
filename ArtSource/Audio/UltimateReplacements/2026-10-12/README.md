# Ultimate-Ersatzclips — 12. Oktober 2026

Sieben Ultimate_Selected-Ressourcen wurden aus kostenlosen, archivierten CC0-Downloads neu zusammengestellt. Die alten hochgeladenen Aufnahmen dienten als Referenz für Dauer, Dynamik und Frequenzverteilung. Kein Sample aus einer dieser ungeklärten Aufnahmen wurde in den Ersatz kopiert. Die damaligen Downloadquellen bleiben unbekannt.

## Hörproben

[Alle sieben nacheinander](Ultimate-Hoerproben.mp3), mit einer Sekunde Pause zwischen den Helden:

| Reihenfolge | Held / interne ID | Entwurf | Einzelprobe |
|---|---|---|---|
| 1 | Brakk Orun / Rook | Energieanlauf und schwerer Einschlag | [MP3](Previews/Brakk-Orun.mp3) |
| 2 | Tahla Venn / Mira | Heilimpuls mit magischem Nachklang | [MP3](Previews/Tahla-Venn.mp3) |
| 3 | Ione Vaska / Elyra | Tiefer, anhaltender Energiepuls | [MP3](Previews/Ione-Vaska.mp3) |
| 4 | Sivra Quell / Nyra | Energieanlauf mit vier Schussimpulsen | [MP3](Previews/Sivra-Quell.mp3) |
| 5 | Auren Kest / Kaelon | Schwerer, bassbetonter Schild-/Festungsimpuls | [MP3](Previews/Auren-Kest.mp3) |
| 6 | THE TRAVELER / Traveler | Portalauftakt, Kollaps und Energienachlauf | [MP3](Previews/The-Traveler.mp3) |
| 7 | Zhaev Null / Veyr | Kurzer futuristischer Energieangriff | [MP3](Previews/Zhaev-Null.mp3) |

Die Bezeichnungen beschreiben die beabsichtigte Gestaltung. Auswahl und Bearbeitung beruhen auf technischen Klangmerkmalen und den Heldenrollen; eine subjektive Hörabnahme in diesem Lauf wird nicht behauptet. Die sieben Clips sind eine überprüfbare Ersatzrevision und können nach Hörfeedback angepasst werden.

## Quellen und Lizenznachweise

| Originalanbieter | Downloadangebot | Lizenzwahl | Gesicherte Dateien |
|---|---|---|---|
| Kenney | [Sci-Fi Sounds](https://opengameart.org/content/sci-fi-sounds), zusätzlich [Anbieterseite](https://kenney.nl/assets/sci-fi-sounds) | CC0-1.0 | Sechs Original-OGGs und unveränderter [Paketlizenztext](Kenney-LICENSE.txt) |
| rubberduck | [60 CC0 Sci-Fi SFX](https://opengameart.org/node/97882) | CC0-1.0 | sfx_06.ogg |
| Augmentality / Brandon Morris, eingereicht von HaelDB | [Spell sounds](https://opengameart.org/content/spell-sounds) | CC0-1.0 aus dem angebotenen Mehrfachlizenzangebot gewählt | healing.ogg, Original mit mehreren Varianten |
| JaggedStone | [Magic Spell SFX](https://opengameart.org/content/magic-spell-sfx) | CC0-1.0 | magical_1.ogg |
| Ogrebane | [Teleport Spell](https://opengameart.org/content/teleport-spell) | CC0-1.0 | teleport.wav |

Die einzelnen Anbieterangebote wurden am 12.10.2026 auf Titel, Urheber und CC0-Angabe geprüft. Für die vier Einzelangebote ist die jeweilige Anbieter-Lizenzdeklaration der Beleg; ein separates, nicht mitgeliefertes Lizenzzertifikat wird nicht behauptet. [CC0-1.0](https://creativecommons.org/publicdomain/zero/1.0/) ist die ausgewählte Lizenzgrundlage. Die Autoren werden auch ohne vorgeschriebene Namensnennung dokumentiert.

[sources.json](sources.json) enthält Originaldatei, exakte Downloadadresse, Urheber, Lizenzwahl, SHA-256, Bytezahl und bei Paketen den Archivhash und Originalpfad. Nur tatsächlich verwendete Originalsamples liegen unter Sources/. Andere heruntergeladene Kandidaten werden nicht als Projektquellen geführt.

## Reproduktion und technische Prüfung

Generator: [build_licensed_ultimates.py](../../../../Tools/Audio/build_licensed_ultimates.py). Aufruf im Repository: `python Tools/Audio/build_licensed_ultimates.py` (numpy und ffmpeg erforderlich).

[validation.json](validation.json) enthält für jeden Clip Quellen, Zuschnitt, Zeitpositionen, Pegel, Tempoänderung, Ausgabehash, Dauer, Sample- und True-Peak und integrierte Lautheit. Der Generator verwendet ausschließlich archivierte lizenzierte Quellen, normalisiert sie, mischt die dokumentierten Ereignisse und kurze Echos und begrenzt Spitzen vor der Vorbis-Kodierung. Alle Messungen werden anschließend am tatsächlich kodierten OGG vorgenommen.

Ziel ist die bereits dokumentierte Fähigkeitenausgabe von rund -22,88 LUFS nach bestehendem Code-Gain. Der bestehende zusätzliche Traveler-Gain wird einbezogen. Dateinamen und Ressourcenpfade bleiben identisch; vorhandene Unity-Importer-GUIDs werden nicht verändert. Bestehende Gameplay- und Unterstützungs-Audiolayer bleiben erhalten.

Die sechs unterschiedlichen alten Uploads hatten sieben Zuordnungen: Auren und Traveler waren beim ursprünglichen Import identisch. Der aktuelle Traveler-Clip hat laut späterer Lautstärkeprüfung eine Dauer von etwa 7,25 Sekunden; diese spätere Laufzeitdauer wird im neuen Entwurf beibehalten. Ein bytegenauer Ersatz durch den ursprünglichen Upload wird nicht behauptet.

Die Prüfung umfasst Dekodierung, endliche Samples, keine Vollaussteuerung, Spitzenbegrenzung, Lautheitsabgleich und Dauerkontrolle. Unity-Import, Android-Gerätetest und vollständige Live-Mischung wurden hier nicht ausgeführt.

## Historische Belege

[historical-unverified-import-validation.json](historical-unverified-import-validation.json) hält die alte Importzuordnung fest. [reference-feature-comparison.json](reference-feature-comparison.json) dokumentiert den technischen Vergleich mit heruntergeladenen Kandidaten. Ein niedriger Vergleichsabstand beweist weder identische Aufnahmen noch die alte Herkunft und ersetzt keine Hörbeurteilung.

Die aktive Ersatzrevision klärt die Herkunft der neuen Clips. Alte Git-Revisionen und alte Quelldateien erhalten dadurch keine rückwirkende Lizenzfreigabe.
