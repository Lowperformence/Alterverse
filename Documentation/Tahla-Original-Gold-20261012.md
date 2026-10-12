# Tahla Venn: Originalmodell mit dezentem Goldschmuck

Auftrag vom 12.10.2026: Neues Ersatzkonzept stoppen, ursprüngliche Heilerin wiederherstellen, nur kleine Details wie Goldarmreifen und Halskette ergänzen.

Identität: Tahla Venn, interner HeroId.Mira. VELIN wurde in dieser Sitzung nicht als Spielmodell integriert.

Wiederherstellungsquelle: Lowperformence/Alterverse, dev/vertical-slice, geprüfter Stand 2b6898b91bf71623fe18180344b87eafa5c5ebb0. Wiederhergestellt werden ausschließlich die ursprünglichen Dateien der Heilerin:

- ArtSource/Heroes/Mira.blend, Git-Blob 83082f00d25a432baead0b1d688e8b85ded744c8.
- Assets/Alterverse/Resources/AuthoredHeroes/Mira.fbx, Git-Blob cdcccdf5f92f901f56ddef57d36dfc043ae068a0.

Ziel: art/hero-parts-redesign-20261011. Die alte Tahla-Darstellung mit vorhandener Beinrüstung, Helm, Halo, Gliedmaßendetails und Stiefeln wird für Mira unabhängig vom globalen ConceptStyle-Schalter beibehalten. Andere Helden verwenden ihre bisherigen Bedingungen weiter. Alte Konzeptentwürfe bleiben als Historie erhalten; sie werden nicht als aktives Mira-Spielmodell verwendet.

Neue Gestaltung: zwei schmale, nicht leuchtende Goldarmreifen an ForearmL/R sowie eine feine goldene Halskette am Chest-Bone. Schmuck wird additiv aus eigener prozeduraler Geometrie erstellt. Er verändert keine Körpervertices, Bones, Waffe, Animationen, Fähigkeiten oder Hitboxen. Die Armreifen werden anhand des vorhandenen Ruhe-Meshs angepasst. Die Halskette folgt den Proportionen des ursprünglichen Rigs. Es gibt keine Schmuckphysik und keine neuen Collider.

Herkunft des neuen Schmucks: im Auftrag durch ChatGPT/Codex erstellter C#-Quelltext TahlaGoldJewelry.cs; keine externen Meshes, Texturen oder Symbole hinzugefügt. Das wiederhergestellte Originalmodell behält seine bisherige Herkunft und Rechtebasis. Kleine Schmuckergänzungen ersetzen oder bereinigen dessen bestehende Herkunft nicht. Eine neue pauschale Lizenzfreigabe für das alte Modell wird nicht behauptet.

Validierung: C#-Syntaxprüfung mit Tree-sitter; enge Prüfung der Mira-spezifischen Verzweigungen; wiederhergestellte Blob-SHAs gegen Quelle und Ziel verglichen. Unity/Android sind in dieser Umgebung nicht verfügbar. Import, sichtbarer Schmuck-Sitz und Animation im tatsächlichen Spiel müssen daher dort überprüft werden.
