"""Create an evidence register from the pinned repository inventory, not guessed rights."""
from pathlib import Path
import json, re, hashlib, collections
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Documentation/Provenance'
SNAP=json.loads((OUT/'RepositorySnapshot.json').read_text())
FILES=SNAP['files']
DATE='2026-10-12'
def read(path): return json.loads((ROOT/path).read_text())
def write(path,data):
    p=ROOT/path;p.parent.mkdir(parents=True,exist_ok=True)
    p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
sources=[]
def source(id,title,creator,license,url,evidence,status='project-record-and-provider-declaration',notes=''):
    sources.append(dict(id=id,title=title,creator=creator,license=license,sourceUrl=url,evidence=evidence,checkedOn=DATE,status=status,notes=notes))
source('blender-anatomy','Human Base Meshes 1.4.1','Blender Studio / individual mesh authors','CC0-1.0','https://www.blender.org/download/demo-files/',['Tools/HeroRebuild/README.md'],'project-record; upstream original bundle notice to be archived','Anatomy is third-party. Costumes/weapons/rig edits are documented project work, not evidence that the anatomy was built from scratch.')
source('quaternius-combat','Universal Animation Library 1 and 2, free Standard','Quaternius','CC0-1.0','https://quaternius.itch.io/universal-animation-library',['ArtSource/Animations/README.md','ArtSource/Animations/Quaternius-CC0.txt'])
source('quaternius-locomotion','Universal Animation Library Standard','Quaternius','CC0-1.0','https://quaternius.com/packs/universalanimationlibrary.html',['ArtSource/Animations/Locomotion/README.md','ArtSource/Animations/Locomotion/License-Quaternius.txt'])
source('kaykit','Character Animations 1.1','Kay Lousberg','CC0-1.0','https://kaylousberg.itch.io/kaykit-character-animations',['ArtSource/Animations/Locomotion/README.md','ArtSource/Animations/Locomotion/License-KayKit.txt'])
source('mixamo','User-supplied Standing Idle (1).fbx','Adobe Mixamo','Mixamo terms; NOT CC0','https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html',['ArtSource/Animations/README.md','ArtSource/Animations/mixamo-standing-idle-report.json'],'source documented; original user download receipt not available','A provider declaration and a source-file digest are distinct from the account download record. No claim of CC0 or permission to resell standalone source files.')
for name,slug in [('SciFi','sci-fi-sounds'),('Impact','impact-sounds'),('RPG','rpg-audio')]:
    source('kenney-'+name.lower(),name+' audio','Kenney','CC0-1.0','https://kenney.nl/assets/'+slug,['Docs/AudioLicenses/Kenney-'+name+'-CC0.txt','Docs/ThirdPartyAudioLicenses.md','ArtSource/Audio/Combat/sources.json'])
source('firearm','The Free Firearm Sound Library','Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney','CC0-1.0','https://opengameart.org/content/the-free-firearm-sound-library',['Docs/ThirdPartyAudioLicenses.md','ArtSource/Audio/Combat/sources.json'])
for id,title,slug in [('music-menu','Another space background track','another-space-background-track'),('music-match','Space Music: Out There','space-music-out-there')]:
    source(id,title,'yd','CC0-1.0','https://opengameart.org/content/'+slug,['Docs/ThirdPartyAudioLicenses.md'])
