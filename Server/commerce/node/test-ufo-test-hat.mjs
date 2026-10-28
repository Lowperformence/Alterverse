import assert from 'node:assert/strict';
import vm from 'node:vm';
import {enableShopHeadwear, headwearItems, entries, entry} from './enable-ufo-test-hat.mjs';
const source="const w = key => ({key,kind:0,number:1});\nconst item = (price,writes) => ({price,writes});\nconst ITEMS = {\n  voidfall: item(2000, [w('DF_SkinOwned_Voidfall')]),\n};\n";
const patched=enableShopHeadwear(source);
assert.equal(patched.replace(entries,''),source);
assert.equal(enableShopHeadwear(patched),patched);
const items=vm.runInNewContext(patched+';ITEMS');
assert.equal(headwearItems.length,4);
for(const expected of headwearItems) {
 assert.equal(items[expected.id].price,100);
 assert.deepEqual(JSON.parse(JSON.stringify(items[expected.id].writes)),expected.writes);
}
assert.equal(items.voidfall.price,2000);
assert.ok(!Object.keys(items).some(x=>x.includes('beta')));
const legacy=source.replace('const ITEMS = {\n',"const ITEMS = {\n  ufo_test_hat: item(1, [w('DF_HeadwearOwned_UfoTestHat')]),\n");
assert.equal(enableShopHeadwear(enableShopHeadwear(legacy)),enableShopHeadwear(legacy));
assert.ok(enableShopHeadwear(legacy).includes(entry));
assert.equal(enableShopHeadwear(patched.replace('ufo_test_hat:',"'ufo_test_hat':")),patched);
assert.throws(()=>enableShopHeadwear(patched.replace('ufo_test_hat: item(100,','ufo_test_hat: item(2,')));
assert.throws(()=>enableShopHeadwear(patched.replace('wizard_hat: item(100,','wizard_hat: item(1,')));
assert.throws(()=>enableShopHeadwear(patched.replace('DF_HeadwearOwned_WizardHat','DF_HeadwearOwned_BetaCap')));
assert.throws(()=>enableShopHeadwear(patched.replace('const ITEMS = {\n','const ITEMS = {\n'+entry)));
assert.throws(()=>enableShopHeadwear('different source layout'));
console.log('PASS four 100-shard hat contracts, legacy UFO upgrade, guarded catalog-only patch, duplicates and idempotence.');
