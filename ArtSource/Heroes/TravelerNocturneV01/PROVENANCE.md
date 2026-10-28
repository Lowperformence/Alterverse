# Herkunftsprotokoll — Traveler Nocturne V01

Erstelltag: 2026-10-12. Projekt: Lowperformence/Alterverse. Auftraggeber: Nutzer dieses ChatGPT-Gesprächs. Ausführung: ChatGPT/Codex mit integrierter OpenAI-Bildgenerierung und neu geschriebenem parametrischem Python-Code. Nicht als rein menschlich handmodellierte Arbeit ausgewiesen.

## Primärinput: Nutzerauftrag

> Ich will, dass du für den Charakter den Traveler von Grund auf ein eigenes Modell baust, mit Prototyp und allen Herkunftsnachweisen, Prompts, eigenem Design, eine Sprengzeichnung von seiner Rüstung, Waffe. Waffe soll eine Kugel sein, wie jetzt auch, farblich und alles ist schwarz, düster, Raumanzug. Mit allem, was du brauchst, machst du davon eine Sprengzeichnung, um das möglichst detailliert und hochwertig anzufertigen. GitHub-Freigabe hast du.

## Tatsächlich verwendete Quellen

| Gegenstand | Quelle | Einordnung |
|---|---|---|
| Traveler, schwarz, Raumanzug, Kugelwaffe | Obiger Nutzertext | Gestaltungsvorgabe |
| Nocturne-Formensprache, Module und Maße | Neuer Entwurf in diesem Gespräch | KI-unterstützte Designarbeit |
| Konzeptbild | OpenAI-Bildgenerierung, Prompt 001 in prompts.txt | Neues KI-Bild ohne Eingabebilder |
| 3D-Geometrie | build_traveler.py | Neu geschriebene mathematische Primitive, geschlossene Platten und Kugelschalen |
| Materialparameter | build_traveler.py / DESIGN.md | Neue achromatische numerische PBR-Werte |
| Explosionsanordnung | Bauteilvektoren in build_traveler.py und parts.json | Aus identischer eigener Geometrie abgeleitet |
| Projektschnittstelle | Vier konkret genannte bestehende Repo-Dateien in README.md | Nur Schnittstellenanalyse, keine übernommene Geometrie oder kopierter Generatorcode |
| Rechtebasis | OpenAI-Nutzungsbedingungen für Europa, abgerufen 12.10.2026 | Offizielle Bedingungsquelle, kein eigenes Lizenzzertifikat |

Kein Base-Mesh, Asset-Store-Modell, Mixamo-Rig, Stockmaterial, Foto, Bild aus einem Franchise oder eingelesene alte Traveler-Geometrie Teil dieses Pakets. Python/NumPy/Matplotlib sind Ausführungswerkzeuge; ihre Bibliotheken werden nicht mitgeliefert. Blender-API ist ausschließlich in einem neu geschriebenen Importer angesprochen. Die Herkunft von Trainingdaten des Bildmodells ist hier nicht prüfbar.

## Erstellkette

1. Nutzerauftrag und GitHub-Zugriff festgestellt; Zielprojekt und Entwicklungsbranch über verbundene Quellen ermittelt und direkt im Repository verifiziert.
2. Repo-Pfade und Schnittstellen gelesen; neuer Entwurf auf Nutzertext aufgebaut.
3. Prompt 001 an integrierte Bildgenerierung gegeben, ohne Bildreferenzen. Ergebnis unverändert als `concept_exploded.png` übernommen. Genauer interner Modellname, Seed und signiertes Generierungszertifikat wurden nicht bereitgestellt und werden nicht erfunden.
4. Neue parametrische Bauquelle geschrieben, Geometrie lokal erzeugt, glTF/OBJ exportiert, Bauteile und Materialien registriert.
5. Geometrieprüfungen ausgeführt; tatsächliche Meshansichten visuell angesehen; Explosionsrender in sechs getrennte Baugruppenpanels verbessert.
6. Dokumentation, Prompts, Rechtevermerk und SHA-256-Prüfsummen mit den Artefakten versioniert. Github-Commit/PR identifizieren den Ablagestand; ein Git-Commit ist kein gerichtliches oder unabhängiges Urheberzertifikat.

## Grenzen des Nachweises

Das Protokoll belegt die konkret dokumentierte Erstellung dieser Fassung und die bewusste Nichtverwendung externer Mesh-/Texturquellen. Es beweist keine weltweite Einzigartigkeit der Formen, keine Markenfreigabe des Namens Traveler, keinen gesetzlichen Urheberrechtsschutz für sämtliche KI-Ausgaben und keine Lizenzbereinigung alter Projektdateien, Sounds oder Animationen. Keine erfundenen Lizenznummern, Unterschriften, CC0-Freigaben oder Drittanbieter-Zertifikate.

SHA256SUMS.json erfasst alle Paketdateien außer sich selbst; Prüfwerte sichern Dateizustand und Vergleichbarkeit. Neu erzeugte Ausgaben können nach einer Änderung der Bauquelle abweichen. Der ursprüngliche Bildprompt wird archiviert, nicht als deterministische Bildreproduktionsgarantie dargestellt.