source('camo','Pixelated Camouflage Set / camouflage-grey.png','Angela Netzke (netzapps)','CC0-1.0','https://opengameart.org/content/pixelated-camouflage-set',['Documentation/UrbanDigitalCamo-LICENSE.md'])
source('wolfwoot','Archived RPG Male Adventurer voice','Brandon Song (wolfwoot)','CC0-1.0','https://opengameart.org/content/voice-clip-pack-male-adventurer-rpg',['Docs/HeroDeathVoiceLicense.md'],'archived; no longer active HeroDeath cue')
source('mixkit','Open special box, asset 3190','Mixkit listing','Mixkit Sound Effects Free License','https://mixkit.co/license/modal/sfxFree/',['Docs/HeroDeathVoiceLicense.md'],'license page verified; source identity recorded in project','Finished-game incorporation and standalone/source-file redistribution are different permissions. Raw source is retained in this private repository.')
source('spacekit','Ultimate Space Kit','Quaternius','CC0 claimed; pack-specific notice mismatch','https://quaternius.com/packs/ultimatespacekit.html',['Assets/Alterverse/ThirdParty/UltimateSpaceKit_LICENSE.txt'],'needs exact archive notice','The existing file is named UltimateSpaceKit but its text says Ultimate Platformer Pack. Do not silently treat the wrong pack heading as an exact pack certificate.')
for name in ['Rajdhani','Orbitron']:
    source('font-'+name.lower(),name,'Indian Type Foundry' if name=='Rajdhani' else 'The Orbitron Project Authors','OFL-1.1','https://github.com/google/fonts/tree/main/ofl/'+name.lower(),['Documentation/Provenance/Licenses/'+name+'-OFL-1.1.txt'],'license text archived; font name-table checked','DimensionsDisplay-SemiBold.ttf identifies itself as a renamed Orbitron derivative in its copyright/name table.' if name=='Orbitron' else 'Rajdhani-Regular.ttf and Rajdhani-SemiBold.ttf retain their original copyright and OFL metadata.')
source('google-play-games','Play Games plugin v2.2.1','Google','Apache-2.0','https://github.com/playgameservices/play-games-plugin-for-unity/tree/v2.2.1',['Documentation/Provenance/Licenses/GooglePlayGames-Apache-2.0.txt','Packages/manifest.json'])
source('google-edm','External Dependency Manager v1.2.188','Google','Apache-2.0','https://github.com/googlesamples/unity-jar-resolver/tree/v1.2.188',['Documentation/Provenance/Licenses/ExternalDependencyManager-Apache-2.0.txt','Packages/packages-lock.json'])
PIX=[
('337205','fighter-jet-powerful-low-flyby-sfx','MIGHTUSER'),('100653','heathers-gunshot-effect2','okieactor (Freesound), via freesound_community'),
('426481','big-robot-footstep-009','AudioPapkin'),('445104','big-robot-footstep-014','AudioPapkin'),('416172','boom-swoosh-01','DRAGON-STUDIO'),
('478377','elemental-spell-impact-water','DRAGON-STUDIO'),('393845','sword-slice-2','DRAGON-STUDIO'),('393847','sword-slice','DRAGON-STUDIO'),
('234631','swing-whoosh-metal-hit-14','floraphonic'),('234636','swing-whoosh-metal-hit-19','floraphonic'),
('101878','funny-swish','deleted_user_6479820 (Freesound), via freesound_community'),('7103','power-down','noirenex (Freesound), via freesound_community'),
('106252','steel-chain-dragged-shower-reverb','ani_music (Freesound), via freesound_community'),('442015','cinematic-hit-shot','SoundReality'),('312659','sci-fi-gun-shot','SoundReality'),
('229313','sci-fi-blaster-shot','Universfield'),('378663','healing-magic-2','Yodguard'),('482495','short-energy-beam-shot-1','Yodguard'),('482500','short-energy-beam-shot-4','Yodguard'),
('352708','sword-blade-slicing-flesh','Universfield'),('202030','sci-fi-sound-effect-designed-circuits-sfx-29','GregorQuendel'),('450253','a-massive-axe-impact-hitting-metal-2','Yodguard')]
for id,slug,creator in PIX:
    category='technology' if id=='482495' else 'film-special-effects'
    url='https://pixabay.com/sound-effects/'+category+'-'+slug+'-'+id+'/'
    if id in ['100653','450253']:url='https://pixabay.com/sound-effects/'+slug+'-'+id+'/'
    source('pixabay-'+id,slug,creator,'Pixabay Content License; NOT CC0',url,['Documentation/Provenance/Pixabay-Evidence.json','ArtSource/Audio/HeroSelections/manifest.json','ArtSource/Audio/HeroSelections/README.md'],'provider page located and license declaration observed; user download certificate not archived','The supplied filename/id links the local recording to this listing. Provider-file byte identity was not independently established. Account-specific download dates/certificates are not invented.')
