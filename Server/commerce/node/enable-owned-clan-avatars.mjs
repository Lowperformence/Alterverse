// Patch the existing standalone Node account index without changing its catalog,
// wallet/authentication functions, dependencies or Firestore namespace.
import {readFileSync,writeFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {fileURLToPath} from 'node:url';

export const previousOwnership=`      if(!Number.isInteger(avatar)||!Object.hasOwn(ITEMS,sku))dfBad('Choose an owned shop icon');
      const entries=new Map(JSON.parse(this.one('SELECT data FROM profile WHERE account=?',[account])[0]).map(e=>[e.key,e]));
      if(entries.get('DF_AvatarOwned_'+avatar)?.number!==1&&!this.one('SELECT 1 FROM cosmetic WHERE account=? AND sku=?',[account,sku]))dfBad('Purchase this icon before choosing it for your clan');`;
export const ownedAvatars=`      if(!Number.isInteger(avatar)||avatar<0||avatar>1000)dfBad('Choose an owned avatar');
      const entries=new Map(JSON.parse(this.one('SELECT data FROM profile WHERE account=?',[account])[0]).map(e=>[e.key,e]));
      const flag=key=>entries.get(key)?.kind===0&&entries.get(key)?.number===1;
      const owned=avatar<6||(avatar===6&&flag('DF_FoundersPack_Owned'))||(avatar>6&&(flag('DF_AvatarOwned_'+avatar)||this.one('SELECT 1 FROM cosmetic WHERE account=? AND sku=?',[account,sku])));
      if(!owned)dfBad('Unlock this avatar before choosing it for your clan');`;

export function patchOwnedClanAvatars(source){
  if(!source.includes('// DF_STANDALONE_CLANS_BEGIN v1')||source.split(previousOwnership).length!==2)
    throw new Error('Expected the original standalone clan ownership gate exactly once; no output written');
  const revision=/const SERVER_REVISION\s*=\s*(['"])2026-10-24\.clans\.node\.1\1/;
  if(!revision.test(source))throw new Error('Review the current live revision before applying this patch');
  return source.replace(previousOwnership,ownedAvatars).replace(revision,"const SERVER_REVISION = '2026-10-25.clans.node.2'");
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  const [input,output]=process.argv.slice(2);
  if(!input||!output||resolve(input)===resolve(output))throw new Error('Usage: node enable-owned-clan-avatars.mjs current-index.js corrected-index.js');
  const result=patchOwnedClanAvatars(readFileSync(input,'utf8'));
  writeFileSync(output,result,{encoding:'utf8',flag:'wx'});
  process.stdout.write('Complete standalone index written; original source preserved.\n');
}
