// Exercise the uploaded profile validator/save path with a controlled transaction.
// No production SDK, token, database or Google service is used.
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import {repairClanSync} from './repair-clan-sync.mjs';
import {repairChatIndexError} from './repair-chat-index-error.mjs';

const [beforePath,afterPath]=process.argv.slice(2);
if(!beforePath||!afterPath)throw new Error('Usage: node test-profile-sync-export.mjs original.js corrected.js');
const before=readFileSync(beforePath,'utf8'),after=readFileSync(afterPath,'utf8');
assert.equal(repairClanSync(before),after,'Corrected export must contain only the guarded patch');
assert.equal(repairClanSync(after),after,'The patch must remain idempotent');

function accountApi(source){
  const walletStart=source.indexOf('function defaultPlayer('),walletEnd=source.indexOf('const RANKING_PAGE_SIZE=',walletStart);
  const start=source.indexOf('function isProfileKey('),end=source.indexOf('function validOperationId(',start);
  assert(walletStart>=0&&walletEnd>walletStart&&start>=0&&end>start);
  const state={document:null,reads:0,writes:0};
  class HttpError extends Error{constructor(status,code){super(code);this.status=status;}}
  const db={async runTransaction(action){
    let queued;
    const tx={async get(ref){state.reads++;assert.equal(ref.id,'fixture-account');return {exists:true,data:()=>structuredClone(state.document)};},
      set(ref,value,options){assert.equal(ref.id,'fixture-account');state.writes++;queued=options?.merge?{...state.document,...structuredClone(value)}:structuredClone(value);}};
    const result=await action(tx);if(queued)state.document=queued;return result;
  }};
  const context={db,HttpError,MAX_BALANCE:2147483647,ITEMS:{},PLAYER_NAME_KEY:'DF_PlayerName',
    FieldValue:{serverTimestamp:()=>({fixtureTimestamp:true})},playerRef:id=>({id}),
    reservePlayerName:async()=>({name:'FixturePilot0000001',needsClaim:false})};
  vm.createContext(context);
  vm.runInContext(source.slice(walletStart,walletEnd)+'\n'+source.slice(start,end)+'\nthis.save=saveProfile;',context);
  return {state,save:context.save};
}
const entry=(key,number)=>({key,kind:0,number});
const profile=mask=>[entry('DF_DailyQuestWonMaps_v1',mask),entry('DF_MatchesCompleted',18),entry('DF_MatchesWon',12),
  entry('DF_Shards',129),{key:'DF_AccountXP',kind:1,text:'1800'},{key:'DF_PlayerName',kind:1,text:'FixturePilot0000001'}];
const seed=()=>({balance:600,revision:7,profileRevision:10,profile:profile(1).sort((a,b)=>a.key.localeCompare(b.key)),owned:['avatar_10'],privateReceiptMarker:'preserve-me'});
const old=accountApi(before);
old.state.document=seed();
await assert.rejects(old.save('fixture-account',profile(1<<11),10),/invalid_profile/);
assert.equal(old.state.reads,0);assert.equal(old.state.writes,0);
assert.equal(old.state.document.profileRevision,10);
let cases=0;
for(const mask of [...Array.from({length:14},(_,i)=>1<<i),16383]){
  const fixed=accountApi(after);fixed.state.document=seed();
  const wallet=await fixed.save('fixture-account',profile(mask),10);
  const values=new Map(wallet.profile.map(e=>[e.key,e]));
  assert.equal(values.get('DF_DailyQuestWonMaps_v1').number,mask);
  assert.equal(values.get('DF_MatchesCompleted').number,18);
  assert.equal(values.get('DF_MatchesWon').number,12);
  assert.equal(values.get('DF_AccountXP').text,'1800');
  assert.equal(values.get('DF_Shards').number,129);
  assert.equal(wallet.balance,600);assert.equal(wallet.revision,7);
  assert.equal(wallet.profileRevision,mask===1?10:11);
  assert.equal(wallet.accountHash,'fixture-account');
  assert.deepEqual(Array.from(wallet.owned),['avatar_10']);
  assert.equal(fixed.state.document.privateReceiptMarker,'preserve-me');cases++;
}
const conflict=accountApi(after);conflict.state.document=seed();
await assert.rejects(conflict.save('fixture-account',profile(1<<13),9),e=>e.status===409);
assert.equal(conflict.state.writes,0);
const invalid=accountApi(after);invalid.state.document=seed();
await assert.rejects(invalid.save('fixture-account',profile(1<<14),10),/invalid_profile/);
assert.equal(invalid.state.writes,0);
assert.equal(after.includes("code:'CHAT_INDEX_REQUIRED'"),true);
assert.equal(repairChatIndexError(after),after);
console.log('PASS: current export reproduced the old rejection; '+cases+' real save-path cases preserve wins, XP, balances, entitlements and metadata; conflict and invalid-mask guards retained.');
