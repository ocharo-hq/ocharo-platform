const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = __dirname;
const source = fs.readFileSync(path.join(root, 'source', 'compact-master.png'));
const sourceHash = crypto.createHash('sha256').update(source).digest('hex');
const width = source.readUInt32BE(16), height = source.readUInt32BE(20);
const uri = 'data:image/png;base64,' + source.toString('base64');
const ink = '#20211E', white = '#FFFFFF', ivory = '#F7F3E9';
const specs = [
  { name: 'ocharo-symbol-ink', color: ink },
  { name: 'ocharo-tile', color: white, background: ink },
  { name: 'ocharo-micro', color: ink },
  { name: 'ocharo-micro-reverse', color: white },
  { name: 'ocharo-avatar', color: ink, background: ivory },
].map(spec => ({ ...spec, w: width, h: height }));
fs.mkdirSync(path.join(root, 'assets'), { recursive: true });
for (const spec of specs) {
  const background = spec.background ? `<rect width="${width}" height="${height}" fill="${spec.background}"/>` : '';
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" role="img" aria-label="${spec.name}"><metadata>Generated compact master SHA256 ${sourceHash}. Shared geometry for every compact export.</metadata><defs><mask id="compact-master" maskUnits="userSpaceOnUse" maskContentUnits="userSpaceOnUse" x="0" y="0" width="${width}" height="${height}" style="mask-type:alpha"><image href="${uri}" width="${width}" height="${height}"/></mask></defs>${background}<rect width="${width}" height="${height}" fill="${spec.color}" mask="url(#compact-master)"/></svg>`;
  fs.writeFileSync(path.join(root, 'assets', spec.name + '.svg'), svg);
}
fs.writeFileSync(path.join(root, 'export-spec.json'), JSON.stringify({ sourceHash, width, height, originalWordmarkPreserved: true, compactRegenerated: true, specs }, null, 2) + '\n');
console.log(JSON.stringify({ sourceHash, width, height, compactCompositions: specs.length }));
