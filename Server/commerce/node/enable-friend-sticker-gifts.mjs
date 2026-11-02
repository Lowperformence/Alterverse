// Preserve the current live index, including later catalog/account fixes.
import {readFileSync, writeFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {fileURLToPath} from 'node:url';

export function enableFriendStickerGifts(source) {
  if (source.includes('DF_FRIEND_STICKER_GIFTS_BEGIN')) throw new Error('Friend gifts are already installed.');
  const dispatch = /async function dfSocialDispatch\(account,route,request,config=dfChatConfig\(\)\)\{/g;
  if ([...source.matchAll(dispatch)].length !== 1 || !source.includes('// DF_STANDALONE_CLANS_BEGIN v1')) throw new Error('Expected the current standalone Node account backend; source needs review.');
  for (const name of ['dfSyncDirectory','dfSocialKey','walletSnapshot','playerRef','readWallet']) if (!source.includes(name)) throw new Error('Missing account dependency: '+name);
  const revision = /(\bconst\s+SERVER_REVISION\s*=\s*)(['"])([^'"\r\n]+)\2/g;
  if ([...source.matchAll(revision)].length !== 1) throw new Error('Expected one account server revision.');
  const module = readFileSync(new URL('./friend-sticker-gifts.inc.js', import.meta.url), 'utf8');
  return source.replace(revision, (_,prefix,quote,value) => prefix+quote+value+'.friend-gifts.1'+quote).replace(dispatch, match => match+"\n  if (dfGiftRoutes.has(route)) return await dfFriendGiftDispatch(account, route, request);")+
    '\n// DF_FRIEND_STICKER_GIFTS_BEGIN v1\n'+module+'\n// DF_FRIEND_STICKER_GIFTS_END v1\n';
}
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [input,output] = process.argv.slice(2);
  if (!input || !output || resolve(input) === resolve(output)) throw new Error('Usage: node enable-friend-sticker-gifts.mjs current-index.js updated-index.js');
  writeFileSync(output, enableFriendStickerGifts(readFileSync(input,'utf8')), {flag:'wx'});
  console.log('Updated index prepared. Original source, authentication and catalog preserved.');
}
