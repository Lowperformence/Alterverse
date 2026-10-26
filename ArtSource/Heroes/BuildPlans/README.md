# Bauteilkonzepte und Blender-Aufbaupläne

Aktueller Umfang dieses Auftrags: **Sivra Quell, Ione Vaska, Zhaev Null und Andromeda Timmy**.

**Auren Kest, Tahla Venn, Nerok Abyss, Brakk Orun und THE TRAVELER sind ausgeschlossen.** Ihre zusätzlichen Modelländerungen aus diesem Durchlauf wurden auf den vorherigen Stand zurückgesetzt. Die früheren, bereits vorhandenen Konzept- und Modellakten dieser Helden bleiben erhalten. Sie werden in anderen Chats bearbeitet.

| Held | Spiel-ID | Bauteile | Spielmodell |
|---|---|---|---|
| [Sivra](Sivra/README.md) | Nyra | Kopf/Helm, Torso/Panzerung, beide Arme, beide Beine, Gewehr | `CyberRanger/Model.bytes`; Gewehr in `AuthoredHeroes/Nyra.fbx` |
| [Ione](Ione/README.md) | Elyra | Kopf/Helm, Torso/Panzerung, beide Arme, beide Beine, Fokus | `UmbralMage/Model.bytes` |
| [Zhaev](Zhaev/README.md) | Veyr | Helm, Torso/Umhang/Panzerung, beide Arme, beide Beine, Klingen | `AuthoredHeroes/Veyr.fbx` |
| [Timmy](Timmy/README.md) | Timmy | Kopf/Helm, Raumanzug, beide Arme, beide Beine, Schwanz, Energiekrallen | `AuthoredHeroes/Timmy.fbx` |

Jeder Ordner enthält die ursprüngliche KI-Konzepttafel, ihren exakten Prompt und Dateinachweis, eine Montage-Blender-Datei, eine Explosionsansicht als Blender-Datei, OBJ-Bauteile, echte orthografische Modellprojektionen mit Maßen sowie Bauteil- und Validierungsberichte. Die Bilder sind Gestaltungsentwürfe; die SVG-Maße stammen aus der tatsächlichen Geometrie.

Modellbau, Teilaufteilung und FBX-Rückimport werden je Held abgeschlossen, bevor der nächste folgt. Die automatischen Prüfungen decken Skelett, erhaltene Ausgangspositionen, Skinning, Gewichtsnormalisierung, gespeicherte Ausgangsanimationen und die vollständige Dreiecksaufteilung ab. Sie ersetzen keine Unity-/Android-Ansicht oder Geräteprofilierung.

Die Unity-Ressourcen und editierbaren Quellen liegen im bestehenden Art-Branch und PR #24. Der Hauptbranch wurde durch diesen Auftrag nicht zusammengeführt. Ein spielbereiter Endzustand wird bis zu den tatsächlichen Editor-/Geräteprüfungen nicht behauptet.

Herkunft und Nutzungsrechte: [RIGHTS.md](RIGHTS.md). Frühere offene Herkunftsnachweise werden durch einen neuen Entwurf nicht automatisch geschlossen.
