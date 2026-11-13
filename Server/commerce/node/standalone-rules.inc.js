// The existing clan/chat protocol, implemented inside the Node account process.
// SQLite is an in-memory transaction workspace; Firestore remains the durable store.
class DfSocialError extends Error {
  constructor(status,message){super(message);this.status=status;}
}
const dfBad=message=>{throw new DfSocialError(400,message);};
const dfDeny=message=>{throw new DfSocialError(403,message);};
const dfHex=()=>dfRandomBytes(10).toString('hex');
const dfCopy=value=>JSON.parse(JSON.stringify(value));
const dfGet=(value,key,fallback)=>Object.hasOwn(value,key)?value[key]:fallback;
const dfFold=value=>Array.from(value,c=>DF_CASE_FOLD[c]??c.toLowerCase()).join('');
function dfPyJSON(value,compact=false,ascii=true){
  if(value===undefined)value=null;
  if(Array.isArray(value))return '['+value.map(v=>dfPyJSON(v,compact,ascii)).join(compact?',':', ')+']';
  if(value&&typeof value==='object')return '{'+Object.entries(value).map(([k,v])=>dfPyJSON(k,compact,ascii)+(compact?':':': ')+dfPyJSON(v,compact,ascii)).join(compact?',':', ')+'}';
  const text=JSON.stringify(value);
  return ascii?text.replace(/[\u007f-\uffff]/g,c=>'\\u'+c.charCodeAt(0).toString(16).padStart(4,'0')):text;
}
function dfChatConfig(){
  const days=(key,fallback)=>{
    const raw=process.env[key]??String(fallback);
    if(!/^\d+$/.test(raw)||!Number.isInteger(Number(raw))||Number(raw)<1||Number(raw)>365)throw new Error('Invalid chat retention configuration');
    return Number(raw);
  };
  return {enabled:(process.env.DF_ENABLE_CLAN_CHAT??'1')==='1',
    admins:new Set((process.env.DF_CHAT_ADMIN_ACCOUNTS??'').split(',').map(x=>x.trim()).filter(Boolean)),
    messageDays:days('DF_CHAT_MESSAGE_DAYS',30),reportDays:days('DF_CHAT_REPORT_DAYS',180),
    words:(process.env.DF_CHAT_FILTER_WORDS??'fuck,bitch,cunt,arschloch,hurensohn,heil hitler,kill yourself').split(',').map(x=>dfFold(x.trim())).filter(Boolean),
    adminEmail:(process.env.DF_CHAT_ADMIN_EMAIL??'').trim()};
}
function dfValidName(value,clan=false){
  if(typeof value!=='string'||!/[0-9]{7}$/.test(value))return false;
  return (clan?DF_IDENTITY.clanNames:DF_IDENTITY.playerNames).includes(value.slice(0,clan?-8:-7))&&(!clan||value.at(-8)===' ');
}
const dfClanName=()=>DF_IDENTITY.clanNames[dfRandomInt(DF_IDENTITY.clanNames.length)]+' '+String(dfRandomInt(10000000)).padStart(7,'0');
const dfClanTag=()=>'C'+String(dfRandomInt(10000000)).padStart(7,'0');
const dfValidTag=value=>typeof value==='string'&&/^C[0-9]{7}$/.test(value);
function dfDisplayName(value){return typeof value==='string'&&value.length>0&&value.length<=32&&Buffer.byteLength(value,'utf8')<=60&&value===value.trim()&&value.normalize('NFC')===value&&!/[<>\p{Cc}\p{Cf}\p{Cs}\p{Co}\p{Cn}\p{Zl}\p{Zp}]/u.test(value);}
function dfVisibleName(value,identity){
  if(dfDisplayName(value))return value;
  const digest=BigInt('0x'+sha256(String(identity))),n=BigInt(DF_IDENTITY.playerNames.length);
  return DF_IDENTITY.playerNames[Number(digest%n)]+String(digest/n%10000000n).padStart(7,'0');
}
function dfPublicValues(entries,account){
  const values=new Map(entries.map(e=>[e.key,e]));
  const alias=values.get('DF_PlayGamesDisplayName');
  const canonical=values.get('DF_PlayerName')?.text;
  const name=dfVisibleName(alias?.kind===1&&dfDisplayName(alias.text)?alias.text:canonical,account);
  const raw=values.get('DF_AccountXP')?.text??'0';let xp=0;
  const max=100*999+2*999*999+Math.floor(999**3/250);
  if(typeof raw==='string'&&raw.length<=20&&/^[+-]?\d+$/.test(raw.trim())){
    const n=BigInt(raw.trim());xp=n<0n?0:n>BigInt(max)?max:Number(n);
  }
  let lo=1,hi=1000;
  while(lo<hi){const m=Math.floor((lo+hi+1)/2),n=m-1;if(100*n+2*n*n+Math.floor(n**3/250)<=xp)lo=m;else hi=m-1;}
  const a=values.get('DF_Avatar')?.number;
  return {name,name_search:dfFold(name),xp,level:lo,avatar:Number.isInteger(a)&&a>=0&&a<=1000?a:0};
}
function dfDescription(data){
  const index=dfGet(data,'descriptionId',0);
  if(!Number.isInteger(index)||index<0||index>=DF_IDENTITY.clanDescriptions.length)dfBad('Choose a preset clan description');
  if(data.description!=null&&data.description!==''&&!DF_IDENTITY.clanDescriptions.includes(data.description))dfBad('Custom clan descriptions are no longer available');
  return DF_IDENTITY.clanDescriptions[index];
}
class DfClanRules {
  constructor(sql,account,config,generated=null){this.sql=sql;this.account=account;this.config=config;this.generated=generated;}
  now(){return this.config.clock?this.config.clock():Date.now();}
  statement(query,parameters=[]){return {statement:this.sql.prepare(query),parameters:parameters.map(v=>typeof v==='boolean'?Number(v):v)};}
  one(query,parameters=[]){const {statement,parameters:p}=this.statement(query,parameters);const row=statement.get(...p);return row?Object.values(row):null;}
  all(query,parameters=[]){const {statement,parameters:p}=this.statement(query,parameters);return statement.all(...p).map(Object.values);}
  run(query,parameters=[]){const {statement,parameters:p}=this.statement(query,parameters);return statement.run(...p);}
  count(query,parameters=[]){return this.one(query,parameters)?.[0]??0;}
  member(account=this.account){return this.one('SELECT clan,role FROM social_member WHERE account=?',[account]);}
  player(account){
    const row=this.one('SELECT player_id,name,level,xp,avatar FROM social_player WHERE account=?',[account]);
    if(!row)dfBad('Player not found');
    return {playerId:row[0],name:dfVisibleName(row[1],row[0]),level:row[2],xp:row[3],avatar:row[4]};
  }
  target(data){
    if(typeof data.playerId!=='string'||!/^[a-f0-9]{20}$/.test(data.playerId))dfBad('Invalid player ID');
    const row=this.one('SELECT account FROM social_player WHERE player_id=?',[data.playerId]);
    if(!row)dfBad('Player not found');return row[0];
  }
  identifier(value){if(typeof value!=='string'||!/^[a-f0-9]{20}$/.test(value))dfBad('Invalid message or report ID');return value;}
  clanId(data){if(typeof data.clanId!=='string'||!/^[a-f0-9]{20}$/.test(data.clanId))dfBad('Invalid clan ID');return data.clanId;}
  page(data){const n=dfGet(data,'offset',0);if(!Number.isInteger(n)||n<0||n>1000000)dfBad('Invalid page');return n;}
  query(data){const q=dfGet(data,'query','');if(typeof q!=='string'||Array.from(q).length>40)dfBad('Invalid search');return dfFold(q.trim());}
  accountAllowed(account=this.account){const row=this.one('SELECT until_at FROM chat_user_ban WHERE account=?',[account]);if(row&&(row[0]===-1||row[0]>this.now()))dfDeny('Account suspended');}
  consent(account,kind){return this.one('SELECT accepted_at,age_band FROM chat_consent WHERE account=? AND kind=? AND version=?',[account,kind,'1']);}
  accept(data,kind,account=this.account){
    if(data.accepted!==true||data.consentVersion!=='1')dfBad('Please actively accept the current terms.');
    const age=kind==='chat'?(data.ageBand??''):'';
    if(kind==='chat'&&!['13-17','18+'].includes(age))dfBad('Chat requires an age group of 13 or older.');
    const old=this.consent(account,kind);if(old&&old[1]!==age)dfBad('Your saved age group cannot be changed here.');
    this.run('INSERT OR IGNORE INTO chat_consent VALUES(?,?,?,?,?)',[account,kind,'1',this.now(),age]);
  }
  eligible(account,clan){const row=this.one('SELECT banned FROM chat_sanction WHERE clan=? AND account=?',[clan,account]);if(row?.[0])dfDeny('You are banned from rejoining this clan.');this.accountAllowed(account);}
  leader(){const m=this.member();if(!m||m[1]!=='leader')dfDeny('Clan leader required');if(!this.consent(this.account,'leader'))dfDeny('Accept the moderation terms first.');return m[0];}
  scope(data){
    const m=this.member();if(!m)dfDeny('Clan membership required');
    if(data.clanId!=null&&data.clanId!==''&&data.clanId!==m[0])dfDeny('Private clan room');
    if(!this.consent(this.account,'chat'))dfDeny('Accept the chat rules first.');
    if(m[1]==='leader'&&!this.consent(this.account,'leader'))dfDeny('Accept the moderation terms first.');
    this.eligible(this.account,m[0]);return m[0];
  }
  syncPlayer(profile){
    const v=dfPublicValues(profile,this.account);
    this.run('INSERT INTO social_player(account,player_id,name,name_search,xp,level,avatar) VALUES(?,?,?,?,?,?,?) ON CONFLICT(account) DO UPDATE SET name=excluded.name,name_search=excluded.name_search,xp=excluded.xp,level=excluded.level,avatar=excluded.avatar',[this.account,dfHex(),v.name,v.name_search,v.xp,v.level,v.avatar]);
  }
  clan(id){
    const row=this.one('SELECT id,name,tag,description,open,avatar FROM social_clan WHERE id=?',[id]);if(!row)dfBad('Clan not found');
    const members=this.all("SELECT account,role FROM social_member WHERE clan=? ORDER BY CASE role WHEN 'leader' THEN 0 WHEN 'officer' THEN 1 ELSE 2 END,account",[id]).map(([account,role])=>({...this.player(account),role}));
    return {clanId:row[0],name:row[1],tag:row[2],description:row[3],open:Boolean(row[4]),avatar:row[5],descriptionId:DF_IDENTITY.clanDescriptions.indexOf(row[3]),members,memberCount:members.length,totalLevel:members.reduce((n,p)=>n+p.level,0)};
  }
  snapshot(){
    const account=this.account,m=this.member(),chat=this.consent(account,'chat'),leader=this.consent(account,'leader');
    const sanction=m?this.one('SELECT muted_until,warnings FROM chat_sanction WHERE clan=? AND account=?',[m[0],account]):null;
    return {chatEnabled:this.config.enabled,chatProtocolVersion:1,chatRulesVersion:'1',leaderRulesVersion:'1',chatRules:DF_CHAT_RULES,leaderRules:DF_LEADER_RULES,reportReasons:[...DF_REPORT_REASONS],
      chatAccepted:Boolean(chat),leaderAccepted:Boolean(leader),chatAcceptedAt:chat?.[0]??0,leaderAcceptedAt:leader?.[0]??0,mutedUntil:sanction?.[0]??0,warnings:sanction?.[1]??0,isChatAdmin:this.config.admins.has(account),maxMessageLength:500,
      hasClan:Boolean(m),clanCreationCost:500,clanProtocolVersion:this.config.enabled?4:3,identityProtocolVersion:1,clanDescriptions:[...DF_IDENTITY.clanDescriptions],me:this.player(account),friends:[],incoming:[],outgoing:[],
      blocked:this.config.enabled?this.all('SELECT target FROM social_block WHERE account=? ORDER BY target',[account]).map(r=>this.player(r[0])):[],clan:m?this.clan(m[0]):null,role:m?.[1]??'',
      applications:m&&['leader','officer'].includes(m[1])?this.all('SELECT account FROM social_application WHERE clan=? ORDER BY account',[m[0]]).map(r=>this.player(r[0])):[],
      pendingClans:this.all('SELECT clan FROM social_application WHERE account=? ORDER BY clan',[account]).map(r=>r[0])};
  }
  rate(kind,maximum,spacing=0,fingerprint=''){
    const now=this.now(),window=Math.floor(now/60000),row=this.one('SELECT window,count,last_at,fingerprint FROM chat_rate WHERE account=? AND kind=?',[this.account,kind]);
    const count=row&&row[0]===window?row[1]:0;
    if(count>=maximum||(row&&now-row[2]<spacing))dfBad('Too many actions. Please wait a moment.');
    if(fingerprint&&row&&fingerprint===row[3]&&now-row[2]<30000)dfBad('Repeated message. Please wait a moment.');
    this.run('INSERT INTO chat_rate VALUES(?,?,?,?,?,?) ON CONFLICT(account,kind) DO UPDATE SET window=excluded.window,count=excluded.count,last_at=excluded.last_at,fingerprint=excluded.fingerprint',[this.account,kind,window,count+1,now,fingerprint]);
  }
  text(value){
    if(typeof value!=='string')dfBad('Message required.');value=value.normalize('NFKC').trim();
    if(Array.from(value).length<1||Array.from(value).length>500)dfBad('Messages must contain 1–500 characters.');
    if(/[\p{C}]/u.test(value))dfBad('Control characters are not allowed.');
    if(/(?:https?\s*:|www\s*\.|[a-z0-9-]+\s*[.]\s*(?:com|de|net|org|gg|io|ru|xyz)\b|\b[a-z0-9._%+-]+@[a-z0-9.-]+|(?:\+?\d[\d ()/-]*){7})/i.test(value))dfBad('Links and contact details are not allowed in clan chat.');
    const folded=dfFold(value),compact=folded.replace(/[^a-zäöüß]/g,'');
    for(const word of this.config.words){
      const escaped=word.replace(/[.*+?^${}()|[\]\\]/g,'\\$&');
      if(new RegExp('(?<![\\p{L}\\p{N}_])'+escaped+'(?![\\p{L}\\p{N}_])','u').test(folded)||compact.includes(word.replace(/[^a-zäöüß]/g,'')))dfBad('This message contains a blocked expression.');
    }
    return value;
  }
  revision(clan){return this.one('SELECT revision FROM chat_room WHERE clan=?',[clan])?.[0]??'initial';}
  touch(clan){if(clan)this.run('INSERT OR REPLACE INTO chat_room VALUES(?,?)',[clan,dfHex()]);}
  audit(clan,target,action){this.run('INSERT INTO chat_audit VALUES(?,?,?,?,?,?)',[dfHex(),clan,this.account,target,action,this.now()]);}
  messages(clan){
    return this.all('SELECT m.id,m.name,m.text,m.created_at,p.player_id FROM social_message m JOIN social_player p ON p.account=m.account WHERE m.clan=? AND m.created_at>=? AND NOT EXISTS(SELECT 1 FROM social_block b WHERE (b.account=? AND b.target=m.account) OR (b.account=m.account AND b.target=?)) ORDER BY m.created_at DESC,m.id DESC LIMIT 50',[clan,this.now()-this.config.messageDays*86400000,this.account,this.account]).reverse().map(r=>({messageId:r[0],name:r[1],text:r[2],createdAt:r[3],playerId:r[4]}));
  }
  addMember(account,clan,role='member'){
    this.eligible(account,clan);if(this.member(account))dfBad('Player already belongs to a clan');
    if(this.count('SELECT COUNT(*) FROM social_member WHERE clan=?',[clan])>=(this.config.maxMembers??50))dfBad('Clan full (50 members)');
    this.run('INSERT INTO social_member VALUES(?,?,?)',[account,clan,role]);this.run('DELETE FROM social_application WHERE account=?',[account]);
  }
  wallet(){
    const [balance,revision]=this.one('SELECT balance,revision FROM wallet WHERE account=?',[this.account]);
    const [profileRevision,profile]=this.one('SELECT revision,data FROM profile WHERE account=?',[this.account]);
    return {balance,revision,profileRevision,profile:JSON.parse(profile),owned:this.all('SELECT sku FROM cosmetic WHERE account=?',[this.account]).map(r=>r[0])};
  }
  clanAction(route,data){
    const account=this.account,m=this.member();
    if(route==='/social/clan/create'){
      if(this.config.enabled)this.accept(data,'leader');
      const opened=dfGet(data,'open',true),operation=data.operationId;
      if(typeof operation!=='string'||!/^[a-f0-9]{32}$/.test(operation))dfBad('Invalid operation ID');
      if(typeof opened!=='boolean')dfBad('Invalid join policy');
      const previous=this.one('SELECT fingerprint FROM social_creation WHERE account=? AND operation_id=?',[account,operation]);
      if(previous){
        const hashes=[sha256(dfPyJSON([data.name??null,typeof data.tag==='string'?data.tag.toUpperCase():(data.tag??null),Object.hasOwn(data,'description')?data.description:'',opened],false,false))];
        try{const description=dfDescription(data);hashes.push(sha256(dfPyJSON([description,opened],false,false)));if(dfValidName(data.name,true)&&dfValidTag(data.tag))hashes.push(sha256(dfPyJSON([data.name,data.tag,description,opened],false,false)));}catch(error){if(!(error instanceof DfSocialError))throw error;}
        if(!hashes.includes(previous[0]))dfBad('Clan operation already bound');return;
      }
      const description=dfDescription(data);if(m)dfBad('Leave your current clan first');
      const revision=data.expectedProfileRevision;if(!Number.isInteger(revision)||revision<0)dfBad('Invalid profile revision');
      const current=this.one('SELECT revision,data FROM profile WHERE account=?',[account]);
      if(current[0]!==revision)throw new DfSocialError(409,'Cloud profile changed; refresh and retry.');
      let [name,tag]=this.generated??[null,null];
      if(!name){for(let i=0;i<64;i++){name=dfClanName();tag=dfClanTag();if(!this.one('SELECT 1 FROM social_clan WHERE name_search=? OR tag=?',[dfFold(name),tag]))break;name=null;}if(!name)dfBad('Clan identity allocation unavailable');}
      if(this.one('SELECT 1 FROM social_clan WHERE name_search=? OR tag=?',[dfFold(name),tag]))dfBad('Generated clan identity is unavailable; retry');
      const id=dfHex(),entries=new Map(JSON.parse(current[1]).map(e=>[e.key,e]));
      const earned=entries.get('DF_Shards')?.number??0,contribution=Math.min(earned,500),paid=500-contribution;
      if(this.run('UPDATE wallet SET balance=balance-?,revision=revision+1 WHERE account=? AND balance>=?',[paid,account,paid]).changes!==1)dfBad('Clan creation costs 500 Shards; insufficient balance');
      entries.set('DF_Shards',{key:'DF_Shards',kind:0,number:earned-contribution});
      const profile=[...entries.values()].sort((a,b)=>a.key<b.key?-1:a.key>b.key?1:0);
      this.run('UPDATE profile SET data=?,revision=revision+1 WHERE account=?',[JSON.stringify(profile),account]);
      this.run('INSERT INTO social_creation VALUES(?,?,?,?,?)',[account,operation,id,sha256(dfPyJSON([description,opened],false,false)),500]);
      this.run('INSERT INTO social_clan(id,name,name_search,tag,description,open) VALUES(?,?,?,?,?,?)',[id,name,dfFold(name),tag,description,Number(opened)]);
      this.addMember(account,id,'leader');return;
    }
    if(['/social/clan/join','/social/clan/cancel'].includes(route)){
      const id=this.clanId(data),clan=this.one('SELECT open FROM social_clan WHERE id=?',[id]);if(!clan)dfBad('Clan not found');
      if(route.endsWith('/cancel')){this.run('DELETE FROM social_application WHERE account=? AND clan=?',[account,id]);return;}
      this.eligible(account,id);if(m){if(m[0]===id)return;dfBad('Leave your current clan first');}
      if(clan[0])this.addMember(account,id);else{
        if(this.count('SELECT COUNT(*) FROM social_application WHERE account=?',[account])>=5)dfBad('At most five pending clan applications');
        if(this.count('SELECT COUNT(*) FROM social_application WHERE clan=?',[id])>=100)dfBad('Clan application list full');
        this.run('INSERT OR IGNORE INTO social_application VALUES(?,?)',[account,id]);
      }return;
    }
    if(!m)dfBad('Clan membership required');const [id,role]=m;
    if(route==='/social/clan/icon'){
      if(role!=='leader')dfDeny('Leader required');const avatar=data.avatar,sku='avatar_'+avatar;
      if(!Number.isInteger(avatar)||!Object.hasOwn(ITEMS,sku))dfBad('Choose an owned shop icon');
      const entries=new Map(JSON.parse(this.one('SELECT data FROM profile WHERE account=?',[account])[0]).map(e=>[e.key,e]));
      if(entries.get('DF_AvatarOwned_'+avatar)?.number!==1&&!this.one('SELECT 1 FROM cosmetic WHERE account=? AND sku=?',[account,sku]))dfBad('Purchase this icon before choosing it for your clan');
      this.run('UPDATE social_clan SET avatar=? WHERE id=?',[avatar,id]);return;
    }
    if(route==='/social/clan/leave'){
      const count=this.count('SELECT COUNT(*) FROM social_member WHERE clan=?',[id]);if(role==='leader'&&count>1)dfBad('Transfer leadership before leaving');
      this.run('DELETE FROM social_member WHERE account=?',[account]);
      if(count===1){this.run('DELETE FROM social_application WHERE clan=?',[id]);this.run('DELETE FROM social_message WHERE clan=?',[id]);this.run('DELETE FROM social_clan WHERE id=?',[id]);}return;
    }
    const other=this.target(data);if(other===account)dfBad('Select another member');const target=this.member(other);
    if(['/social/clan/accept','/social/clan/reject'].includes(route)){
      if(!['leader','officer'].includes(role))dfDeny('Clan management required');
      if(!this.one('SELECT 1 FROM social_application WHERE account=? AND clan=?',[other,id])){if(route.endsWith('/accept')&&target?.[0]===id)return;dfBad('Application not found');}
      if(route.endsWith('/accept'))this.addMember(other,id);else this.run('DELETE FROM social_application WHERE account=? AND clan=?',[other,id]);return;
    }
    if(!target||target[0]!==id)dfBad('Member not found in your clan');
    if(route.endsWith('/kick')){if(role==='member'||target[1]==='leader'||role==='officer'&&target[1]!=='member')dfDeny('Cannot remove this member');this.run('DELETE FROM social_member WHERE account=?',[other]);}
    else if(route.endsWith('/role')){if(role!=='leader')dfDeny('Leader required');if(!['member','officer'].includes(data.role)||target[1]==='leader')dfBad('Invalid role');this.run('UPDATE social_member SET role=? WHERE account=?',[data.role,other]);}
    else if(route.endsWith('/transfer')){if(role!=='leader')dfDeny('Leader required');if(this.config.enabled&&!this.consent(other,'leader'))dfBad('New leader must accept moderation terms first');this.run("UPDATE social_member SET role='member' WHERE account=?",[account]);this.run("UPDATE social_member SET role='leader' WHERE account=?",[other]);}
  }
  block(route,data){
    const other=this.target(data);if(other===this.account)dfBad('Cannot select yourself');
    if(route==='/social/block'){
      if(!this.one('SELECT 1 FROM social_block WHERE account=? AND target=?',[this.account,other])&&this.count('SELECT COUNT(*) FROM social_block WHERE account=?',[this.account])>=200)dfBad('Block list full');
      this.run('INSERT OR IGNORE INTO social_block VALUES(?,?)',[this.account,other]);const [a,b]=[this.account,other].sort();this.run('DELETE FROM social_friend WHERE a=? AND b=?',[a,b]);
    }else this.run('DELETE FROM social_block WHERE account=? AND target=?',[this.account,other]);
    for(const who of [this.account,other]){const m=this.member(who);if(m)this.touch(m[0]);}
  }
  report(data,clan){
    if(!DF_REPORT_REASONS.includes(data.reason))dfBad('Choose a report reason.');
    const message=data.messageId||'';let target,evidence;
    if(message){this.identifier(message);const r=this.one('SELECT account,name,text,created_at FROM social_message WHERE id=? AND clan=?',[message,clan]);if(!r||r[3]<this.now()-this.config.messageDays*86400000)dfDeny('Message unavailable');target=r[0];evidence={name:r[1],text:r[2],createdAt:r[3],playerId:this.player(target).playerId};}
    else{target=this.target(data);if(this.member(target)?.[0]!==clan)dfDeny('Player unavailable');evidence=this.player(target);}
    if(target===this.account)dfBad('You cannot report yourself.');
    const id=sha256(dfPyJSON([this.account,clan,message||target])).slice(0,20);
    if(this.one('SELECT 1 FROM chat_report WHERE id=?',[id]))return id;
    this.rate('report',5);this.run('INSERT INTO chat_report VALUES(?,?,?,?,?,?,?,?,?,?)',[id,clan,message,this.account,target,data.reason,this.now(),dfPyJSON(evidence,false,false),'open',0]);
    if(this.config.adminEmail)this.run('INSERT INTO chat_outbox VALUES(?,?,?,?,?)',[id,0,this.now(),0,0]);return id;
  }
  moderate(data){
    const clan=this.leader();if(data.clanId!=null&&data.clanId!==''&&data.clanId!==clan)dfDeny('Private clan room');
    const other=this.target(data),m=this.member(other);if(other===this.account||m?.[0]!==clan)dfDeny('Clan member required');
    const action=data.action;if(!['warn','mute','unmute','ban'].includes(action))dfBad('Unknown moderation action');
    let [until,banned,warnings]=this.one('SELECT muted_until,banned,warnings FROM chat_sanction WHERE clan=? AND account=?',[clan,other])??[0,0,0];
    if(action==='warn')warnings++;else if(action==='mute'){const minutes=dfGet(data,'minutes',60);if(!Number.isInteger(minutes)||minutes<1||minutes>10080)dfBad('Invalid mute duration');until=this.now()+minutes*60000;}
    else if(action==='unmute')until=0;else{banned=1;this.run('DELETE FROM social_member WHERE account=?',[other]);this.run('DELETE FROM social_application WHERE account=? AND clan=?',[other,clan]);}
    this.run('INSERT OR REPLACE INTO chat_sanction VALUES(?,?,?,?,?)',[clan,other,until,banned,warnings]);this.audit(clan,other,action);this.touch(clan);
  }
  admin(route,data){
    if(!this.config.admins.has(this.account))dfDeny('Administrator required');
    if(route==='/social/chat/admin/reports'){
      const offset=this.page(data);const rows=this.all('SELECT id,clan,message_id,reporter,target,reason,created_at,evidence,status,reviewed_at FROM chat_report WHERE created_at>=? ORDER BY created_at DESC,id DESC LIMIT 8 OFFSET ?',[this.now()-this.config.reportDays*86400000,offset]);
      return {reports:rows.map(r=>({reportId:r[0],clanId:r[1],messageId:r[2],reason:r[5],createdAt:r[6],evidence:JSON.parse(r[7]),status:r[8],reviewedAt:r[9],reporterId:this.player(r[3]).playerId,playerId:this.player(r[4]).playerId})),offset};
    }
    if(route!=='/social/chat/admin/action')dfBad('Unknown administration route');this.rate('admin',30);
    const action=data.action;if(!['remove','suspend','unsuspend','close','transfer','resolve'].includes(action))dfBad('Unknown administration action');
    let report=null;if(data.reportId){report=this.one('SELECT clan,message_id,target FROM chat_report WHERE id=?',[this.identifier(data.reportId)]);if(!report)dfBad('Report unavailable');}
    const clan=report?report[0]:this.clanId(data);let target=report?report[2]:(data.playerId?this.target(data):'');
    if(action==='remove'){const message=report?report[1]:this.identifier(data.messageId);this.run('DELETE FROM social_message WHERE id=? AND clan=?',[message,clan]);}
    else if(['suspend','unsuspend'].includes(action)){if(!target||this.config.admins.has(target))dfDeny('Cannot suspend an administrator');this.run('INSERT OR REPLACE INTO chat_user_ban VALUES(?,?)',[target,action==='suspend'?-1:0]);}
    else if(action==='close'){this.run('DELETE FROM social_member WHERE clan=?',[clan]);this.run('DELETE FROM social_application WHERE clan=?',[clan]);this.run('DELETE FROM social_message WHERE clan=?',[clan]);this.run('DELETE FROM social_clan WHERE id=?',[clan]);}
    else if(action==='transfer'){target=this.target(data);if(this.member(target)?.[0]!==clan||!this.consent(target,'leader'))dfBad('New leader must be a member and accept moderation terms first');this.run("UPDATE social_member SET role='member' WHERE clan=? AND role='leader'",[clan]);this.run("UPDATE social_member SET role='leader' WHERE account=?",[target]);}
    else if(!report)dfBad('Report required');
    if(report)this.run("UPDATE chat_report SET status='resolved',reviewed_at=? WHERE id=?",[this.now(),data.reportId]);this.audit(clan,target,'admin:'+action);this.touch(clan);return {adminAction:action};
  }
  chat(route,data){
    if(!this.config.enabled)dfBad('Communication is disabled in this version.');
    if(route.startsWith('/social/chat/admin/'))return this.admin(route,data);
    if(['/social/chat/consent','/social/chat/leader-consent'].includes(route)){this.rate('consent',20);this.accept(data,route.endsWith('/consent')?'chat':'leader');return this.snapshot();}
    const clan=this.scope(data),revision=this.revision(clan),expiresBefore=this.now()-this.config.messageDays*86400000;
    if(route==='/social/chat/read'&&data.knownRevision===revision)return {unchanged:true,chatRevision:revision,clanId:clan,expiresBefore};
    const result={};
    if(route==='/social/chat/send'){
      const text=this.text(data.text);if((this.one('SELECT muted_until FROM chat_sanction WHERE clan=? AND account=?',[clan,this.account])?.[0]??0)>this.now())dfDeny('You are muted in clan chat.');
      const operation=this.identifier(data.operationId),id=sha256(this.account+':'+operation).slice(0,20),old=this.one('SELECT clan,account,text FROM social_message WHERE id=?',[id]);
      if(old){if(old[0]!==clan||old[1]!==this.account||old[2]!==text)dfBad('Message operation already bound');}
      else{this.rate('send',12,1500,sha256(dfFold(text)));this.run('INSERT INTO social_message VALUES(?,?,?,?,?,?)',[id,clan,this.account,this.player(this.account).name,text,this.now()]);this.touch(clan);}result.sentMessageId=id;
    }else if(route==='/social/chat/report')result.reportId=this.report(data,clan);
    else if(route==='/social/chat/moderate'){this.rate('moderate',30);this.moderate(data);}
    else if(route!=='/social/chat/read')dfBad('Unknown chat route');
    return {...result,...this.snapshot(),messages:this.messages(clan),chatRevision:this.revision(clan),clanId:clan,expiresBefore};
  }
  dispatch(route,data){
    if(route.startsWith('/social/clan/message')||route.startsWith('/social/message')||route.startsWith('/social/friend/')||['/social/players','/social/clan/identity'].includes(route)||!this.config.enabled&&['/social/block','/social/unblock'].includes(route))dfBad('Communication is disabled in this version.');
    this.accountAllowed();const profile=JSON.parse(this.one('SELECT data FROM profile WHERE account=?',[this.account])[0]);this.syncPlayer(profile);
    if(route.startsWith('/social/chat/'))return this.chat(route,data);
    if(route==='/social/state')return this.snapshot();
    if(route==='/social/levels'){
      const offset=this.page(data),rank='SELECT account,ROW_NUMBER() OVER(ORDER BY level DESC,xp DESC,player_id) AS rank FROM social_player';
      return {players:this.all(rank+' ORDER BY rank LIMIT 8 OFFSET ?',[offset]).map(r=>({...this.player(r[0]),rank:r[1]})),selfRank:this.one('SELECT rank FROM ('+rank+') WHERE account=?',[this.account])[0],total:this.count('SELECT COUNT(*) FROM social_player'),me:this.player(this.account),offset};
    }
    if(['/social/clans','/social/clan-levels'].includes(route)){
      const offset=this.page(data),query=this.query(data),base='SELECT c.id,c.name,c.tag,c.description,c.open,c.avatar,COUNT(m.account),COALESCE(SUM(p.level),0) AS score FROM social_clan c LEFT JOIN social_member m ON m.clan=c.id LEFT JOIN social_player p ON p.account=m.account',rank='SELECT *,ROW_NUMBER() OVER(ORDER BY score DESC,id) AS rank FROM ('+base+' GROUP BY c.id)';
      const rows=route==='/social/clans'?this.all(base+' WHERE instr(c.name_search,?)>0 OR instr(lower(c.tag),?)>0 GROUP BY c.id ORDER BY score DESC,c.id LIMIT 8 OFFSET ?',[query,query,offset]):this.all(rank+' ORDER BY rank LIMIT 8 OFFSET ?',[offset]);
      const m=this.member();return {clans:rows.map(r=>{const c={clanId:r[0],name:r[1],tag:r[2],description:r[3],open:Boolean(r[4]),avatar:r[5],memberCount:r[6],totalLevel:r[7]};if(r.length>8)c.rank=r[8];return c;}),selfRank:m?this.one('SELECT rank FROM ('+rank+') WHERE id=?',[m[0]])[0]:0,offset,total:this.count('SELECT COUNT(*) FROM social_clan')};
    }
    const allowed=['/social/block','/social/unblock','/social/clan/create','/social/clan/join','/social/clan/cancel','/social/clan/accept','/social/clan/reject','/social/clan/leave','/social/clan/kick','/social/clan/role','/social/clan/transfer','/social/clan/icon'];
    if(!allowed.includes(route))dfBad('Unknown social route');
    const window=Math.floor(this.now()/60000),old=this.one('SELECT window,count FROM social_rate WHERE account=?',[this.account]),count=old?.[0]===window?old[1]:0;
    if(count>=30)dfBad('Too many actions; try again in a minute');this.run('INSERT INTO social_rate VALUES(?,?,?) ON CONFLICT(account) DO UPDATE SET window=excluded.window,count=excluded.count',[this.account,window,count+1]);
    if(['/social/block','/social/unblock'].includes(route))this.block(route,data);else this.clanAction(route,data);
    const result=this.snapshot();if(route==='/social/clan/create'){this.syncPlayer(JSON.parse(this.one('SELECT data FROM profile WHERE account=?',[this.account])[0]));result.wallet=this.wallet();}return result;
  }
}
