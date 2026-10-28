// Patch only the deployed standalone chat error response. Keep its existing
// authentication, profiles, wallet, purchases, runtime and package.json intact.
import {readFileSync,writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';

export function repairChatIndexError(source){
  if(!source.includes('// DF_STANDALONE_CLANS_BEGIN v1'))throw new Error('Expected the standalone clan backend.');
  if(source.includes("code:'CHAT_INDEX_REQUIRED'"))return source;
  const anchor="    console.error('Clan account service unavailable.',{code:Number.isInteger(error?.code)?error.code:'unavailable'});";
  if(source.split(anchor).length!==2)throw new Error('Chat handler changed; inspect the current source before patching.');
  return source.replace(anchor,`    if(req.path.startsWith('/social/chat/')&&error?.code===9&&/index/i.test(String(error?.message??''))){
      console.error('Clan chat requires Firestore index setup.');
      return res.status(503).json({fulfilled:false,code:'CHAT_INDEX_REQUIRED',message:'Clan chat needs a server database index. The game administrator must finish chat setup.'});
    }
${anchor}`);
}

if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  if(process.argv.length!==4)throw new Error('Usage: node repair-chat-index-error.mjs current-index.js corrected-index.js');
  if(resolve(process.argv[2])===resolve(process.argv[3]))throw new Error('Use a separate output file so the original source is preserved.');
  writeFileSync(process.argv[3],repairChatIndexError(readFileSync(process.argv[2],'utf8')));
}
