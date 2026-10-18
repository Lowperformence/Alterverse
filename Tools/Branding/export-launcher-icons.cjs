// Export Android adaptive launcher layers from the approved round portal artwork.
// Requires Node.js and sharp (npm install --no-save sharp).
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');

const root = path.resolve(__dirname, '../..');
const branding = path.join(root, 'Assets/Alterverse/Branding');
const source = path.join(branding, 'DimensionsFrontAppIcon.png');
const size = 512;
const emblemSize = 300; // 88% of Android's 72/108 visible circular mask (342px).

async function main() {
  const raw = Buffer.alloc(size * size * 4);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const t = x / (size - 1);
      const radial = Math.max(0, 1 - Math.hypot(x - 256, y - 256) / 370);
      const i = (y * size + x) * 4;
      raw[i] = Math.floor(3 + 12 * t + 5 * radial);
      raw[i + 1] = Math.floor(9 + 5 * radial);
      raw[i + 2] = Math.floor(38 + 27 * t + 9 * radial);
      raw[i + 3] = 255;
    }
  }
  const bg = await sharp(raw, { raw: { width: size, height: size, channels: 4 } }).png().toBuffer();
  const fg = await sharp(source).resize(emblemSize, emblemSize).ensureAlpha().png().toBuffer();
  const legacy = await sharp(source).resize(324, 324).ensureAlpha().png().toBuffer();
  await fs.promises.writeFile(path.join(branding, 'DimensionsFrontLauncherBackground.png'), bg);
  await sharp({ create: { width: size, height: size, channels: 4, background: '#00000000' } })
    .composite([{ input: fg, left: 106, top: 106 }])
    .png().toFile(path.join(branding, 'DimensionsFrontLauncherForeground.png'));
  await sharp(bg).composite([{ input: legacy, left: 94, top: 94 }])
    .png().toFile(path.join(branding, 'DimensionsFrontLauncherLegacy.png'));
  console.log('Exported large circular Dimensions Front portal icon.');
}

main().catch(error => { console.error(error); process.exitCode = 1; });