for id,title,paths in [('universe-image','User-supplied Universe.png',['Documentation/Universe-Backdrop.md']),('reference-images','Historical character reference images',['Tools/HeroRebuild/README.md','ArtSource/Heroes/UmbralMage/README.md','Documentation/CyberShark.md','Docs/AndromedaTimmy-Season2.md']),('frontend-video','Interstellar menu video/poster and branding',['Docs/Production-Frontend-Pass.md'])]:
    source(id,title,'not yet established','not established','',paths,'open provenance gap','An upload or project commit is not by itself a license grant. Keep generated concepts dated separately from historical references.')
write('Documentation/Provenance/LicenseRegister.json',{'checkedOn':DATE,'scope':'all repository media assets plus package lock; evidence register, not blanket clearance','sources':sources})
write('Documentation/Provenance/Pixabay-Evidence.json',{'checkedOn':DATE,'licenseUrl':'https://pixabay.com/service/license-summary/','bindingTermsUrl':'https://pixabay.com/service/terms/','verification':'Public listing title/creator/item id and Content License designation checked with web retrieval. No account download certificate or historical license snapshot is fabricated.','items':[s for s in sources if s['id'].startswith('pixabay-')]})
assets=[]
extensions={'.png','.jpg','.jpeg','.mp4','.wav','.mp3','.ogg','.ttf','.otf','.fbx','.obj','.bytes','.blend','.gz'}
selected_manifest=read('ArtSource/Audio/HeroSelections/manifest.json')
source_hashes={e['source']:e['sourceSha256'] for e in selected_manifest}
combat={e['file']:e for e in read('ArtSource/Audio/Combat/sources.json')}
for f in FILES:
    p=f['path'];suffix=Path(p).suffix.lower()
    if suffix not in extensions:continue
    refs=[];status='needs asset-specific source mapping';note=''
    name=Path(p).name
    if p.startswith('ArtSource/Audio/Combat/') and name in combat:
        e=combat[name];refs=[{'sourceUrl':e.get('url'),'license':e.get('license'),'recordedOriginalSha256':e.get('original_sha256')}];status='exact input mapped by existing source manifest'
    elif 'Rajdhani' in name:refs=['font-rajdhani'];status='font metadata and notice'
    elif 'DimensionsDisplay' in name:refs=['font-orbitron'];status='renamed derivative; metadata and notice'
    elif p.endswith('StandingIdle.fbx.gz'):refs=['mixamo'];status='source and mapping recorded; original user receipt missing'
    elif p.startswith('ArtSource/Animations/'):
        refs=['kaykit' if 'KayKit' in name else 'quaternius-locomotion' if 'Movement' in name else 'quaternius-combat'];status='provider/source documented'
    elif 'UrbanDigitalCamo' in name:refs=['camo'];status='exact source and recorded SHA-256'
    elif 'HeroDeathSpecialBox' in name:refs=['mixkit'];status='source hash and listing recorded'
    elif 'HeroDeathHa' in name:refs=['wolfwoot'];status='archived source'
    elif name=='Universe.png':refs=['universe-image'];status='open license/source gap'
    elif 'InterstellarBackdrop' in name:refs=['frontend-video'];status='open source/account evidence gap'
    elif '/MapPanoramas/' in p:refs=['Documentation/Map-Photo-Panoramas.md'];status='AI generation directions documented; exact historical generation record not complete'
    elif p.startswith('ArtSource/Heroes/') or '/AuthoredHeroes/' in p:
        refs=['Tools/HeroRebuild/README.md','HistoricalCommits.json'];status='project model lineage documented; historical visual reference rights unresolved'
        if 'Timmy' not in p and 'CyberShark' not in p:refs+=['blender-anatomy']
    elif re.search(r'/(CyberRanger|UmbralMage|SiegeTank|ChronoTraveler|CyberShark)/Model.bytes$',p):
        refs=['Tools/'+p.split('/')[-2], 'HistoricalCommits.json'];status='procedurally authored project resource; historical reference identity unresolved'
    elif 'TimmySurface_' in p:refs=['Docs/AndromedaTimmy-Season2.md','Tools/AndromedaTimmy/build_timmy.py'];status='procedural atlas generation documented'
    elif '/PlanetSurfaces/' in p:refs=['Documentation/Blender-Visual-Refinement.md'];status='procedural material generation documented'
    for id,slug,creator in PIX:
        if re.search(r'(?<!\d)'+id+r'(?!\d)',name):refs=['pixabay-'+id];status='listing located; file relation based on supplied filename/id';break
    if '/Audio/' in p and '/Resources/' in p and not refs:
        refs=['Docs/CombatAudioAssets.json','Docs/ThirdPartyAudioLicenses.md','ArtSource/Audio/HeroSelections/README.md'];status='output requires current recipe/override resolution; do not assume all audio is CC0'
    e={**f,'sourceEvidence':refs,'status':status}
    if name in source_hashes:e['recordedOriginalSha256']=source_hashes[name]
    assets.append(e)
