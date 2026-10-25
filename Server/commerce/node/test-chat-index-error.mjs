import assert from 'node:assert/strict';
import vm from 'node:vm';
import {readFileSync} from 'node:fs';
import {repairChatIndexError} from './repair-chat-index-error.mjs';

const source=readFileSync(process.argv[2],'utf8'),corrected=repairChatIndexError(source);
const start=corrected.indexOf('async function dfClanReply('),end=corrected.indexOf('// Same durable report outbox',start);
assert(start>=0&&end>start);
let error;
class DfSocialError extends Error{}
const context={Buffer,DfSocialError,console:{error(){}},dfSocialDispatch:async()=>{throw error;}};
vm.createContext(context);vm.runInContext(corrected.slice(start,end)+';this.reply=dfClanReply',context);
const reply=async(path,failure)=>{
  error=failure;
  const result={set(){return this;},status(status){this.code=status;return this;},json(body){this.body=body;return this;}};
  await context.reply({path,body:{}},result,'caller');return result;
};
let result=await reply('/social/chat/read',{code:9,message:'The query requires an index: private-console-url'});
assert.equal(result.code,503);assert.equal(result.body.code,'CHAT_INDEX_REQUIRED');assert(!JSON.stringify(result.body).includes('private-console-url'));
result=await reply('/social/clan-levels',{code:9,message:'requires an index'});
assert.equal(result.body.error,'clan_service_unavailable');
result=await reply('/social/chat/read',{code:14,message:'Unavailable'});
assert.equal(result.body.error,'clan_service_unavailable');
assert.equal(repairChatIndexError(corrected),corrected,'idempotent patch');
assert.equal(corrected.replace(/    if\(req.path.startsWith\('\/social\/chat\/'\).*?\n    }\n/s,''),source,'no unrelated source changes');
assert.throws(()=>repairChatIndexError('unrelated source'));
console.log('PASS: standalone chat index error response, privacy, routing and source preservation.');
