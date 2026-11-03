// Runs behind the existing verified Unity-token boundary. Firestore is authoritative.
const DF_GIFT_PRICES = Object.freeze([50, 100, 250, 500, 1000, 2500]);
const dfGiftRoutes = new Set(['/social/friend/state', '/social/friend/request', '/social/friend/accept', '/social/friend/remove', '/social/sticker/send', '/social/sticker/claim']);
const dfGiftInbox = account => playerRef(account).collection('sticker-gifts');
const dfGiftOperations = account => playerRef(account).collection('sticker-operations');
const dfGiftPublic = value => ({playerId:value.player_id, name:value.name, level:value.level, xp:value.xp, avatar:value.avatar});
async function dfGiftLinks(account, reader = ref => ref.get()) {
  const entries = new Map();
  for (const field of ['a', 'b']) {
    const result = await reader(dfSocialCollection('social_friend').where(field, '==', account).limit(151));
    for (const doc of result.docs) entries.set(doc.id, {ref:doc.ref, ...doc.data()});
  }
  return [...entries.values()];
}
async function dfGiftSnapshot(account) {
  const self = await dfSocialCollection('social_player').doc(account).get();
  const links = await dfGiftLinks(account), friends = [], incoming = [], outgoing = [];
  for (const link of links) {
    const other = link.a === account ? link.b : link.a;
    const player = await dfSocialCollection('social_player').doc(other).get();
    if (!player.exists) continue;
    (link.accepted ? friends : link.sender === account ? outgoing : incoming).push(dfGiftPublic(player.data()));
  }
  const inbox = await dfGiftInbox(account).where('claimed', '==', false).limit(50).get();
  const receivedStickers = inbox.docs.map(doc => {
    const gift = doc.data();
    return {giftId:doc.id, senderName:gift.senderName, senderHero:gift.senderHero, stickerId:gift.stickerId, receivedAtUnix:Math.floor(gift.createdAt/1000)};
  }).sort((a,b) => b.receivedAtUnix-a.receivedAtUnix || a.giftId.localeCompare(b.giftId));
  return {stickerProtocolVersion:1, me:dfGiftPublic(self.data()), friends, incoming, outgoing, receivedStickers, partyInvites:await dfPartyInvitesSnapshot(account)};
}
function dfGiftProfile(value) {
  if (!value || !Number.isSafeInteger(value.balance) || value.balance < 0 || !Number.isSafeInteger(value.revision) || !Number.isSafeInteger(value.profileRevision) || !Array.isArray(value.profile)) throw new Error('Invalid account wallet');
  return {...value, profile:value.profile.map(entry => ({...entry}))};
}
function dfGiftEarned(value) {
  const entry = value.profile.find(entry => entry.key === 'DF_Shards');
  if (entry && (entry.kind !== 0 || !Number.isInteger(entry.number) || entry.number < 0 || entry.number > 2147483647)) throw new Error('Invalid earned balance');
  return entry?.number ?? 0;
}
function dfGiftSetEarned(value, amount) {
  if (!Number.isSafeInteger(amount) || amount < 0 || amount > 2147483647) dfBad('Shard balance limit reached.');
  const entry = {key:'DF_Shards', kind:0, number:amount};
  value.profile = value.profile.filter(item => item.key !== entry.key).concat([entry]).sort((a,b) => a.key.localeCompare(b.key));
  value.profileRevision++;
}
async function dfFriendGiftDispatch(account, route, data) {
  if (!data || typeof data !== 'object' || Array.isArray(data)) dfBad('Invalid friend request.');
  await readWallet(account);
  await dfClanAccess(account);
  await dfSyncDirectory(account);
  if (route === '/social/friend/state') return await dfGiftSnapshot(account);
  if (!dfGiftRoutes.has(route)) dfBad('Unknown friend or sticker action.');
  const isSend = route === '/social/sticker/send', isClaim = route === '/social/sticker/claim';
  if (isSend && (typeof data.operationId !== 'string' || !/^[a-f0-9]{32}$/.test(data.operationId) || !Number.isInteger(data.stickerId) || data.stickerId < 0 || data.stickerId >= DF_GIFT_PRICES.length)) dfBad('Invalid sticker or operation ID.');
  if (isClaim && (typeof data.giftId !== 'string' || !/^[a-f0-9]{64}$/.test(data.giftId))) dfBad('Invalid gift ID.');
  let target = null;
  if (!isClaim) {
    const id = typeof data.playerId === 'string' ? data.playerId.trim() : '';
    const query = typeof data.query === 'string' ? data.query.trim() : '';
    if (id && !/^[a-f0-9]{20}$/.test(id) || !id && (route !== '/social/friend/request' || !query || query.length > 40)) dfBad('Choose a real player name or ID.');
    const result = await dfSocialCollection('social_player').where(id ? 'player_id' : 'name_search', '==', id || dfFold(query)).limit(2).get();
    if (result.docs.length !== 1) dfBad('Player not found or name is ambiguous. Use the player ID.');
    target = result.docs[0].id;
    if (target === account) dfBad('Choose another player.');
  }
  const committed = await db.runTransaction(async tx => {
    const read = async ref => {const snap = await tx.get(ref); return snap.exists ? snap.data() : null;};
    const caller = dfGiftProfile(await read(playerRef(account)));
    const ownLock = dfSocialCollection('friend_state').doc(account), ownState = await read(ownLock);
    const ownBan = await read(dfSocialCollection('chat_user_ban').doc(account));
    if (ownBan && (ownBan.until_at === -1 || ownBan.until_at > Date.now())) dfDeny('Account suspended.');
    if (isClaim) {
      const ref = dfGiftInbox(account).doc(data.giftId), gift = await read(ref);
      if (!gift || gift.recipient !== account) dfBad('Gift not found.');
      if (gift.claimed) return caller; // Durable receipt survives retries and restarts.
      if (caller.profileRevision !== data.expectedProfileRevision) throw new DfSocialError(409, 'Cloud profile changed; refresh and retry.');
      if (!Number.isInteger(gift.stickerId) || gift.stickerId < 0 || gift.stickerId >= DF_GIFT_PRICES.length) throw new Error('Invalid stored gift');
      dfGiftSetEarned(caller, dfGiftEarned(caller) + DF_GIFT_PRICES[gift.stickerId]/5);
      tx.set(playerRef(account), caller);
      tx.set(ref, {...gift, claimed:true, claimedAt:Date.now()});
      tx.set(ownLock, {revision:(ownState?.revision ?? 0)+1});
      return caller;
    }
    const [a,b] = [account,target].sort();
    const linkRef = dfSocialCollection('social_friend').doc(dfSocialKey('social_friend', {a,b}));
    const link = await read(linkRef);
    const targetLock = dfSocialCollection('friend_state').doc(target), targetState = await read(targetLock);
    const receiver = await read(playerRef(target));
    const senderPublic = await read(dfSocialCollection('social_player').doc(account));
    const receiverPublic = await read(dfSocialCollection('social_player').doc(target));
    const ban = await read(dfSocialCollection('chat_user_ban').doc(target));
    const blocked = await read(dfSocialCollection('social_block').doc(dfSocialKey('social_block', {account,target})));
    const blockedBack = await read(dfSocialCollection('social_block').doc(dfSocialKey('social_block', {account:target,target:account})));
    const rateRef = dfSocialCollection('social_rate').doc(account), rate = await read(rateRef), window = Math.floor(Date.now()/60000);
    const count = rate?.window === window ? rate.count : 0;
    const operationRef = isSend ? dfGiftOperations(account).doc(data.operationId) : null;
    const previous = operationRef ? await read(operationRef) : null;
    if (previous) {
      if (previous.recipient !== target || previous.stickerId !== data.stickerId) dfBad('Gift operation already bound.');
      return caller;
    }
    if (route !== '/social/friend/remove' && (!receiver || !senderPublic || !receiverPublic || blocked || blockedBack || ban && (ban.until_at === -1 || ban.until_at > Date.now()))) dfDeny('This player is unavailable.');
    if (count >= 30) dfBad('Too many actions; try again in a minute.');
    if (isSend) {
      if (!link?.accepted) dfDeny('Only accepted friends can receive gifts.');
      if (caller.profileRevision !== data.expectedProfileRevision) throw new DfSocialError(409, 'Cloud profile changed; refresh and retry.');
      const inbox = await tx.get(dfGiftInbox(target).where('claimed', '==', false).limit(50));
      if (inbox.docs.length >= 50) dfBad('Your friend must collect gifts before receiving more.');
      const cost = DF_GIFT_PRICES[data.stickerId], earned = dfGiftEarned(caller), contribution = Math.min(earned,cost), paid = cost-contribution;
      if (caller.balance < paid) dfBad('Not enough Shards.');
      caller.balance -= paid; caller.revision++;
      dfGiftSetEarned(caller, earned-contribution);
      const giftId = sha256('friend-sticker/v1/'+account+'/'+data.operationId);
      tx.set(dfGiftInbox(target).doc(giftId), {recipient:target, senderName:senderPublic.name, senderHero:0, senderPlayerId:senderPublic.player_id, stickerId:data.stickerId, createdAt:Date.now(), claimed:false});
      tx.set(operationRef, {recipient:target, stickerId:data.stickerId, giftId});
      tx.set(playerRef(account), caller);
    } else if (route === '/social/friend/request') {
      if (link) return null;
      for (const who of [account,target]) {
        const links = await dfGiftLinks(who, ref => tx.get(ref));
        if (links.filter(item => item.accepted).length >= 100 || links.filter(item => !item.accepted).length >= 50) dfBad('Friend or request list is full.');
      }
      tx.set(linkRef, {a,b,sender:account,accepted:0});
    } else if (route === '/social/friend/accept') {
      if (!link || link.sender === account) dfBad('Only the recipient can accept a friend request.');
      if (link.accepted) return null;
      for (const who of [account,target]) if ((await dfGiftLinks(who, ref => tx.get(ref))).filter(item => item.accepted).length >= 100) dfBad('Friend list is full.');
      tx.set(linkRef, {...link, accepted:1});
    } else if (route === '/social/friend/remove') {
      if (link) tx.delete(linkRef);
    }
    tx.set(rateRef, {account, window, count:count+1});
    tx.set(ownLock, {revision:(ownState?.revision ?? 0)+1});
    tx.set(targetLock, {revision:(targetState?.revision ?? 0)+1});
    return isSend ? caller : null;
  });
  const result = await dfGiftSnapshot(account);
  if (committed) result.wallet = walletSnapshot(account, committed);
  return result;
}
