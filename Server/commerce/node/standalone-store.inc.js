// Existing Firestore collection layout; all durable writes remain transactional.
const DF_CLAN_NAMESPACE='dimensions-front';
const dfSocialCollection=table=>db.collection(DF_CLAN_NAMESPACE+'-'+table.replaceAll('_','-'));
const dfSocialKey=(table,row)=>{const p=DF_SOCIAL_KEYS[table].map(k=>row[k]);return p.length===1?p[0]:sha256(dfPyJSON(p,true));};
function dfExistingAccount(snapshot){
  if(!snapshot.exists)throw new Error('Account wallet must be loaded first');
  const value=snapshot.data();
  if(!value||!['balance','revision','profileRevision'].every(k=>Number.isSafeInteger(value[k])&&value[k]>=0)||!Array.isArray(value.profile)||!Array.isArray(value.owned)||!value.owned.every(x=>typeof x==='string')||!dfValidName(value.profile.find(e=>e.key==='DF_PlayerName')?.text))throw new Error('Account profile is not compatible');
  return value;
}
async function dfClanAccess(account){
  const snap=await dfSocialCollection('chat_user_ban').doc(account).get();
  if(snap.exists){const until=snap.data().until_at;if(until===-1||until>Date.now())throw new HttpError(403,'account_suspended');}
}
async function dfSyncDirectory(account){
  return db.runTransaction(async tx=>{
    const profile=await tx.get(playerRef(account));const current=dfExistingAccount(profile);
    const ref=dfSocialCollection('social_player').doc(account),old=await tx.get(ref);
    const value={account,player_id:old.exists&&old.data().player_id||dfHex(),...dfPublicValues(current.profile,account)};
    if(!old.exists||JSON.stringify(value)!==JSON.stringify(old.data()))tx.set(ref,value);
  });
}
async function dfClanProfileSaved(account,saved){
  const wallet=await saved;
  try{await dfSyncDirectory(account);}catch{console.warn('Clan directory synchronization pending; committed profile preserved.');}
  return wallet;
}
async function dfChatHeartbeat(account,data,config){
  return db.runTransaction(async tx=>{
    const read=async(table,id)=>{const s=await tx.get(dfSocialCollection(table).doc(id));return s.exists?s.data():null;};
    const m=await read('social_member',account),ban=await read('chat_user_ban',account),now=config.clock?config.clock():Date.now();
    if(!m||ban&&(ban.until_at===-1||ban.until_at>now))dfDeny('Clan access revoked');
    if(data.clanId!=null&&data.clanId!==''&&data.clanId!==m.clan)dfDeny('Private clan room');
    for(const kind of m.role==='leader'?['chat','leader']:['chat'])if(!await read('chat_consent',dfSocialKey('chat_consent',{account,kind,version:'1'})))dfDeny('Accept the current terms first');
    // Rejoining bans are authoritative even when a stale membership exists.
    const sanction=await read('chat_sanction',dfSocialKey('chat_sanction',{clan:m.clan,account}));
    if(sanction?.banned)dfDeny('You are banned from rejoining this clan.');
    const room=await read('chat_room',m.clan),revision=room?.revision??'initial';
    return revision===data.knownRevision?{unchanged:true,chatRevision:revision,clanId:m.clan,expiresBefore:now-config.messageDays*86400000}:null;
  });
}
let dfSqliteModule;
async function dfSocialDispatch(account,route,request,config=dfChatConfig()){
  if(route.startsWith('/social/clan/message')||route.startsWith('/social/message')||route.startsWith('/social/friend/')||['/social/players','/social/clan/identity'].includes(route)||!config.enabled&&['/social/block','/social/unblock'].includes(route))dfBad('Communication is disabled in this version.');
  const admin=route.startsWith('/social/chat/admin/');
  if(admin&&!config.admins.has(account))dfDeny('Administrator required');
  if(route==='/social/chat/admin/delivery'){await dfClanAccess(account);return {delivery:await dfPumpReports(config)};}
  if(route==='/social/chat/read'&&request.knownRevision){if(!config.enabled)dfBad('Communication is disabled in this version.');const result=await dfChatHeartbeat(account,request,config);if(result)return result;}
  // Use the existing Node wallet initializer when social state wins the startup
  // race. This is the same account store/name-claim code used by /wallet.
  await readWallet(account);
  // Loaded lazily: an unsupported SQLite runtime cannot prevent the existing
  // purchase/profile handler from starting or serving its original endpoints.
  dfSqliteModule??=import('node:sqlite');const {DatabaseSync}=await dfSqliteModule;
  return db.runTransaction(async tx=>{
    let data={...request};
    const rows=Object.fromEntries(Object.keys(DF_SOCIAL_TABLES).map(t=>[t,new Map()]));
    const rowFrom=(table,snap)=>{
      const raw=snap.data(),row={};
      for(const c of DF_SOCIAL_TABLES[table]){if(raw[c]===undefined){if(c==='avatar'&&table==='social_clan')row[c]=-1;else throw new Error('Clan document schema requires review');}else row[c]=raw[c];}
      rows[table].set(snap.id,row);return row;
    };
    const read=async(table,id)=>{if(rows[table].has(id))return rows[table].get(id);const snap=await tx.get(dfSocialCollection(table).doc(id));return snap.exists?rowFrom(table,snap):null;};
    const query=async(table,field,value)=>{let q=dfSocialCollection(table);if(field)q=q.where(field,'==',value);const snapshot=await tx.get(q);for(const doc of snapshot.docs)rowFrom(table,doc);};
    const profileSnap=await tx.get(playerRef(account)),caller=dfExistingAccount(profileSnap);
    await read('social_player',account);let target=null;
    if(typeof data.playerId==='string'&&data.playerId.length===20){await query('social_player','player_id',data.playerId);target=[...rows.social_player.values()].find(p=>p.player_id===data.playerId)?.account??null;}
    const actors=new Set([account]);if(target)actors.add(target);
    for(const who of actors){await read('social_member',who);for(const f of ['a','b'])await query('social_friend',f,who);for(const f of ['account','target'])await query('social_block',f,who);await query('social_application','account',who);}
    await read('social_rate',account);await query('social_creation','account',account);
    const clans=new Set([...rows.social_member.values()].map(r=>r.clan));if(typeof data.clanId==='string'&&data.clanId.length===20)clans.add(data.clanId);
    for(const clan of clans){await read('social_clan',clan);await query('social_member','clan',clan);await query('social_application','clan',clan);}
    const own=rows.social_member.get(account);
    if(route.startsWith('/social/chat/')&&!admin&&!['/social/chat/consent','/social/chat/leader-consent'].includes(route))if(!own||data.clanId!=null&&data.clanId!==''&&data.clanId!==own.clan)dfDeny('Private clan room');
    for(const who of actors){await query('chat_consent','account',who);await query('chat_rate','account',who);await read('chat_user_ban',who);}
    for(const clan of clans){await query('chat_sanction','clan',clan);await read('chat_room',clan);}
    if(['/social/chat/read','/social/chat/send','/social/chat/report','/social/chat/moderate'].includes(route)){
      const room=own.clan;
      const page=await tx.get(dfSocialCollection('social_message').where('clan','==',room).orderBy('created_at','desc').limit(100));for(const snap of page.docs)rowFrom('social_message',snap);
      if(typeof data.messageId==='string'&&data.messageId.length===20){const message=await read('social_message',data.messageId);if(message&&message.clan!==room)dfDeny('Private clan room');}
      if(route==='/social/chat/send'&&typeof data.operationId==='string'&&data.operationId.length===20)await read('social_message',sha256(account+':'+data.operationId).slice(0,20));
      if(route==='/social/chat/report'){const subject=data.messageId||target;if(subject){const id=sha256(dfPyJSON([account,room,subject])).slice(0,20);await read('chat_report',id);await read('chat_outbox',id);}}
    }
    if(admin){
      if(route==='/social/chat/admin/reports'){
        const offset=dfGet(data,'offset',0);if(!Number.isInteger(offset)||offset<0||offset>1000)dfBad('Invalid report page');
        const page=await tx.get(dfSocialCollection('chat_report').orderBy('created_at','desc').offset(offset).limit(8));for(const snap of page.docs)rowFrom('chat_report',snap);data={...data,offset:0};
      }else if(typeof data.reportId==='string'&&data.reportId.length===20){const report=await read('chat_report',data.reportId);if(report){target=report.target;actors.add(target);clans.add(report.clan);if(report.message_id)await read('social_message',report.message_id);}}
      for(const clan of clans){await read('social_clan',clan);await read('chat_room',clan);await query('social_member','clan',clan);await query('social_application','clan',clan);}
      for(const who of actors){await read('social_member',who);await read('chat_user_ban',who);await query('chat_consent','account',who);}
      if(data.action==='remove'&&typeof data.messageId==='string'&&data.messageId.length===20)await read('social_message',data.messageId);
    }
    const accounts=new Set(actors);
    for(const r of rows.social_friend.values()){accounts.add(r.a);accounts.add(r.b);}
    for(const r of rows.social_block.values()){accounts.add(r.account);accounts.add(r.target);}
    for(const table of ['social_member','social_application','social_message'])for(const r of rows[table].values())accounts.add(r.account);
    for(const r of rows.chat_report.values()){accounts.add(r.reporter);accounts.add(r.target);}
    for(const who of accounts){await read('social_player',who);await read('social_member',who);}
    if(['/social/levels','/social/players'].includes(route))await query('social_player');
    if(['/social/clans','/social/clan-levels'].includes(route)){await query('social_clan');await query('social_member');await query('social_player');}
    const unique=new Map(),uniqueCollection=db.collection(DF_CLAN_NAMESPACE+'-social-clan-unique');
    const lock=async(kind,value)=>{const id=kind+'-'+sha256(value);if(unique.has(id))return unique.get(id).value;const ref=uniqueCollection.doc(id),snap=await tx.get(ref),v=snap.exists?snap.data():null;unique.set(id,{ref,value:v});return v;};
    let generated=null;
    if(route==='/social/clan/create'){
      for(let i=0;i<64;i++){const name=dfClanName(),tag=dfClanTag(),n=await lock('name',dfFold(name)),t=await lock('tag',tag);if(!n&&!t){generated=[name,tag];break;}}
      if(!generated)dfBad('Clan identity allocation unavailable');
    }
    for(const c of rows.social_clan.values()){await lock('name',c.name_search);await lock('tag',c.tag);}
    const before=Object.fromEntries(Object.entries(rows).map(([t,r])=>[t,new Map([...r].map(([k,v])=>[k,dfCopy(v)]))]));
    const stagedNames=new Set([...rows.social_clan.values()].map(c=>c.name_search)),stagedTags=new Set([...rows.social_clan.values()].map(c=>c.tag));
    for(const [id,old] of rows.social_clan){
      const c={...old};
      if(!dfValidName(c.name,true)){let allocated=false;for(let i=0;i<64;i++){const name=dfClanName(),key=dfFold(name),claim=await lock('name',key);if((!claim||claim.clan===id)&&!stagedNames.has(key)){stagedNames.add(key);c.name=name;c.name_search=key;allocated=true;break;}}if(!allocated)dfBad('Clan name allocation unavailable');}
      if(!dfValidTag(c.tag)){let allocated=false;for(let i=0;i<64;i++){const tag=dfClanTag(),claim=await lock('tag',tag);if((!claim||claim.clan===id)&&!stagedTags.has(tag)){stagedTags.add(tag);c.tag=tag;allocated=true;break;}}if(!allocated)dfBad('Clan tag allocation unavailable');}
      if(!DF_IDENTITY.clanDescriptions.includes(c.description))c.description=DF_IDENTITY.clanDescriptions[0];rows.social_clan.set(id,c);
    }
    const sql=new DatabaseSync(':memory:');let result,after;
    try{
      sql.exec(DF_SOCIAL_SQL);const rules=new DfClanRules(sql,account,config,generated);
      for(const [table,columns] of Object.entries(DF_SOCIAL_TABLES))for(const row of rows[table].values())rules.run('INSERT INTO '+table+' ('+columns.join(',')+') VALUES ('+columns.map(()=>'?').join(',')+')',columns.map(c=>row[c]));
      rules.run('INSERT INTO profile(account,revision,data) VALUES(?,?,?)',[account,caller.profileRevision,JSON.stringify(caller.profile)]);
      rules.run('INSERT INTO wallet(account,balance,revision) VALUES(?,?,?)',[account,caller.balance,caller.revision]);
      for(const sku of caller.owned)rules.run('INSERT INTO cosmetic(account,sku) VALUES(?,?)',[account,sku]);
      result=rules.dispatch(route,data);
      if(route==='/social/chat/admin/reports')result.offset=request.offset??0;
      after=Object.fromEntries(Object.entries(DF_SOCIAL_TABLES).map(([table,columns])=>[table,new Map(rules.all('SELECT '+columns.join(',')+' FROM '+table).map(values=>{const row=Object.fromEntries(columns.map((c,i)=>[c,values[i]]));return [dfSocialKey(table,row),row];}))]));
    }finally{sql.close();}
    // No Firestore reads below this line. Clan creation, both parts of the debit
    // and the operation receipt commit atomically with membership.
    if(route==='/social/clan/create'&&result.wallet){
      const w=result.wallet,next={...caller,balance:w.balance,revision:w.revision,profileRevision:w.profileRevision,profile:w.profile};
      if(JSON.stringify(next)!==JSON.stringify(caller))tx.set(playerRef(account),next);
      result.wallet=walletSnapshot(account,next);
    }
    for(const table of Object.keys(DF_SOCIAL_TABLES)){
      for(const id of before[table].keys())if(!after[table].has(id))tx.delete(dfSocialCollection(table).doc(id));
      for(const [id,row] of after[table])if(JSON.stringify(before[table].get(id))!==JSON.stringify(row)){
        const persisted={...row};
        if(['social_message','chat_report','chat_audit'].includes(table))persisted.expiresAt=new Date(row.created_at+(table==='social_message'?config.messageDays:config.reportDays)*86400000);
        if(table==='chat_outbox'&&after.chat_report.has(id))persisted.expiresAt=new Date(after.chat_report.get(id).created_at+config.reportDays*86400000);
        tx.set(dfSocialCollection(table).doc(id),persisted);
      }
    }
    const readRoutes=['/social/state','/social/players','/social/levels','/social/clans','/social/clan-levels'];
    if(!readRoutes.includes(route)){
      for(const who of actors)if(after.social_player.has(who))tx.set(dfSocialCollection('social_player').doc(who),after.social_player.get(who));
      for(const clan of clans)if(after.social_clan.has(clan))tx.set(dfSocialCollection('social_clan').doc(clan),after.social_clan.get(clan));
    }
    for(const id of new Set([...before.social_clan.keys(),...after.social_clan.keys()]))for(const [kind,field] of [['name','name_search'],['tag','tag']]){
      const old=before.social_clan.get(id)?.[field],value=after.social_clan.get(id)?.[field];if(old===value)continue;
      if(old!==undefined){const claim=unique.get(kind+'-'+sha256(old));if(claim?.value?.clan===id)tx.delete(claim.ref);}
      if(value!==undefined){const claim=unique.get(kind+'-'+sha256(value));if(!claim)throw new Error('Clan uniqueness lock missing');tx.set(claim.ref,{clan:id});}
    }
    return result;
  });
}
async function dfClanReply(req,res,account){
  if(req.body!==undefined&&(req.body===null||typeof req.body!=='object'||Array.isArray(req.body)))throw new HttpError(400,'invalid_social_payload');
  const body=req.body&&typeof req.body==='object'&&!Array.isArray(req.body)?req.body:{};
  if(Buffer.byteLength(JSON.stringify(body),'utf8')>262144)throw new HttpError(400,'social_request_too_large');
  try{
    const social=await dfSocialDispatch(account,req.path,body);
    if(social.wallet){const sink={set(){},status(){return this;},json(){}};await success(sink,social.wallet);}
    res.set('Cache-Control','no-store');return res.status(200).json({fulfilled:true,social});
  }catch(error){
    res.set('Cache-Control','no-store');
    if(error instanceof DfSocialError)return res.status(error.status).json({fulfilled:false,error:error.status===403?'social_forbidden':error.status===409?'profile_conflict':'invalid_social_request',message:error.message});
    console.error('Clan account service unavailable.',{code:Number.isInteger(error?.code)?error.code:'unavailable'});
    return res.status(503).json({fulfilled:false,error:'clan_service_unavailable',message:'Clan service temporarily unavailable. Please retry.'});
  }
}
