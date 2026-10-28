// Add the two shared BOLT-7 weapon products to the current Node source; never deploys.
import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const catalog=JSON.parse(await readFile(new URL('../../../Assets/Alterverse/Resources/Commerce/ShardCatalog.json',import.meta.url)));
export const items=catalog.cosmetics.filter(x=>/^weapon_10_[01]$/.test(x.id));
if(items.length!==2||items.some(x=>x.price!==500||x.writes.some(w=>w.kind!==0||!w.key.includes('_10'))))throw new Error('Invalid BOLT-7 weapon catalog');
const line=x=>'  '+x.id+': item('+x.price+', '+JSON.stringify(x.writes)+'),\n';
export function enableBolt7Weapons(source){
 const anchor=/const ITEMS = \{\r?\n/g;
 if([...source.matchAll(anchor)].length!==1||!source.includes('const item ='))throw new Error('Unknown Node catalog layout; no source changed');
 const missing=[];
 for(const item of items){
  const mentions=new RegExp('(?:\\b'+item.id+'|[\"\u0027]'+item.id+'[\"\u0027])\\s*:','g');
  const found=[...source.matchAll(mentions)];
  if(!found.length)missing.push(line(item));
  else if(found.length!==1||!source.includes(line(item)))throw new Error('Existing '+item.id+' differs; inspect before changing source');
 }
 return missing.length?source.replace(anchor,match=>match+missing.join('')):source;
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 if(!process.argv[2])throw new Error('Pass current index.js path; --write applies with backup');
 const path=resolve(process.argv[2]),before=await readFile(path,'utf8'),after=enableBolt7Weapons(before);
 if(after===before)console.log('BOLT-7 weapons already configured');
 else if(process.argv.includes('--write')){await writeFile(path+'.before-bolt7-weapons',before,{flag:'wx'});await writeFile(path,after);console.log('Added BOLT-7 weapon products only. Deployment still required.');}
 else console.log('Ready to add two BOLT-7 weapon products. No source written.');
}

