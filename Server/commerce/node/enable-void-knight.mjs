// Targeted launch patch for the CURRENT deployed Node source; never deploys itself.
import {readFileSync,writeFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
export function enableVoidKnight(source){
 const sku='hero_void_knight';
 if(new RegExp('(?:\\b'+sku+'|["\u0027]'+sku+'["\u0027])\\s*:').test(source)){
  if(!/hero_void_knight\s*:\s*item\(1600,\s*\[w\(["']DF_HeroOwned_VoidKnight["']\)\]\)/.test(source))throw Error('Existing Void Knight product differs; no change made.');
  return source;
 }
 if(!source.includes('const item=')&&!source.includes('const item ='))throw Error('Unknown item factory; no change made.');
 const anchor=/const ITEMS\s*=\s*\{/g;
 if([...source.matchAll(anchor)].length!==1)throw Error('Unknown catalog layout; no change made.');
 return source.replace(anchor,m=>m+'\nhero_void_knight:item(1600,[w("DF_HeroOwned_VoidKnight")]),\n');
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 if(process.argv.length!==4||resolve(process.argv[2])===resolve(process.argv[3]))throw Error('Usage: node enable-void-knight.mjs CURRENT-index.js NEW-index.js');
 writeFileSync(process.argv[3],enableVoidKnight(readFileSync(process.argv[2],'utf8')),{flag:'wx'});
 console.log('Void Knight added at 1600 shards. Deploy NEW-index.js with existing package/settings/storage.');
}
