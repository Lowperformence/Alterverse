"""Build per-hero provenance records from archived project evidence, without inventing licenses."""
from pathlib import Path
import hashlib,json

ROOT=Path(__file__).resolve().parents[2]
DIRECTORY=ROOT/'ArtSource/Heroes/Concepts'
AS_OF='2026-10-12'
RUNTIME={'Sivra':'CyberRanger','Ione':'UmbralMage','Nerok':'CyberShark','Auren':'SiegeTank','Traveler':'ChronoTraveler'}
NATIVE={'Zhaev':'Veyr','Tahla':'Mira','Brakk':'Rook','Timmy':'AndromedaTimmy/AndromedaTimmy'}
GENERATORS={'Sivra':'CyberRanger/build_cyber_ranger.py','Ione':'UmbralMage/build_umbral_mage.py','Nerok':'CyberShark/build_cyber_shark.py','Auren':'SiegeTank/build_siege_tank.py','Traveler':'ChronoTraveler/build_chrono_traveler.py','Timmy':'AndromedaTimmy/build_timmy.py'}
PARTS={'Sivra':'detailed replacement head','Ione':'detailed replacement head','Tahla':'three-layer gold/ivory shoulder plates','Nerok':'tailored jacket and trousers'}

def main():
    manifest=json.loads((DIRECTORY/'manifest.json').read_text())
    contracts=json.loads((DIRECTORY/'input-contracts.json').read_text())
    records=[]
    for c in manifest:
        key=c['key'];hero=c['heroId'];inputs=[]
        anatomy='procedurally generated costume/body geometry' if key in RUNTIME or key=='Timmy' else 'adapted Blender Human Base Meshes 1.4.1 anatomy'
        if key in RUNTIME:
            kind=RUNTIME[key];contract=contracts['runtime'][kind]
            inputs.append({'file':f'Assets/Alterverse/Resources/{kind}/Model.bytes','sha256':contract['sha256'],'revision':contracts['sourceRevision'],'role':'retained source geometry, anchors and weights'})
            inputs.append({'file':'Tools/'+GENERATORS[key],'role':'existing project geometry generator; previous designs also used user-supplied visual references'})
            base='Rook' if key=='Nerok' else hero
            inputs.append({'file':f'Assets/Alterverse/Resources/AuthoredHeroes/{base}.fbx','role':'inherited shared rig/base renderer and compatible weapon controls; anatomical base originates in Blender Human Base Meshes'})
        else:
            source=contracts['native'][hero]
            inputs.append({'file':source['source'],'sha256':source['sha256'],'revision':contracts['sourceRevision'],'role':anatomy+' and original project costume/rig'})
            inputs.append({'file':'Tools/'+GENERATORS[key] if key=='Timmy' else 'Tools/HeroRebuild/build_heroes.py','role':'existing project authoring generator'})
        record={
            'hero':c['hero'],'heroId':hero,'key':key,'asOf':AS_OF,
            'concept':{'file':c['concept'],'sha256':c['sha256'],'gitBlob':c['gitBlob'],'promptRecord':c['prompt_record'],'generator':c['tool'],'AI_generated':True,'embeddedC2PA':c['embedded_c2pa_present'],'C2PASignatureValidated':False,'status':'original final PNG and exact final-generation/edit prompts archived'},
            'retainedInputs':inputs,
            'licenses':[
                {'id':'Blender-Human-Base-Meshes-1.4.1','license':'CC0-1.0','role':'inherited anatomy in applicable native bases and shared rigs','evidence':'ArtSource/Heroes/Concepts/Licenses/blender-base-source.json','status':'official version-specific listing verified; original bundle README still not retrieved'},
                {'id':'OpenAI-output','role':'new AI concept images and code-assisted design work','license':'OpenAI Europe Terms of Use; not a CC0 declaration','source':'https://openai.com/policies/eu-terms-of-use/','termsUpdated':'2026-01-16','checked':AS_OF,'status':'Content clauses verified; output rights subject to terms and applicable law'},
                {'id':'locomotion-CC0','role':'shared movement animation bank','license':'CC0-1.0','evidence':['ArtSource/Animations/Locomotion/License-KayKit.txt','ArtSource/Animations/Locomotion/License-Quaternius.txt','ArtSource/Animations/Locomotion/README.md'],'status':'original provider notices already preserved'},
                {'id':'combat-Quaternius','role':'combat animation source bank','license':'CC0-1.0 declared for free Standard packages','evidence':'ArtSource/Animations/README.md','status':'source mapping documented; separate Mixamo idle must not be labeled CC0'},
                {'id':'Mixamo-idle','role':'standing idle for seven heroes, excluding Sivra; applicability to Timmy not asserted','license':'Adobe/Mixamo terms, not CC0','evidence':['ArtSource/Animations/README.md','ArtSource/Animations/mixamo-standing-idle-report.json'],'status':'user-supplied source and mapping recorded; exact download-license evidence not archived'},
                {'id':'hero-audio','role':'basic/ability/death effects','evidence':['Docs/ThirdPartyAudioLicenses.md','ArtSource/Audio/Combat/sources.json','ArtSource/Audio/HeroSelections/manifest.json'],'status':'mixed: some preserved CC0 notices; selected recordings have unresolved source/license evidence'},
            ],
            'implementation':{'fullConceptModel':'pending','previousValidatedReplacement':PARTS.get(key),'previousValidation':'ArtSource/Heroes/PartsRedesign/validation-report.json' if key in PARTS else None},
            'openEvidence':['full new-concept model and render/FBX validation','historical user-supplied design-reference image sources were not recovered','per-hero portraits, ability icons, VFX/textures and skins require separate asset-level coverage audit','unresolved selected-audio source and license records'],
        }
        if key=='Timmy':record['licenses']=[x for x in record['licenses'] if x['id'] not in ('Mixamo-idle','Blender-Human-Base-Meshes-1.4.1')];record['releaseFlagChanged']=False
        out=DIRECTORY/'Provenance';out.mkdir(exist_ok=True);(out/(key+'.json')).write_text(json.dumps(record,indent=2)+'\n');records.append(record)
    rows=[]
    for r in records:
        rows.append(f"| {r['hero']} | [{r['key']}](../ArtSource/Heroes/Concepts/Provenance/{r['key']}.json) | gespeichert | {r['implementation']['previousValidatedReplacement'] or 'keine neue Umsetzung validiert'} | offen |")
    text='''# Herkunft und Lizenzstand der neun Helden

Stand: 12. Oktober 2026. Diese Bestandsaufnahme bestätigt vorhandene Belege und benennt fehlende Nachweise. Sie ist keine pauschale Erklärung, dass sämtliche Heldenassets abschließend geklärt sind.

Alle neun neuen Konzeptbilder sind als unveränderte Original-PNGs archiviert. `manifest.json` enthält SHA-256, Git-Blob-ID, Erzeugungswerkzeug und eingebettete Zeitangaben; `Tools/HeroConcepts/prompts.json` enthält die Entwurfs- und Änderungsanweisungen. Iones gültige Fassung ist **v2 mit nach vorne gerichteter Waffe**. Alte Fassungen bleiben im Archiv. Eingebettete C2PA-Signaturen wurden nicht kryptographisch geprüft.

| Held | Detailnachweis | Neues Konzept | Bisherige Modelländerung | Vollständige Konzeptumsetzung |
| --- | --- | --- | --- | --- |
'''+ '\n'.join(rows)+'''

Die vollständige Gestaltung aller neun 3D-Modelle nach diesen Konzepten ist noch nicht abgeschlossen. Die vier genannten Teile sind bereits als echte Geometrie gebaut und mit Blender/FBX geprüft. Das ersetzt keinen Unity- oder Gerätetest.

## Nachgewiesene Quellen und Grenzen

- **Blender Human Base Meshes 1.4.1:** Die offizielle [Blender Demo Files-Seite](https://www.blender.org/download/demo-files/) nennt dieses konkrete Paket CC0. Das versionierte Listing wurde geprüft und kurz dokumentiert. Der ursprüngliche Paket-README-Download ist weiterhin offen (403). Dieser Punkt betrifft die Archivierung der Originaldatei, nicht eine Behauptung, dass CC0 unbekannt wäre. Bestehende Anatomie wird weiterhin als übernommen ausgewiesen.
- **Neue Konzepte:** KI-generierte Entwürfe mit OpenAI image_gen. Die [Europe Terms of Use](https://openai.com/policies/eu-terms-of-use/), Fassung vom 16. Januar 2026, regeln die Rechte an Output gegenüber OpenAI. Ein Konzept mit Prompt und Hash belegt die Entstehung; es garantiert keine Exklusivität und klärt keine Rechte an früheren Referenzen.
- **Bewegung:** Die originalen CC0-Hinweise von KayKit und Quaternius sind vorhanden. Die zusätzlich vom Nutzer gelieferte Mixamo-Standanimation ist eine andere Quelle und wird ausdrücklich nicht als CC0 geführt. Ihr konkreter Download-/Lizenzbeleg fehlt noch.
- **Sounds:** Für Kenney und einen Teil der OpenGameArt-Effekte existieren Quellenzuordnung und CC0-Belege. Bei mehreren später ausgewählten Aufnahmen sind Herkunft und Lizenz noch nicht bestätigt. „Kostenlos heruntergeladen“ wird nicht als Lizenznachweis eingetragen.
- **Weitere Assets:** Alte Referenzbilder, Porträts, Fähigkeitssymbole, VFX, Texturen und Skins benötigen noch eine vollständige Zuordnung pro Datei. Neue Konzeptbilder schließen diese historischen Lücken nicht automatisch.

## Aufbewahrung und Wiederherstellung

Der Git-Verlauf bewahrt Konzepte, Prompts, Quellenverweise, Quelldateien, Exporte und Prüfberichte. Die JSON-Datei je Held unterscheidet Entwurf, übernommene Basis und Modellumsetzung. Nach erfolgreich gebauter und geprüfter vollständiger Geometrie muss der jeweilige Umsetzungsstatus mit den tatsächlich erzeugten Dateien und Hashes aktualisiert werden. Keine rückwirkend erfundenen Entwürfe oder Herkunftsangaben.
'''
    (ROOT/'Docs/HeroProvenanceStatus.md').write_text(text)
    print('PROVENANCE',len(records),'concepts; full model implementation pending',flush=True)

if __name__=='__main__':main()
