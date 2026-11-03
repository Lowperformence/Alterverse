// Private, short-lived invitations between accepted friends. No currency mutations.
const dfPartyInbox=account=>playerRef(account).collection('party-invites');
async function dfPartyInvitesSnapshot(account){
  const rows=await dfPartyInbox(account).limit(100).get(),now=Date.now();
  return rows.docs.map(doc=>({inviteId:doc.id,...doc.data()}))
    .filter(value=>value.recipient===account&&value.expiresAt>now)
    .map(value=>({inviteId:value.inviteId,senderName:value.senderName,partyCode:value.partyCode,expiresAtUnix:Math.floor(value.expiresAt/1000)}))
    .sort((a,b)=>b.expiresAtUnix-a.expiresAtUnix);
}
async function dfPartyInviteDispatch(account,route,data){
  await readWallet(account);await dfClanAccess(account);await dfSyncDirectory(account);
  if(route==='/social/party/decline'){
    if(typeof data.inviteId!=='string'||!/^[a-f0-9]{64}$/.test(data.inviteId))dfBad('Invalid invitation.');
    await db.runTransaction(async tx=>{
      const ref=dfPartyInbox(account).doc(data.inviteId),snap=await tx.get(ref);
      if(snap.exists&&snap.data().recipient!==account)dfDeny('Private invitation.');
      if(snap.exists)tx.delete(ref);
    });
    return await dfGiftSnapshot(account);
  }
  if(route!=='/social/party/invite')dfBad('Unknown invitation action.');
  if(typeof data.playerId!=='string'||!/^[a-f0-9]{20}$/.test(data.playerId)||
     typeof data.partyCode!=='string'||!/^[A-Z0-9_-]{4,16}$/.test(data.partyCode)||
     typeof data.partySessionId!=='string'||!/^[A-Za-z0-9_-]{8,128}$/.test(data.partySessionId))dfBad('Invalid party invitation.');
  const players=await dfSocialCollection('social_player').where('player_id','==',data.playerId).limit(2).get();
  if(players.docs.length!==1||players.docs[0].id===account)dfBad('Choose another player.');
  const target=players.docs[0].id;
  await db.runTransaction(async tx=>{
    const read=async ref=>{const snap=await tx.get(ref);return snap.exists?snap.data():null;};
    const [a,b]=[account,target].sort();
    const link=await read(dfSocialCollection('social_friend').doc(dfSocialKey('social_friend',{a,b})));
    const sender=await read(dfSocialCollection('social_player').doc(account));
    const recipient=await read(playerRef(target));
    const blocked=await read(dfSocialCollection('social_block').doc(dfSocialKey('social_block',{account,target})));
    const blockedBack=await read(dfSocialCollection('social_block').doc(dfSocialKey('social_block',{account:target,target:account})));
    const ban=await read(dfSocialCollection('chat_user_ban').doc(target));
    const ownBan=await read(dfSocialCollection('chat_user_ban').doc(account));
    const lockRef=dfSocialCollection('friend_state').doc(target),lock=await read(lockRef);
    const rateRef=dfSocialCollection('party_rate').doc(account),rate=await read(rateRef);
    const inbox=await tx.get(dfPartyInbox(target).limit(100));
    const now=Date.now(),window=Math.floor(now/60000),count=rate?.window===window?rate.count:0;
    if(!link?.accepted||!sender||!recipient||blocked||blockedBack||
       [ban,ownBan].some(value=>value&&(value.until_at===-1||value.until_at>now)))dfDeny('This friend is unavailable.');
    if(count>=10)dfBad('Too many invitations. Try again in a minute.');
    const id=sha256('party-invite/v1/'+account+'/'+target+'/'+data.partySessionId);
    if(inbox.docs.filter(doc=>doc.data().expiresAt>now&&doc.id!==id).length>=10)dfBad('Your friend has too many pending invitations.');
    for(const doc of inbox.docs)if(doc.data().expiresAt<=now)tx.delete(doc.ref);
    tx.set(dfPartyInbox(target).doc(id),{recipient:target,senderName:sender.name,partyCode:data.partyCode,expiresAt:now+300000});
    tx.set(lockRef,{revision:(lock?.revision??0)+1});tx.set(rateRef,{window,count:count+1});
  });
  return await dfGiftSnapshot(account);
}
