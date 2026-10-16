// Mechanical launcher layout export: keeps the existing logo artwork unchanged.
// Requires Node.js and sharp (npm install --no-save sharp).
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');

const root = path.resolve(__dirname, '../..');
const branding = path.join(root, 'Assets/Alterverse/Branding');
const source = fs.readFileSync(path.join(branding, 'DimensionsFrontAppIcon.png'));
const image = `data:image/png;base64,${source.toString('base64')}`;
const background = '<rect width="512" height="512" fill="#050813"/>';
// Match the existing rounded frame and hide its baked-in light outer corners.
const logo = (side) => {
  const inset = (512-side)/2;
  return `<defs><clipPath id="frame"><rect x="${inset}" y="${inset}" width="${side}" height="${side}" rx="${side/4}"/></clipPath></defs><image x="${inset}" y="${inset}" width="${side}" height="${side}" href="${image}" clip-path="url(#frame)"/>`;
};
const svg = (body) => Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">${body}</svg>`);

async function main() {
  // All corners of the 216px square fit within Android's central 66/108 safe circle.
  // The legacy image uses the equivalent size after the adaptive 72/108 mask crop.
  for (const [name, body] of [
    ['Background', background],
    ['Foreground', logo(216)],
    ['Legacy', background + logo(324)],
  ]) {
    await sharp(svg(body)).png().toFile(path.join(branding, `DimensionsFrontLauncher${name}.png`));
  }
  console.log('Exported Android launcher layers and legacy image from the original logo.');
}
main().catch(error => { console.error(error); process.exitCode = 1; });
