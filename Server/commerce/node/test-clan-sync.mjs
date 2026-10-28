import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import {attachStandaloneClans} from './attach-standalone-clans.mjs';
import {repairClanSync} from './repair-clan-sync.mjs';
import {repairChatIndexError} from './repair-chat-index-error.mjs';

const validator=source=>{
  const start=source.indexOf('function isProfileKey('),end=source.indexOf('function sameEntry(',start);
  assert(start>=0&&end>start);
  class HttpError extends Error{constructor(status,code){super(code);this.status=status;}}
  const context={HttpError,MAX_BALANCE:2147483647,Date,Intl};vm.createContext(context);
  vm.runInContext(source.slice(start,end)+';this.validate=validateProfile;',context);return context.validate;
};
let checks=0;
for(const name of ['node-2026-10-22.2-cloud-export.js','node-2026-10-22.2.js']){
  const original=await attachStandaloneClans(readFileSync(new URL('../../../Tools/ClanRestore/fixtures/'+name,import.meta.url),'utf8'));
  const before=validator(original),corrected=repairClanSync(original),after=validator(corrected);
  const profile=mask=>[{key:'DF_DailyQuestWonMaps_v1',kind:0,number:mask}];
  assert.throws(()=>before(profile(1<<11)),/invalid_profile/);checks++;
  for(let map=0;map<14;map++){assert.equal(after(profile(1<<map))[0].number,1<<map);checks++;}
  for(const mask of [0,2047,16383]){assert.equal(after(profile(mask))[0].number,mask);checks++;}
  for(const mask of [-1,16384,32768,1.5,'2048',null]){assert.throws(()=>after(profile(mask)),/invalid_profile/);checks++;}
  assert.throws(()=>after([...profile(1),...profile(1)]),/invalid_profile/);checks++;
  assert.throws(()=>after([{key:'DF_CloudSecret',kind:0,number:1}]),/invalid_profile/);checks++;
  assert.throws(()=>after([{key:'DF_Shards',kind:0,number:-1}]),/invalid_profile/);checks++;
  assert.throws(()=>after([{key:'DF_LoginStreakIndex_v1',kind:0,number:8}]),/invalid_profile/);checks++;
  assert.equal(repairClanSync(corrected),corrected);checks++;
  // Reverse only the two intended source edits plus the diagnostic; everything else must be identical.
  const oldRevision=original.match(/\bconst\s+SERVER_REVISION\s*=\s*(['"])[^'"\r\n]+\1\s*;/)[0];
  const oldMask=original.slice(original.indexOf('function validateProfile('),original.indexOf('function sameEntry(')).match(/\(\s*1\s*<<\s*11\s*\)\s*-\s*1/)[0];
  const restored=corrected.replace("const SERVER_REVISION = '2026-10-28.clans.sync.1';",oldRevision).replace('(1 << 14) - 1',oldMask);
  assert.equal(restored,repairChatIndexError(original));checks++;
  assert.equal(repairClanSync(repairChatIndexError(original)),corrected);checks++;
}
assert.throws(()=>repairClanSync('unrelated source'));checks++;
console.log('PASS: '+checks+' checks for the reproduced 11-map failure, all 14 maps, invalid profiles, unchanged account code and idempotent patch.');
