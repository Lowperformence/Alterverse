# DIMENSIONS FRONT — Menü-Pass

Status: **CODE READY**; noch keine Unity-/Gerätefreigabe.

- Dunkler kosmischer Hintergrund mit zwei Planeten, zurückhaltender violett/cyan Trennlinie und gebrochenem Mondhorizont. Einmalig erzeugte 768×384-RGBA-Textur (1,125 MiB GPU-Nutzdaten), CPU-Kopie nach Upload freigegeben; keine Partikel oder zusätzlichen Szenenlichter.
- Dieselben überarbeiteten 3D-Heroes wie im Match, einschließlich VOIDFALL. Transparenter Portrait-Hintergrund, ruhige Fassung und individueller Hero-Farbstreifen statt undifferenziertem Rechteck. Separater Vorschaukamera-Volumemasken-/Postprocessing-Zustand.
- Bestehende Home-, Play-, Heroes-, Cosmetics-, Profile- und Settings-Wege bleiben bestehen. Roster-Zeilen erhalten individuelle Farbakzente; Profil bezeichnet den tatsächlich vorhandenen lokalen, ungewerteten Fortschritt. Kein erfundener Rang.
- Safe Area und mindestens 1200×720 virtuelle Fläche bleiben erhalten. Navigation 48 Einheiten, primäre Aktionen 68–76, Lautstärke-Dragfläche 54. Dies sind Layoutmaße, keine nachgewiesenen physischen dp-Größen auf Geräten.

Akzeptanz offen: 16:9 / 20:9 / 4:3, Cutouts, Android-Systemleisten, Tastatur; lange Namen/lokalisierte Storepreise; Sichtbarkeit aller sechs Waffen und VOIDFALL-Flügel; Transparenz der Portraitkamera unter dem tatsächlich verwendeten URP-Renderer. Physische Touchgrößen und Lesbarkeit auf kleinstem unterstütztem Telefon prüfen. RenderTexture bleibt auf 640×720 begrenzt. Keine Desktop- oder Smartphone-Screenshots wurden in dieser Umgebung erzeugt.

Nach diesem Pass gilt Feature Freeze. Weitere Arbeiten nur an konkreten Release-Blockern.
