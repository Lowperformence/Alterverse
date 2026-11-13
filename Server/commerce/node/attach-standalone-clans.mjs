// Produce one self-contained index.js for the existing Cloud Console Node editor.
import {readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';

export async function attachStandaloneClans(source){
  if(source.includes('DF_STANDALONE_CLANS_BEGIN'))throw new Error('Standalone clans are already present.');
  if(source.includes('DF_CLAN_BRIDGE_BEGIN'))throw new Error('Inspect the paired release instead of patching it as a plain Node export.');
  const database=/\bconst\s+db\s*=\s*new\s+Firestore\s*(?:\(\s*\))?\s*;/g;
  if([...source.matchAll(database)].length!==1)throw new Error('Unexpected default Firestore constructor.');
  for(const name of ['authenticateUnity','success','saveProfile'])if([...source.matchAll(new RegExp('\\basync\\s+function\\s+'+name+'\\s*\\(','g'))].length!==1)throw new Error('Unexpected account function: '+name);
  const entry=/\bhttp\s*\(\s*(['"])helloHttp\1\s*,/g,entries=[...source.matchAll(entry)];
  if(entries.length!==1)throw new Error('Unexpected helloHttp entry point.');
  const unsupported=/if\s*\(\s*req\.path\.startsWith\(\s*(['"])\/social\/\1\s*\)\s*\)\s*\{\s*throw\s+new\s+HttpError\(\s*503\s*,\s*(['"])social_feature_not_deployed\2\s*\)\s*;?\s*\}/g;
  if([...source.matchAll(unsupported)].length!==1)throw new Error('Expected unsupported-social route was not found exactly once.');
  const credential=/await\s+authenticateUnity\(\s*req\s*\)\s*;/g;
  if([...source.matchAll(credential)].length!==1)throw new Error('Cannot locate the verified-authentication boundary.');
  const saves=[...source.matchAll(/await\s+saveProfile\(\s*accountHash\s*,\s*body\.profile\s*,\s*body\.expectedProfileRevision\s*\)/g)].filter(m=>m.index>entries[0].index);
  if(saves.length!==1)throw new Error('Cannot locate the existing profile-save call exactly once.');
  const revision=/\bconst\s+SERVER_REVISION\s*=\s*(['"])[^'"\r\n]+\1\s*;/g;
  if([...source.matchAll(revision)].length!==1)throw new Error('Expected server revision marker is missing or ambiguous.');
  const fragments=await Promise.all(['standalone-data.inc.js','standalone-rules.inc.js','standalone-store.inc.js','standalone-delivery.inc.js'].map(name=>readFile(new URL('./'+name,import.meta.url),'utf8')));
  const save=saves[0];
  let prepared=source.slice(0,save.index)+'await dfClanProfileSaved(accountHash, '+save[0].replace(/^await\s+/,'')+')'+source.slice(save.index+save[0].length);
  prepared=prepared.replace(unsupported,"if (req.path.startsWith('/social/')) { return await dfClanReply(req, res, accountHash); }")
    .replace(credential,match=>match+"\nawait dfClanAccess(accountHash);")
    .replace(revision,"const SERVER_REVISION = '2026-10-24.clans.node.1';")
    .replace(entry,match=>';\n// DF_STANDALONE_CLANS_BEGIN v1\n'+fragments.join('\n')+'\n// DF_STANDALONE_CLANS_END v1\n'+match);
  if(prepared.includes('http://127.0.0.1:8081'))throw new Error('Standalone release contains a companion-service dependency.');
  return '// Standalone clan restoration. Source SHA-256: '+createHash('sha256').update(source).digest('hex')+'\n'+"import {randomBytes as dfRandomBytes,randomInt as dfRandomInt} from 'node:crypto';\n"+prepared;
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  const [input,output]=process.argv.slice(2);if(!input||!output||resolve(input)===resolve(output))throw new Error('Use distinct input and output paths.');
  await writeFile(output,await attachStandaloneClans(await readFile(input,'utf8')),{flag:'wx'});
  console.log('Prepared standalone Node index.js. Existing package.json remains valid. Nothing deployed.');
}
