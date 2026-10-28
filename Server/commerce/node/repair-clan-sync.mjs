// Incremental repair for the existing standalone Node service; no storage migration.
import {readFileSync,writeFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {repairChatIndexError} from './repair-chat-index-error.mjs';

export function repairClanSync(source){
  if(!source.includes('// DF_STANDALONE_CLANS_BEGIN v1'))throw new Error('Expected the existing standalone clan backend.');
  const start=source.indexOf('function validateProfile('),end=source.indexOf('function sameEntry(',start);
  if(start<0||end<=start||source.indexOf('function validateProfile(',start+1)>=0)throw new Error('Profile validator changed; inspect the current server source.');
  let validator=source.slice(start,end);
  if(!validator.includes('DF_DailyQuestWonMaps_v1'))throw new Error('Daily map validation was not found.');
  const oldMask=/\(\s*1\s*<<\s*11\s*\)\s*-\s*1/g,newMask=/\(\s*1\s*<<\s*14\s*\)\s*-\s*1/g;
  const oldCount=[...validator.matchAll(oldMask)].length,newCount=[...validator.matchAll(newMask)].length;
  if(oldCount===1&&newCount===0)validator=validator.replace(oldMask,'(1 << 14) - 1');
  else if(oldCount!==0||newCount!==1)throw new Error('Unexpected map mask; inspect the current validator.');
  let corrected=repairChatIndexError(source.slice(0,start)+validator+source.slice(end));
  const revision=/\bconst\s+SERVER_REVISION\s*=\s*(['"])[^'"\r\n]+\1\s*;/g;
  if([...corrected.matchAll(revision)].length!==1)throw new Error('Expected one server revision marker.');
  return corrected.replace(revision,"const SERVER_REVISION = '2026-10-28.clans.sync.1';");
}

if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  if(process.argv.length!==4||resolve(process.argv[2])===resolve(process.argv[3]))throw new Error('Usage: node repair-clan-sync.mjs current-index.js corrected-index.js (distinct paths)');
  writeFileSync(process.argv[3],repairClanSync(readFileSync(process.argv[2],'utf8')),{flag:'wx'});
  console.log('Prepared the 14-map profile validator and chat index diagnostic. Nothing deployed.');
}
