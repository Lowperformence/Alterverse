# Herkunft und Nutzungsrechte der Bauteilkonzepte

Stand: 12.10.2026. Dieser Durchlauf umfasst **Sivra, Ione, Zhaev, Traveler und Timmy**. **Auren, Tahla, Nerok und Brakk sind ausgeschlossen** und werden in anderen Chats bearbeitet. Ihre bisherigen Rechtsnachweise werden hier nicht ersetzt.

## Neue Entwürfe und Modellbauteile

Die archivierten `*-component-concept.png` wurden in diesem Auftrag mit OpenAI-Bildgenerierung erzeugt. Die jeweiligen `provenance.json` nennen den exakten Prompt, Eingabereferenz, SHA-256, Originaldateigröße und Git-Blob. Vorhandene C2PA-Metadaten bleiben in den unveränderten PNG-Dateien erhalten; eine Prüfung der C2PA-Signatur wird nicht behauptet. Bildkonzepte, tatsächliche Geometriepläne und Spielmodelle sind separat gekennzeichnet.

Die neue Geometrie ist im Quellcode `Tools/HeroConcepts/build_models.py` und `part_optimization.py` reproduzierbar beschrieben. Sie ist aus benannten Schalen, Wartungsflächen, Einlagen und Befestigungen aufgebaut. Die maßhaltigen SVG-Pläne sind Projektionen der tatsächlichen Modellgeometrie. Sie sind keine fremden Bilder und keine gemessenen Daten aus dem KI-Konzept.

Für neue OpenAI-Ausgaben gilt der Abschnitt **Content / Ownership of content** der [OpenAI Europe Terms of Use](https://openai.com/policies/eu-terms-of-use/), Fassung vom 16.01.2026, geprüft am 12.10.2026. Zwischen dem Nutzer und OpenAI werden dem Nutzer die gegebenenfalls bestehenden Rechte an der Ausgabe nach Maßgabe dieser Bedingungen und des anwendbaren Rechts zugeordnet. Das ist keine CC0-Freigabe, keine Exklusivitätszusage und keine Garantie, dass sämtliche übernommenen Bestandteile frei von Drittrechten sind.

Ein GitHub-Commit dokumentiert Herkunft, Zeitpunkt und Dateistand. Er erzeugt für sich allein keine Nutzungsrechte und heilt keine Rechte an Eingaben.

## Übernommene Bestandteile

Die ausdrücklich erlaubten bisherigen Grundformen, Proportionen, Skelettverträge und vorhandenen Animationen werden weiterverwendet. Jeder Held verweist auf seine vorhandene Einzelakte in `ArtSource/Heroes/Concepts/Provenance/`. Darin bleiben dokumentiert:

- CC0-Nachweise für die jeweils verwendete Blender-Basis und freie Animationspakete;
- die getrennte Einordnung von Adobe/Mixamo-Bestandteilen, soweit sie für diesen Helden gelten;
- die bisherige Projektgeometrie und ihr konkreter Quellstand;
- noch offene Nachweise historischer Referenzbilder sowie separat zu prüfender Audio-, Icon-, Textur-, VFX- und Skinbestandteile.

Neue eigene Konzepttafeln ersetzen keine fehlenden Nachweise historischer Referenzbilder. Dieser Durchlauf behauptet deshalb keine vollständig geschlossene Lizenzkette für das ganze Spiel.

## Modellstatus

Der Modellbau prüft jeden Helden einzeln und committed seine Bauteildateien erst nach erfolgreichen Blender-/FBX-Prüfungen. `validation.json` dokumentiert tatsächliche Prüfungen, `assembly.json` die Geometrieaufteilung. Unity-Editor- und Android-Prüfungen bleiben bis zu ihrer tatsächlichen Ausführung ausdrücklich offen.
