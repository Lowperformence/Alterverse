// Apply only the requested catalog item to the current Node Cloud Run source.
// Usage: node enable-ufo-test-hat.mjs /path/to/index.js --write
import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const entry = "  ufo_test_hat: item(1, [w('DF_HeadwearOwned_UfoTestHat')]),\n";
export function enableUfoTestHat(source) {
  if (/(?:\bufo_test_hat|['"]ufo_test_hat['"])\s*:/.test(source)) {
    if (source.includes(entry.trim())) return source;
    throw new Error('An existing UFO item differs; inspect its price/ownership before changing it.');
  }
  const anchor = 'const ITEMS = {\n';
  if (source.split(anchor).length !== 2 || !source.includes('const item =') || !source.includes('const w ='))
    throw new Error('Expected Node catalog layout was not found; no file changed.');
  return source.replace(anchor, anchor + entry);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (!process.argv[2]) throw new Error('Pass the current Node index.js path; add --write to apply.');
  const path = resolve(process.argv[2]);
  const before = await readFile(path, 'utf8');
  const after = enableUfoTestHat(before);
  if (after === before) console.log('UFO item already has price 1 and the correct ownership key.');
  else if (process.argv.includes('--write')) {
    await writeFile(path + '.before-ufo-test-hat', before, { flag: 'wx' });
    await writeFile(path, after);
    console.log('Added UFO TEST HAT for 1 shard. Backup retained. Deployment is still required.');
  } else console.log('Ready: one catalog entry will be added; no source file written.');
}
