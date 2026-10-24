// Add transport for the existing clan engine to a fresh export of the live Node source.
// This deliberately preserves its catalog, receipts, authentication and billing code.
import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export async function attachClans(source) {
  if (source.includes('DF_CLAN_BRIDGE_BEGIN')) throw new Error('Clan bridge is already present; inspect that release instead of patching twice.');
  for (const anchor of ['const db = new Firestore();', 'async function authenticateUnity(',
    'async function success(', 'async function saveProfile(', "http(\n  'helloHttp',"]) {
    if (source.split(anchor).length !== 2) throw new Error('Unexpected Node source layout: ' + anchor);
  }
  const unsupported = /if \(req\.path\.startsWith\('\/social\/'\)\) \{\s*throw new HttpError\(503, 'social_feature_not_deployed'\);\s*\}/g;
  if ([...source.matchAll(unsupported)].length !== 1) throw new Error('Expected unsupported-social route was not found exactly once.');
  const credential = /await authenticateUnity\(\s*req\s*\);/g;
  if ([...source.matchAll(credential)].length !== 1) throw new Error('Cannot locate the existing verified-authentication boundary.');
  const saveStart = source.indexOf('await saveProfile(', source.indexOf("http(\n  'helloHttp',"));
  if (saveStart < 0 || source.indexOf('await saveProfile(', saveStart + 1) >= 0) throw new Error('Cannot locate the existing profile-save call exactly once.');
  // Only parentheses inside this known call are traversed; reject strings/comments
  // rather than guessing when the current export has a different implementation.
  let depth=1, end=saveStart+'await saveProfile('.length;
  for (; end<source.length && depth; end++) {
    if (/["'`\/]/.test(source[end])) throw new Error('Profile call needs a manual review; no source written.');
    if (source[end]==='(')depth++;
    if (source[end]===')')depth--;
  }
  if (depth) throw new Error('Unbalanced profile-save call.');
  let patched = source.slice(0,saveStart) + 'await dfClanProfileSaved(req, ' +
    source.slice(saveStart+'await '.length,end) + ')' + source.slice(end);
  patched = patched.replace(unsupported, 'if (req.path.startsWith(\'/social/\')) {\n        return await dfClanReply(req, res, accountHash);\n      }');
  patched = patched.replace(credential, match => match + "\n      if (!req.path.startsWith('/social/')) await dfClanAccess(req);");
  const revision=/const SERVER_REVISION = '[^']+';/g;
  if ([...patched.matchAll(revision)].length !== 1) throw new Error('Expected server revision marker is missing or ambiguous.');
  patched=patched.replace(revision,"const SERVER_REVISION = '2026-10-24.clans.1';");
  const proxy=await readFile(new URL('./clan-proxy.inc.js',import.meta.url),'utf8');
  return patched.replace("http(\n  'helloHttp',",proxy+"\n\nhttp(\n  'helloHttp',");
}

if (process.argv[1] && resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  const [input,output]=process.argv.slice(2);
  if (!input||!output||resolve(input)===resolve(output)) throw new Error('Pass distinct current-index.js and repaired-index.js paths.');
  const before=await readFile(input,'utf8');
  const after=await attachClans(before);
  await writeFile(output,after,{flag:'wx'});
  console.log('Prepared clan transport from the supplied Node source. Input unchanged. Paired Python runtime and existing namespace are required; nothing deployed.');
}