write('Documentation/Provenance/AssetRegister.json',{'snapshotCommit':SNAP['commit'],'inventoryCompleteForExtensions':sorted(extensions),'assetCount':len(assets),'hashType':'git blob SHA-1 from pinned GitHub tree; do not confuse with raw-file SHA-256','items':assets})
lock=read('Packages/packages-lock.json')
write('Documentation/Provenance/DependencyRegister.json',{'checkedOn':DATE,'dependencies':lock.get('dependencies',{}),'notice':'Unity package terms/embedded notices and user subscription entitlements must be retained from the installed package cache/account. Package lock proves version/source, not an account entitlement. Google upstream Apache notices are archived separately.'})
hist=read('Documentation/Provenance/HistoricalCommits.json')
heroes=[('Veyr','Zhaev Null','Zhaev-Null'),('Kaelon','Auren Kest','Auren-Kest'),('Nyra','Sivra Quell','Sivra-Quell'),('Elyra','Ione Vaska','Ione-Vaska'),('Mira','Tahla Venn','Tahla-Venn'),('Rook','Brakk Orun','Brakk-Orun'),('Traveler','THE TRAVELER','The-Traveler'),('Nerok','Nerok Abyss','Nerok-Abyss'),('Timmy','Andromeda Timmy','Andromeda-Timmy')]
types={'Nyra':'CyberRanger','Elyra':'UmbralMage','Kaelon':'SiegeTank','Traveler':'ChronoTraveler','Nerok':'CyberShark'}
for id,name,slug in heroes:
    lines=['# '+name+' — Herkunft und Entwurf','',f'Interne ID: `{id}`. Nachweissammlung erstellt am {DATE}.','', '## Zeitliche Einordnung','', 'Die Chronik wird aus vorhandenen Repository-Commits rekonstruiert. Ein Commitdatum ist kein Beweis für den Zeitpunkt einer früheren Idee. Neue Konzeptblätter werden nicht rückdatiert.','']
    hp='Tools/AndromedaTimmy/build_timmy.py' if id=='Timmy' else 'Tools/CyberShark/build_cyber_shark.py' if id=='Nerok' else 'Assets/Alterverse/Scripts/Visuals/StylizedHeroVisual.cs'
    h=list(reversed(hist[hp]))
    if id not in ['Timmy','Nerok']:h+=list(reversed(hist['Tools/HeroRebuild/build_heroes.py']))
    if id in ['Nyra','Elyra']:h+=list(reversed(hist['Tools/'+types[id]+'/build_'+('cyber_ranger' if id=='Nyra' else 'umbral_mage')+'.py']))
    if id in ['Nyra','Elyra','Mira','Nerok']:h+=hist['ArtSource/Heroes/PartsRedesign/Sivra-head.obj']
    unique={c['sha']:c for c in h};h=sorted(unique.values(),key=lambda c:c['date'])
    lines+=['| Commitdatum (UTC) | Entwicklungsschritt | GitHub-Beleg |','|---|---|---|']
    for c in h:lines.append(f"| {c['date']} | {c['message'].replace('|','/')} | [{c['sha'][:8]}](https://github.com/Lowperformence/Alterverse/commit/{c['sha']}) |")
    lines+=['','Die gemeinsame Builder-Chronik enthält auch Änderungen anderer Helden; nicht jede Zeile ist eine individuelle Änderung dieses Helden. Der jeweilige Diff ist der maßgebliche Beleg.','','## Bestandteile und Quellen','']
    if id=='Timmy':lines+=['Katzenkopf, Pfoten, Astronautenanzug und Fell-/Gewebeatlas: prozedurale Projektgeometrie gemäß `Tools/AndromedaTimmy/build_timmy.py` und `Docs/AndromedaTimmy-Season2.md`. Das gemeinsam verwendete Rig ist in der Projektchronik erfasst. Frühere Katzen-/Astronautenreferenzen sind erwähnt, ihre ursprünglichen Bildrechte jedoch noch nicht einzeln zugeordnet.']
    elif id=='Nerok':lines+=['Haikopf, Körper, Anker, Haken und Kleidung: Projektgenerator `Tools/CyberShark/build_cyber_shark.py`, Referenzkopf-Generator und PartsRedesign. Gemeinsamer Rook-Rig-/Template-Vertrag wird weiterverwendet; dies wird nicht als vollständig neu erfundenes Rig bezeichnet. Frühere Referenzbildrechte bleiben gesondert zu klären.']
    else:lines+=['Historische anatomische Basis: Blender Human Base Meshes 1.4.1, CC0 laut Originalprojekt-README. Kleidung, Waffen, Rig und Bearbeitungsschritte: Projektbuilder und Commit-Diffs. Bei mechanischen Ersatzkörpern wird die sichtbare Geometrie separat durch den jeweiligen Generator erzeugt; das importierte Rig-/Anatomie-Template bleibt Teil der Abhängigkeit.']
    lines+=['','Animationen: Quaternius und KayKit gemäß Lizenzregister; Mixamo-Ruhepose separat, ausdrücklich nicht CC0. Die tatsächliche Nutzung ist in `ArtSource/Animations/README.md` beschrieben. Sounds sind separat pro Quelle/Slot zugeordnet und nicht pauschal mit der Modelllizenz abgedeckt.','','## Neuer eigener Entwurf','',f'![{name}](../../../ArtSource/Heroes/Concepts/2026-10-12/{slug}.png)','',f'Bild: `ArtSource/Heroes/Concepts/2026-10-12/{slug}.png`. Datum: {DATE}. Vollständiger Prompt, Referenzrolle und Prüfsummen: `generation-records.json` im gleichen Ordner. Methode: eingebaute ChatGPT-Bildgenerierung. Die Referenz ist eine vorhandene Projekt-Modellvorschau; dieses Blatt ist eine neue Designrevision und kein ursprünglicher Vorabentwurf.','','## Modellzuordnung und Grenzen','',f'Basis-Unity-Resource: `Assets/Alterverse/Resources/AuthoredHeroes/{id}.fbx`.' if id!='Nerok' else 'Runtime-Resource: `Assets/Alterverse/Resources/CyberShark/Model.bytes`.']
    if id in types:lines +=[f'Aktive mechanische/sichtbare Ersatzgeometrie: `Assets/Alterverse/Resources/{types[id]}/Model.bytes`.']
    lines +=['Neue Quellen und geometrische Abgleichprotokolle: `ArtSource/Heroes/ConceptRebuild/`. Nur ein vorhandenes Build-/Validierungsprotokoll belegt einen abgeschlossenen Export. Konzeptbilder sind keine Ingame-Screenshots. Unity-/Android-Prüfung wird nicht durch ein Blender-Render ersetzt.','','Offen: historische Bildreferenzen und deren Nutzungsrechte; accountgebundene Downloadbelege soweit nicht verfügbar. Ein neuer Entwurf beseitigt diese historische Nachweislücke nicht.']
    p=OUT/'Heroes'/f'{slug}.md';p.parent.mkdir(parents=True,exist_ok=True);p.write_text('\n'.join(lines)+'\n')
counts=collections.Counter(a['status'] for a in assets)
print(json.dumps({'sources':len(sources),'assets':len(assets),'heroes':len(heroes),'assetStatuses':dict(counts)},ensure_ascii=False))
