import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {readFile} from 'node:fs/promises';
import vm from 'node:vm';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {once} from 'node:events';
import {attachClans} from '../../Server/commerce/node/attach-clans.mjs';

const sourceArgument=process.argv.find(value=>value.startsWith('--source='));
const original=await readFile(sourceArgument?.slice('--source='.length)||new URL('./fixtures/node-2026-10-22.2.js',import.meta.url),'utf8');
const patched=await attachClans(original);
let checks=0;
const check=(value,message)=>{assert.ok(value,message);checks++;};
const hash=value=>createHash('sha256').update(value).digest('hex');
const copy=value=>JSON.parse(JSON.stringify(value));
const alice=hash('alice');

const accountFunctions=['authenticateUnity','saveProfile','spendWallet','fulfillGooglePurchase','syncSubscription',
  'handleSubscriptionPush','reservePlayerName','readWallet','walletSnapshot','success'];
const beforeRuntime=runtime(original),afterRuntime=runtime(patched);
for(const name of accountFunctions){
  const before=beforeRuntime.functions[name],after=afterRuntime.functions[name];
  check(before&&before===after,'original account/billing function preserved byte for byte: '+name);
}
const catalog=source=>{
  const start=/\bconst\s+ITEMS\s*=\s*\{/.exec(source)?.index;
  const end=/\bconst\s+db\s*=\s*new\s+Firestore/.exec(source)?.index;
  assert.ok(start!==undefined&&end>start,'catalog boundaries must be present');
  return source.slice(start,end);
};
check(catalog(original)===catalog(patched),'entire catalog, prices, IDs and ownership writes preserved');
await assert.rejects(attachClans(patched),/already present/);checks++;
await assert.rejects(attachClans(original.replace(/(['"])social_feature_not_deployed\1/g,"'other_route'")),/unsupported-social/);checks++;
await assert.rejects(attachClans(original.replace(/await\s+saveProfile\(/,'await anotherSave(')),/profile-save/);checks++;
await assert.rejects(attachClans(original.replace(/new\s+Firestore(?:\(\s*\))?/,"new Firestore({projectId:'other'})")),/Firestore/);checks++;
await assert.rejects(attachClans(original.replace(/body\.profile\s*,\s*body\.expectedProfileRevision\s*\)/,'body.profile, body.otherRevision)')),/profile-save/);checks++;
const withHat=original.replace(/const\s+ITEMS\s*=\s*\{/,match=>match+"\n  another_future_hat: item(1, [w('DF_HeadwearOwned_FutureHat')]),");
check(catalog(await attachClans(withHat))===catalog(withHat),'fresh export keeps later hat/catalog additions exactly');

function runtime(source=patched){
  const data=new Map(),calls=[],warnings=[],googleCalls=[];
  let handler,fetchBehavior;
  const profile=[{key:'DF_PlayerName',kind:1,text:'SolarFox1234567'},
    {key:'DF_AccountXP',kind:1,text:'0'},{key:'DF_Shards',kind:0,number:200}];
  data.set('players/'+alice,{balance:900,revision:1,profileRevision:2,profile,owned:[],extraMetadata:'retain'});
  class Ref{
    constructor(path){this.path=path;this.id=path.split('/').at(-1);}
    collection(name){return new Collection(this.path+'/'+name);}
    async get(){return snapshot(this);}
  }
  function snapshot(ref){return {id:ref.id,exists:data.has(ref.path),data:()=>copy(data.get(ref.path)||{})};}
  class Collection{
    constructor(path){this.path=path;this.cap=Infinity;this.fields=null;}
    doc(id){return new Ref(this.path+'/'+id);}
    select(...fields){this.fields=fields;return this;}
    limit(cap){this.cap=cap;return this;}
    async *stream(){let n=0;for(const [path,value]of data){if(path.startsWith(this.path+'/')&&!path.slice(this.path.length+1).includes('/')&&n++<this.cap){
      const ref=new Ref(path);yield {id:ref.id,exists:true,data:()=>this.fields?Object.fromEntries(this.fields.map(k=>[k,copy(value[k])])):copy(value)};
    }}}
  }
  class Firestore{
    collection(path){return new Collection(path);}
    async runTransaction(action){
      const writes=[];
      const tx={get:async ref=>{assert.equal(writes.length,0,'transaction reads precede writes');return snapshot(ref);},
        set:(ref,value,options)=>writes.push(()=>data.set(ref.path,options?.merge?{...data.get(ref.path),...copy(value)}:copy(value))),
        create:(ref,value)=>writes.push(()=>{assert.equal(data.has(ref.path),false);data.set(ref.path,copy(value));}),
        delete:ref=>writes.push(()=>data.delete(ref.path))};
      const result=await action(tx);for(const write of writes)write();return result;
    }
  }
  const response=(status,payload)=>({status,text:async()=>typeof payload==='string'?payload:JSON.stringify(payload)});
  fetchBehavior=async(url,options)=>response(200,url.endsWith('/_access')||url.endsWith('/_sync')?{fulfilled:true}:{fulfilled:true,social:{me:{playerId:'c'.repeat(20)},hasClan:false,clanProtocolVersion:4}});
  const context={URL,Buffer,console:{error(){},warn(message){warnings.push(message);}},process:{env:{}},createHash,
    Firestore,FieldValue:{serverTimestamp:()=>({timestamp:'fixture'})},
    GoogleAuth:class{async getClient(){return {request:async options=>{googleCalls.push(options);return {data:{
      purchaseStateContext:{purchaseState:'PURCHASED'},obfuscatedExternalAccountId:alice,
      productLineItem:[{productId:'dimensions_shards_500',productOfferDetails:{quantity:1,refundableQuantity:1,consumptionState:'YET_TO_BE_CONSUMED'}}]
    }};}};}},
    createRemoteJWKSet:()=>({fixture:true}),jwtVerify:async token=>{
      if(token!=='alice-token')throw new Error('invalid fixture signature');
      return {payload:{sub:'alice',project_id:'57bb61ab-1371-4b7a-a996-5a932e351c03',aud:['upid:57bb61ab-1371-4b7a-a996-5a932e351c03','envName:production']}};
    },
    AbortSignal,fetch:async(url,options)=>{calls.push({url,options});return fetchBehavior(url,options);},
    http:(name,fn)=>{check(name==='helloHttp','entry point name preserved');handler=fn;}
  };
  // The SDK/authentication dependencies are the same mocks for both real export
  // formats. Node's own parser supplies function boundaries for preservation
  // checks, so minified functions are compared without a formatting heuristic.
  const executable=source.replace(/\bimport\s*(?:\{[^}]*\}|[A-Za-z_$][\w$]*)\s*from\s*(['"])[^'"]+\1\s*;?/g,'');
  vm.runInNewContext(executable,context,{timeout:2000});
  const functions=Object.fromEntries(accountFunctions.map(name=>[name,context[name]?.toString()]));
  async function request(path,body={},token='alice-token',method='POST'){
    const res={code:0,body:null,sends:0,headers:{},set(k,v){this.headers[k]=v;return this;},status(code){this.code=code;return this;},
      json(value){this.body=copy(value);this.sends++;return this;},send(value){this.body=value;this.sends++;return this;}};
    await handler({path,body,method,get:name=>name.toLowerCase()==='authorization'?(token?'Bearer '+token:''):''},res);
    check(res.sends===1,'HTTP response emitted exactly once');return res;
  }
  return {data,calls,warnings,googleCalls,request,response,functions,setFetch:value=>{fetchBehavior=value;}};
}

let r=runtime();
let res=await r.request('/',{},'','GET');
check(res.code===200&&res.body.revision==='2026-10-24.clans.1'&&r.calls.length===0,'public root keeps service identity without internal account lookup');
for(const token of ['', 'wrong']){
  res=await r.request('/social/clan/create',{},token);
  check(res.code===401&&r.calls.length===0,'Unity authentication precedes all clan forwarding');
}
res=await r.request('/social/state',{accountHash:'forged'});
check(res.code===200&&res.body.social.clanProtocolVersion===4,'clan response reaches the existing client');
check(r.calls.at(-1).url==='http://127.0.0.1:8081/social/state'&&r.calls.at(-1).options.headers.Authorization==='Bearer alice-token','forward only to fixed loopback service with original authentication');
res=await r.request('/social/clan/create',{},'alice-token','GET');
check(res.code===405,'HTTP method restrictions retained');
const count=r.calls.length;
res=await r.request('/social/../../wallet');check(res.code===400&&r.calls.length===count,'route cannot select an arbitrary destination');
res=await r.request('/social/state',{text:'x'.repeat(262145)});check(res.code===400,'oversized social requests rejected');
r.setFetch(async()=>r.response(502,'<html>private failure</html>'));
res=await r.request('/social/state');check(res.code===503&&!JSON.stringify(res.body).includes('private failure'),'malformed upstream failures are safe and retryable');

r=runtime();
r.setFetch(async()=>r.response(200,{fulfilled:true,social:{wallet:{accountHash:alice,balance:600,profileRevision:3,revision:2,profile:[],owned:[]}}}));
res=await r.request('/social/clan/create',{operationId:'1'.repeat(32)});
check(res.code===200&&res.body.social.wallet.billingReady===true&&res.body.social.wallet.subscriptionsReady===false,'clan debit uses original Node billing decoration');
check(res.headers['Cache-Control']==='no-store'&&res.body.social.wallet.balance===600,'wallet values and cache policy retained');
r.setFetch(async()=>r.response(200,{fulfilled:true,social:{wallet:{accountHash:'b'.repeat(64),balance:100000}}}));
res=await r.request('/social/clan/create');check(res.code===503,'wrong-account wallet rejected before client application');

r=runtime();
const before=JSON.stringify([...r.data]);
r.setFetch(async()=>r.response(403,{fulfilled:false}));
res=await r.request('/wallet/spend',{cosmeticId:'avatar_10',operationId:'3'.repeat(32),expectedProfileRevision:2});
check(res.code===403&&JSON.stringify([...r.data])===before,'suspended account cannot debit or mutate account data');
r.setFetch(async()=>{throw new Error('private upstream failure');});
res=await r.request('/wallet/spend',{cosmeticId:'avatar_10',operationId:'3'.repeat(32),expectedProfileRevision:2});
check(res.code===503&&JSON.stringify([...r.data])===before,'unavailable access check cannot execute a purchase');

r=runtime();
res=await r.request('/wallet/spend',{cosmeticId:'avatar_10',operationId:'4'.repeat(32),expectedProfileRevision:2});
check(res.code===200&&res.body.wallet.balance===650&&res.body.wallet.owned.includes('avatar_10'),'original shop debit still uses earned then paid shards');
res=await r.request('/wallet/spend',{cosmeticId:'avatar_10',operationId:'4'.repeat(32),expectedProfileRevision:2});
check(res.code===200&&res.body.wallet.balance===650,'original shop retry cannot charge twice');
check(r.calls.every(c=>c.url.endsWith('/social/_access')&&c.options.body==='{}'),'purchase data and receipts are not forwarded into clan service');
check(r.data.get('players/'+alice).extraMetadata==='retain','original account metadata survives shop writes');

r=runtime();
r.setFetch(async url=>url.endsWith('/_access')?r.response(200,{fulfilled:true}):r.response(503,{fulfilled:false}));
res=await r.request('/profile/save',{profile:[{key:'DF_AccountXP',kind:1,text:'102'},{key:'DF_Shards',kind:0,number:200}],expectedProfileRevision:2});
check(res.code===200&&res.body.wallet.profileRevision===3,'failed directory sync cannot turn a committed profile save into an error');
check(r.warnings.length===1&&r.calls.at(-1).url.endsWith('/social/_sync'),'directory sync follows the profile commit and reports deferred refresh');
check(r.calls.at(-1).options.body==='{}','only authenticated account identity reaches profile synchronization');

r=runtime();
res=await r.request('/google-play/fulfill',{productId:'dimensions_shards_500',purchaseToken:'fixture-google-token'});
check(res.code===200&&res.body.wallet.balance===1400,'original Google Play grant still runs through its own verified ledger');
const revision=res.body.wallet.revision;
res=await r.request('/google-play/fulfill',{productId:'dimensions_shards_500',purchaseToken:'fixture-google-token'});
check(res.code===200&&res.body.wallet.balance===1400&&res.body.wallet.revision===revision,'Google receipt replay does not credit twice');
check(r.googleCalls.length>0&&r.calls.every(c=>c.options.body==='{}'),'Google verification retained and receipts excluded from clan transport');

console.log(`Clan transport: ${checks} preservation, authorization, HTTP and real Node business-logic checks passed (engine dependencies mocked).`);

if(process.argv.includes('--http')){
  const python=process.env.DF_CLAN_PYTHON;
  assert.ok(python,'Set DF_CLAN_PYTHON to the Python interpreter with account requirements installed.');
  const root=fileURLToPath(new URL('../../',import.meta.url));
  const worker=spawn(python,[fileURLToPath(new URL('./http_fixture.py',import.meta.url))],{
    env:{...process.env,PYTHONPATH:root+'/Server/commerce'},stdio:['ignore','pipe','pipe']});
  let errors='';worker.stderr.on('data',chunk=>{errors+=chunk.toString();});
  const before=checks;
  try{
    const first=await Promise.race([
      (async()=>{let text='';for await(const chunk of worker.stdout){text+=chunk.toString();if(text.includes('\n'))return JSON.parse(text.split('\n')[0]);}throw new Error('Fixture exited: '+errors);})(),
      new Promise((_,reject)=>{const timer=setTimeout(()=>reject(new Error('HTTP fixture startup timed out: '+errors)),10000);timer.unref();})]);
    r=runtime();r.setFetch((url,options)=>fetch(url.replace('http://127.0.0.1:8081','http://127.0.0.1:'+first.port),options));
    res=await r.request('/social/state');check(res.code===200&&!res.body.social.hasClan&&res.body.social.clan===null,'real HTTP state reaches Node with explicit empty membership');
    res=await r.request('/social/clans');check(res.code===200&&res.body.social.clans.some(c=>c.clanId===first.oldClanId),'real HTTP directory retains the pre-existing clan');
    const body={operationId:'7'.repeat(32),expectedProfileRevision:7,descriptionId:0,open:true};
    res=await r.request('/social/clan/create',body);check(res.code===200&&res.body.social.wallet.balance===600&&res.body.social.wallet.billingReady,'real two-process creation returns exact debit and Node billing flags');
    const created=res.body.social.clan.clanId;
    res=await r.request('/social/clan/create',body);check(res.code===200&&res.body.social.clan.clanId===created&&res.body.social.wallet.balance===600,'real HTTP creation retry has no second debit');
    res=await r.request('/social/clan/leave');check(res.code===200&&!res.body.social.hasClan,'real HTTP leave updates membership');
    res=await r.request('/social/clan/join',{clanId:first.oldClanId});check(res.code===200&&res.body.social.clan.clanId===first.oldClanId&&res.body.social.role==='member','real HTTP joins original clan without replacing its ID');
    res=await r.request('/social/clan-levels');check(res.code===200&&res.body.social.selfRank>0&&res.body.social.clans.some(c=>c.clanId===first.oldClanId&&c.memberCount===2),'real HTTP summed-level ranking includes restored membership');
    console.log(`Clan two-process HTTP: ${checks-before} checks passed with the actual Python rules and Node handler (Firestore/authentication fixtures).`);
  }finally{
    if(worker.exitCode===null){worker.kill('SIGTERM');await once(worker,'exit');}
  }
}
