import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {attachStandaloneClans} from '../../Server/commerce/node/attach-standalone-clans.mjs';
import {enableFriendStickerGifts} from '../../Server/commerce/node/enable-friend-sticker-gifts.mjs';
import {splitIndexFive} from '../../Server/commerce/node/split-index-five.mjs';
let checks=0;
for(const name of ['node-2026-10-22.2.js','node-2026-10-22.2-cloud-export.js']){
  const original=readFileSync(new URL('../ClanRestore/fixtures/'+name,import.meta.url),'utf8');
  const source=await attachStandaloneClans(original);
  for(const before of [source,source.replace(/const\s+ITEMS\s*=\s*\{/,match=>match+'future_cosmetic:item(123),')]){
    const after=enableFriendStickerGifts(before);
    const boundary=after.indexOf('\n// DF_FRIEND_STICKER_GIFTS_BEGIN v1\n');
    const restored=after.slice(0,boundary).replace('\n  if (dfGiftRoutes.has(route)) return await dfFriendGiftDispatch(account, route, request);','').replace('.friend-gifts.1','');
    assert.equal(restored,before,'All pre-existing account source must remain unchanged');checks++;
    assert.throws(()=>enableFriendStickerGifts(after),/already installed/);checks++;
    const parts=splitIndexFive(after);assert.equal(parts.length,5);assert.equal(parts.join(''),after);checks+=2;
    const withoutImports=after.replace(/\bimport\s*(?:\{[^}]*\}|[A-Za-z_$][\w$]*)\s*from\s*(['"])[^'"]+\1\s*;?/g,'');
    new Function(withoutImports);checks++;
  }
}
assert.throws(()=>enableFriendStickerGifts('const SERVER_REVISION="custom";'),/source needs review/);checks++;
assert.equal(splitIndexFive('a\r\nb\r\nc\r\nd\r\ne🙂\r\n').join(''),'a\r\nb\r\nc\r\nd\r\ne🙂\r\n');checks++;
console.log(`${checks} source-preservation, duplicate-patch, syntax and lossless five-part checks passed.`);
