// Test-only RPC worker. Every Firestore operation uses an in-memory strict fake.
import {readFile} from 'node:fs/promises';
import {createHash,randomBytes,randomInt} from 'node:crypto';
import {createInterface} from 'node:readline';
import {attachStandaloneClans} from '../../Server/commerce/node/attach-standalone-clans.mjs';
import {enableFriendStickerGifts} from '../../Server/commerce/node/enable-friend-sticker-gifts.mjs';
const original=await readFile(new URL('./fixtures/node-2026-10-22.2-cloud-export.js',import.meta.url),'utf8');
const base=await attachStandaloneClans(original);
const patched=process.env.DF_TEST_FRIEND_GIFTS==='1'?enableFriendStickerGifts(base):base;
let data=new Map(),reads=[],handler,failRead,epoch=0;
const copy=value=>structuredClone(value);
class Ref{
  constructor(path){this.path=path;this.id=path.split('/').at(-1);}
  collection(name){return new Query(this.path+'/'+name);}
  async get(){reads.push(this.path);return snapshot(this,data);}
}
function snapshot(ref,source){return {id:ref.id,ref,exists:source.has(ref.path),data:()=>copy(source.get(ref.path))};}
class Query{
  constructor(path,filters=[],orders=[],cap=Infinity,skip=0,fields=null){Object.assign(this,{path,filters,orders,cap,skip,fields});this.id=path.split('/').at(-1);}
  doc(id){return new Ref(this.path+'/'+id);}
  where(field,op,value){return new Query(this.path,[...this.filters,[field,op,value]],this.orders,this.cap,this.skip,this.fields);}
  orderBy(field,direction='asc'){return new Query(this.path,this.filters,[...this.orders,[field,direction]],this.cap,this.skip,this.fields);}
  limit(cap){return new Query(this.path,this.filters,this.orders,cap,this.skip,this.fields);}
  offset(skip){return new Query(this.path,this.filters,this.orders,this.cap,skip,this.fields);}
  select(...fields){return new Query(this.path,this.filters,this.orders,this.cap,this.skip,fields);}
  list(source){
    let entries=[...source].filter(([path,v])=>path.startsWith(this.path+'/')&&!path.slice(this.path.length+1).includes('/')&&this.filters.every(([k,op,x])=>op==='=='?v[k]===x:op==='<'?v[k]<x:false)).sort(([a],[b])=>a<b?-1:a>b?1:0);
    for(const [field,dir]of [...this.orders].reverse())entries.sort((a,b)=>(a[1][field]<b[1][field]?-1:a[1][field]>b[1][field]?1:0)*(dir==='desc'?-1:1));
    return entries.slice(this.skip,this.cap===Infinity?undefined:this.skip+this.cap).map(([path,v])=>{const s=snapshot(new Ref(path),source);if(this.fields)s.data=()=>Object.fromEntries(this.fields.map(k=>[k,copy(v[k])]));return s;});
  }
  async get(){reads.push(this.path+'?'+JSON.stringify(this.filters));return {docs:this.list(data)};}
  async *stream(){for(const row of this.list(data))yield row;}
}
class Firestore{
  collection(path){return new Query(path);}
  async runTransaction(action){
    for(let attempt=0;attempt<32;attempt++){
    const started=epoch;
    const source=copy(data),writes=[];
    const tx={get:async ref=>{if(writes.length)throw new Error('Firestore reads must precede writes');if(failRead&&ref.path===failRead)throw new Error('Fixture read unavailable');reads.push(ref instanceof Query?ref.path+'?'+JSON.stringify(ref.filters):ref.path);return ref instanceof Query?{docs:ref.list(source)}:snapshot(ref,source);},
      set:(ref,value,options)=>writes.push(()=>data.set(ref.path,options?.merge?{...data.get(ref.path),...copy(value)}:copy(value))),
      delete:ref=>writes.push(()=>data.delete(ref.path)),create:(ref,value)=>writes.push(()=>{if(data.has(ref.path))throw new Error('Already exists');data.set(ref.path,copy(value));})};
    const value=await action(tx);if(started!==epoch)continue;for(const write of writes)write();if(writes.length)epoch++;return value;
    }
    throw new Error('Fixture transaction retry limit');
  }
}
const unimport=source=>source.replace(/\bimport\s*(?:\{[^}]*\}|[A-Za-z_$][\w$]*)\s*from\s*(['"])[^'"]+\1\s*;?/g,'');
const factory=new Function('http','Firestore','FieldValue','GoogleAuth','createHash','createRemoteJWKSet','jwtVerify','dfRandomBytes','dfRandomInt','process','console',unimport(patched)+`\nreturn {dispatch:dfSocialDispatch,access:dfClanAccess,sync:dfSyncDirectory,reserve:dfReserveReport,finish:dfFinishReport,pump:dfPumpReports,config:dfChatConfig,functions:Object.fromEntries(['authenticateUnity','readWallet','saveProfile','spendWallet','fulfillGooglePurchase','syncSubscription','handleSubscriptionPush','walletSnapshot','success'].map(name=>[name,eval(name).toString()]))};`);
const api=factory((name,fn)=>{handler=fn;},Firestore,{serverTimestamp:()=>({fixtureTimestamp:true})},class{async getClient(){return {request:async()=>({data:{purchaseStateContext:{purchaseState:'PURCHASED'},obfuscatedExternalAccountId:createHash('sha256').update('alice').digest('hex'),productLineItem:[{productId:'dimensions_shards_500',productOfferDetails:{quantity:1,refundableQuantity:1,consumptionState:'YET_TO_BE_CONSUMED'}}]}})};}},createHash,()=>({}),async token=>{if(!token||token==='wrong')throw new Error('Invalid fixture signature');return {payload:{sub:token,project_id:'57bb61ab-1371-4b7a-a996-5a932e351c03',aud:['upid:57bb61ab-1371-4b7a-a996-5a932e351c03','envName:production']}};},randomBytes,randomInt,{env:{}},{warn(){},error(){}});
for await(const line of createInterface({input:process.stdin,crlfDelay:Infinity})){
  if(!line)continue;let request;
  try{
    request=JSON.parse(line);data=new Map(Object.entries(request.data??{}));reads=[];failRead=request.failRead;
    const config={...api.config(),...request.config,admins:new Set(request.config?.admins??[]),clock:()=>request.now??Date.now()};
    let result;
    if(request.mode==='parallel')result=await Promise.all(request.requests.map(async item=>{try{return {ok:true,result:await api.dispatch(item.account,item.route,item.body??{},config)};}catch(error){return {ok:false,status:error.status??503,message:error.message};}}));
    else if(request.mode==='access')result=await api.access(request.account);
    else if(request.mode==='sync')result=await api.sync(request.account);
    else if(request.mode==='functions')result=api.functions;
    else if(request.mode==='reserve')result=await api.reserve(config);
    else if(request.mode==='finish')result=await api.finish(request.id,request.lease,request.delivered);
    else if(request.mode==='pump')result=await api.pump(config,async()=>{if(!request.fakeSendOk)throw new Error('Fixture SMTP offline');});
    else if(request.mode==='http'){
      const res={statusCode:0,headers:{},sends:0,set(k,v){this.headers[k]=v;return this;},status(v){this.statusCode=v;return this;},json(v){this.body=copy(v);this.sends++;return this;},send(v){this.body=v;this.sends++;return this;}};
      await handler({path:request.route,body:request.body??{},method:request.method??'POST',get:name=>name.toLowerCase()==='authorization'?(request.token===null?'':'Bearer '+(request.token??'alice')):''},res);result=res;
    }else result=await api.dispatch(request.account,request.route,request.body??{},config);
    process.stdout.write(JSON.stringify({ok:true,result:result??null,data:Object.fromEntries(data),reads},function(key,value){return this[key] instanceof Date?{$df_time:this[key].toISOString()}:value;})+'\n');
  }catch(error){process.stdout.write(JSON.stringify({ok:false,status:error.status??503,message:error.message,stack:error.stack,data:Object.fromEntries(data),reads})+'\n');}
}
