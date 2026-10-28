import assert from 'node:assert/strict';
import vm from 'node:vm';
import { enableBolt7Weapons,items } from './enable-bolt7-weapons.mjs';
const source="const item = (price,writes)=>({price,writes});\nconst ITEMS = {\n  existing: item(12,[{key:'DF_Existing',kind:0,number:1}]),\n};\n";
const patched=enableBolt7Weapons(source);
const products=vm.runInNewContext(patched+'ITEMS');
assert.equal(products.existing.price,12);
for(const item of items){assert.equal(products[item.id].price,500);assert.equal(JSON.stringify(products[item.id].writes),JSON.stringify(item.writes));}
assert.equal(enableBolt7Weapons(patched),patched);
assert.throws(()=>enableBolt7Weapons(source.replace('const ITEMS','let ITEMS')));
assert.throws(()=>enableBolt7Weapons(patched.replace('weapon_10_0: item(500','weapon_10_0: item(1')));
assert.throws(()=>enableBolt7Weapons(patched.replace('const ITEMS = {','const ITEMS = {\n  weapon_10_0: item(500,[]),')));
console.log('PASS guarded Node BOLT-7 catalog addition, exact writes, existing products, idempotency and conflict rejection. No deployment.');

