# Brakk P01 — Rechtebasis und Herkunftsprotokoll

Erstellungsdatum: 2026-10-12. Auftraggeber laut Sitzung: Chris Radic.
Auftrag: neuer Brakk als Faust-Nahkämpfer, Prototyp, Sprengzeichnung, Kleidung, Kopf, Rüstung und Waffen; Dokumentation und GitHub-Ablage autorisiert.

## Was tatsächlich nachgewiesen wird
Dieses Paket dokumentiert die hier erzeugten Dateien, den reproduzierbaren Geometriecode, den Bildprompt und ihre Prüfsummen. Es ist kein amtliches Lizenzzertifikat, keine unterschriebene Urhebererklärung und keine Garantie, dass Dritte niemals Ansprüche stellen. Ein Git-Commit zeigt den dort gespeicherten Inhalt und seine Version; er beweist allein keine Exklusivität oder Schutzfähigkeit.

| Asset | Ursprung | Fremdmaterial als Eingabe | Rechtebasis / Status |
|---|---|---|---|
| Name Brakk, Rolle Faust-Nahkämpfer | Nutzerauftrag | kein Modell/Referenzbild | Name als Projektbezeichnung; Markenprüfung offen |
| Konzeptbeschreibung und Bauplan | in dieser Sitzung KI-unterstützt erstellt | keine externen Charaktervorlagen | OpenAI-Output; siehe Bedingungen |
| Python-Generator, Blender-Import | in dieser Sitzung KI-unterstützt geschrieben | keine kopierte Asset-Geometrie | OpenAI-Output; keine öffentliche Open-Source-Lizenz vergeben |
| OBJ, MTL, parts.json | deterministisch aus build_brakk.py erzeugt | keine externen Modelle oder Texturen | abgeleitet aus dokumentiertem Generator |
| Konzeptbild | OpenAI-Bildgenerierung aus PROMPT.txt | keine Referenzbilder | KI-generiert; kein behaupteter menschlicher Alleinurheber |
| Dokumentation/SVG | in dieser Sitzung erzeugt | keine Fremdillustrationen | OpenAI-Output |

Die fehlende Nutzung fremder Eingabeassets bedeutet nicht, dass das Trainingsmaterial des Bilddienstes hier auditierbar wäre. Keine Aussage über das gesamte Training oder weltweite Ähnlichkeitsfreiheit.

## Rechtebasis statt erfundener „Prototyp-Lizenz“
Quelle: OpenAI Nutzungsbedingungen für Europa, https://openai.com/de-DE/policies/eu-terms-of-use/
Am 12.10.2026 abgerufen; auf der Seite angegebene Aktualisierung: 16.01.2026.
Relevante Abschnitte: Content / Ownership of content / Similarity of content.
Zusammenfassung: Zwischen Nutzer und OpenAI liegen Rechte am Output beim Nutzer, soweit das geltende Recht dies erlaubt; OpenAI überträgt gegebenenfalls eigene Rechte. Ähnliche Outputs für andere Nutzer sind möglich; Rechte anderer Outputs und Drittinhalte sind nicht mitumfasst. Verantwortung für zulässige Inhalte bleibt beim Nutzer. Das ist eine vertragliche Rechtebasis, kein Nachweis exklusiven gesetzlichen Urheberrechts.

Für diese Erstellung wurde keine fremde Asset-Lizenz benötigt, da kein fremdes Modell, keine fremde Textur und kein fremdes Referenzbild eingebaut wurden. Für spätere Ergänzungen jede Quelle, Lizenzversion und Datei einzeln dokumentieren. Es wird hier keine CC0-, MIT- oder sonstige öffentliche Weitergabelizenz im Namen des Nutzers vergeben. Das Paket ist für dessen Projekt vorgesehen; daraus ergibt sich keine Freigabe an beliebige Dritte.

## Nachweiskette
Nutzerauftrag → eigene schriftliche Spezifikation → PROMPT.txt / Bildgenerierung → build_brakk.py → parts.json / OBJ / MTL → Bauplan → SHA256SUMS → GitHub-Commit.
Der Bilddienst meldet hier keine verifizierte Trainingshistorie; Modellversion, interner Seed und signierte Erstellungsbestätigung liegen nicht vor und werden nicht erfunden.

Die Konzeptgrafik wird als brakk_konzept.png dem Paket beigelegt und mit einer eigenen Prüfsumme erfasst. Der gespeicherte Prompt belegt den Auftrag; die konkrete PNG-Datei belegt das erhaltene Bild. Die Geometrie basiert auf der schriftlichen Spezifikation, nicht auf einem Bildmesh-Extrakt. Die automatisch generierten Zahlen im Konzeptbild weichen vom Bauteilkatalog ab; verbindlich sind README und SVG.

## Noch offene Veröffentlichungsschritte
Detailmodell und Animation fehlen. Bei menschlicher Weiterbearbeitung Arbeitsstände und Mitwirkende erfassen; bei externem Auftrag Rechtevereinbarung dokumentieren. Name und finalen visuellen Entwurf vor Veröffentlichung auf relevante fremde Rechte prüfen lassen, sofern eine belastbare rechtliche Freigabe benötigt wird. Keine rückwirkende Herkunftszuschreibung für alte Brakk-Dateien.

## Namenskorrektur
Nutzerkorrektur am 12.10.2026: Brakk mit zwei k. Der originale erste Bildprompt bleibt in PROMPT_ORIGINAL.txt erhalten. Titel und Dokumentation werden korrigiert; die Figur bleibt derselbe neue Entwurf.

SHA-256 der ursprünglichen Konzeptgrafik vor Titelkorrektur: 34b94bbc65ae8706f17adf3fecacc68fb06fb8dd4ba839cdf12a8ffdcbcd90a2
