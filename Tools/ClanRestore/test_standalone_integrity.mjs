import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {attachStandaloneClans} from '../../Server/commerce/node/attach-standalone-clans.mjs';
let checks=0;
for(const name of ['node-2026-10-22.2.js','node-2026-10-22.2-cloud-export.js']){
  const before=await readFile(new URL('./fixtures/'+name,import.meta.url),'utf8'),after=await attachStandaloneClans(before);
  const names=[...before.matchAll(/\b(?:async\s+)?function\s+([A-Za-z_$][\w$]*)\s*\(/g)].map(m=>m[1]);
  function inspect(source){
    const code=source.replace(/\bimport\s*(?:\{[^}]*\}|[A-Za-z_$][\w$]*)\s*from\s*(['"])[^'"]+\1\s*;?/g,'');
    return new Function('http','Firestore','GoogleAuth','createRemoteJWKSet',code+'\nreturn Object.fromEntries('+JSON.stringify(names)+'.map(name=>[name,eval(name).toString()]));')(()=>{},class{},class{},()=>({}));
  }
  const original=inspect(before),candidate=inspect(after);
  for(const name of names){assert.equal(candidate[name],original[name],'Existing function changed: '+name);checks++;}
  const catalog=s=>s.slice(/\bconst\s+ITEMS\s*=\s*\{/.exec(s).index,/\bconst\s+db\s*=\s*new\s+Firestore/.exec(s).index);
  assert.equal(catalog(after),catalog(before));checks++;
  assert.ok(!after.includes('127.0.0.1:8081')&&!after.includes('gunicorn')&&!after.includes('child_process'));checks++;
  assert.ok(after.includes("DF_CLAN_NAMESPACE='dimensions-front'"));checks++;
  assert.ok(after.includes("'2026-10-24.clans.node.1'"));checks++;
  await assert.rejects(attachStandaloneClans(after),/already present/);checks++;
  const withHat=before.replace(/const\s+ITEMS\s*=\s*\{/,match=>match+"\n future_hat: item(100,[w('DF_HeadwearOwned_Future')]),");
  assert.equal(catalog(await attachStandaloneClans(withHat)),catalog(withHat));checks++;
}
console.log(`Standalone index: ${checks} original-function, catalog and single-file checks passed for both exports.`);
