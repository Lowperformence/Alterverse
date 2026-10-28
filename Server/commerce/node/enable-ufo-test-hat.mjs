// Catalog-only patch for the current Node Cloud Run source. Does not deploy.
// Usage: node enable-ufo-test-hat.mjs /path/to/current/index.js --write
import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const catalog = JSON.parse(await readFile(new URL('../../../Assets/Alterverse/Resources/Commerce/ShardCatalog.json', import.meta.url)));
export const headwearItems = catalog.cosmetics.filter(x => x.writes?.some(w => w.key.startsWith('DF_HeadwearOwned_')));
for (const item of headwearItems) {
  if (!/^[a-z0-9_]+$/.test(item.id) || !Number.isSafeInteger(item.price) || item.price <= 0 ||
      item.writes.length !== 1 || !/^DF_HeadwearOwned_[A-Za-z0-9]+$/.test(item.writes[0].key) ||
      item.writes[0].kind !== 0 || item.writes[0].number !== 1)
    throw new Error('Invalid headwear catalog contract; no source changed.');
}
const line = item => `  ${item.id}: item(${item.price}, [w('${item.writes[0].key}')]),\n`;
export const entries = headwearItems.map(line).join('');
export const entry = line(headwearItems.find(x => x.id === 'ufo_test_hat'));

export function enableShopHeadwear(source) {
  const anchor = /const ITEMS = \{\r?\n/g;
  if ([...source.matchAll(anchor)].length !== 1 || !source.includes('const item =') || !source.includes('const w ='))
    throw new Error('Expected Node catalog layout was not found; no source changed.');
  const missing = [];
  let result = source;
  for (const item of headwearItems) {
    const key = item.writes[0].key;
    const mentions = new RegExp(`(?:\\b${item.id}|['"]${item.id}['"])\\s*:`, 'g');
    const found = [...result.matchAll(mentions)];
    if (found.length === 0) { missing.push(line(item)); continue; }
    const pattern = new RegExp(`^[ \\t]*(?:${item.id}|'${item.id}'|"${item.id}")\\s*:\\s*item\\(\\s*(\\d+)\\s*,\\s*\\[\\s*w\\(\\s*(['"])${key}\\2\\s*\\)\\s*\\]\\s*\\)\\s*,[ \\t]*(?:\\r?\\n|$)`, 'gm');
    const rows = [...result.matchAll(pattern)];
    if (found.length !== 1 || rows.length !== 1)
      throw new Error(`Existing ${item.id} ownership/layout differs; inspect it before changing source.`);
    const oldPrice = Number(rows[0][1]);
    // Only the explicitly superseded 1-shard UFO may be upgraded automatically.
    if (oldPrice !== item.price && !(item.id === 'ufo_test_hat' && oldPrice === 1))
      throw new Error(`Existing ${item.id} price differs; inspect it before changing source.`);
    result = result.replace(pattern, line(item));
  }
  if (missing.length) result = result.replace(anchor, match => match + missing.join(''));
  return result;
}
// Keep existing tooling compatible; this now configures all cataloged shop hats.
export const enableUfoTestHat = enableShopHeadwear;

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (!process.argv[2]) throw new Error('Pass the current Node index.js path; add --write to apply.');
  const path = resolve(process.argv[2]);
  const before = await readFile(path, 'utf8');
  const after = enableShopHeadwear(before);
  if (after === before) console.log('All shop hats already match the shared catalog.');
  else if (process.argv.includes('--write')) {
    await writeFile(path + '.before-headwear-shop', before, { flag: 'wx' });
    await writeFile(path, after);
    console.log('Updated only shop-hat catalog entries. Backup retained. Deployment is still required.');
  } else console.log('Ready: only shop-hat catalog entries will change. No source written.');
}
