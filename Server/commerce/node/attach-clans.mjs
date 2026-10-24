// Add transport for the existing clan engine to a fresh export of the live Node source.
// This deliberately preserves its catalog, receipts, authentication and billing code.
import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export async function attachClans(source) {
  if (source.includes('DF_CLAN_BRIDGE_BEGIN')) throw new Error('Clan bridge is already present; inspect that release instead of patching twice.');
  // Cloud Console exports the same application with either quote style and can
  // omit new-expression parentheses. Match only the reviewed, argument-free
  // Firestore constructor; project/database overrides still require review.
  const database = /\bconst\s+db\s*=\s*new\s+Firestore\s*(?:\(\s*\))?\s*;/g;
  if ([...source.matchAll(database)].length !== 1) throw new Error('Unexpected Node source layout: default Firestore constructor.');
  for (const name of ['authenticateUnity', 'success', 'saveProfile']) {
    const definition = new RegExp('\\basync\\s+function\\s+' + name + '\\s*\\(', 'g');
    if ([...source.matchAll(definition)].length !== 1) throw new Error('Unexpected Node source layout: ' + name);
  }
  const entry = /\bhttp\s*\(\s*(['"])helloHttp\1\s*,/g;
  const entries = [...source.matchAll(entry)];
  if (entries.length !== 1) throw new Error('Unexpected Node source layout: helloHttp entry point.');
  const unsupported = /if\s*\(\s*req\.path\.startsWith\(\s*(['"])\/social\/\1\s*\)\s*\)\s*\{\s*throw\s+new\s+HttpError\(\s*503\s*,\s*(['"])social_feature_not_deployed\2\s*\)\s*;?\s*\}/g;
  if ([...source.matchAll(unsupported)].length !== 1) throw new Error('Expected unsupported-social route was not found exactly once.');
  const credential = /await\s+authenticateUnity\(\s*req\s*\)\s*;/g;
  if ([...source.matchAll(credential)].length !== 1) throw new Error('Cannot locate the existing verified-authentication boundary.');
  // Preserve the supplied call verbatim. A changed argument list is deliberately
  // rejected instead of wrapping an unreviewed save implementation.
  const saves = [...source.matchAll(/await\s+saveProfile\(\s*accountHash\s*,\s*body\.profile\s*,\s*body\.expectedProfileRevision\s*\)/g)]
    .filter(match => match.index > entries[0].index);
  if (saves.length !== 1) throw new Error('Cannot locate the existing profile-save call exactly once.');
  const save = saves[0];
  let patched = source.slice(0,save.index) + 'await dfClanProfileSaved(req, ' +
    save[0].replace(/^await\s+/, '') + ')' + source.slice(save.index+save[0].length);
  patched = patched.replace(unsupported, 'if (req.path.startsWith(\'/social/\')) {\n        return await dfClanReply(req, res, accountHash);\n      }');
  patched = patched.replace(credential, match => match + "\n      if (!req.path.startsWith('/social/')) await dfClanAccess(req);");
  const revision=/\bconst\s+SERVER_REVISION\s*=\s*(['"])[^'"\r\n]+\1\s*;/g;
  if ([...patched.matchAll(revision)].length !== 1) throw new Error('Expected server revision marker is missing or ambiguous.');
  patched=patched.replace(revision,"const SERVER_REVISION = '2026-10-24.clans.1';");
  const proxy=await readFile(new URL('./clan-proxy.inc.js',import.meta.url),'utf8');
  return patched.replace(entry, match => proxy + '\n\n' + match);
}

if (process.argv[1] && resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  const [input,output]=process.argv.slice(2);
  if (!input||!output||resolve(input)===resolve(output)) throw new Error('Pass distinct current-index.js and repaired-index.js paths.');
  const before=await readFile(input,'utf8');
  const after=await attachClans(before);
  await writeFile(output,after,{flag:'wx'});
  console.log('Prepared clan transport from the supplied Node source. Input unchanged. Paired Python runtime and existing namespace are required; nothing deployed.');
}
