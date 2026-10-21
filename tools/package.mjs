// Fabrique l'archive de Location, prete a decompresser a la racine d'un serveur Eco :
//   Mods/UserCode/Location/*.cs        le mod (compile par le serveur)
//   Mods/Translations/Location.csv     ses traductions (lues par Eco au demarrage)
//   README.md, LICENSE.md, NOTICE.md
// Usage : node tools/package.mjs [version]   (version : argument, LOCATION_VERSION, ou VERSION)
import { cp, mkdir, readdir, readFile, rm } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import { join } from 'node:path';

const version = (process.argv[2] ?? process.env.LOCATION_VERSION ?? (await readFile('VERSION', 'utf8'))).trim().replace(/^v/, '');
const SORTIE = 'release/paquet';
const zip = `release/Location-${version}.zip`;

await rm(SORTIE, { recursive: true, force: true });
await mkdir(join(SORTIE, 'Mods/UserCode/Location'), { recursive: true });
await mkdir(join(SORTIE, 'Mods/Translations'), { recursive: true });
for (const f of (await readdir('usercode/Location')).filter((n) => n.endsWith('.cs'))) {
  await cp(join('usercode/Location', f), join(SORTIE, 'Mods/UserCode/Location', f));
}
await cp('translations/Location.csv', join(SORTIE, 'Mods/Translations/Location.csv'));
for (const f of ['README.md', 'LICENSE.md', 'NOTICE.md']) await cp(f, join(SORTIE, f));

await rm(zip, { force: true });
try {
  execFileSync('zip', ['-qr', join('..', `Location-${version}.zip`), '.'], { cwd: SORTIE, stdio: 'pipe' });
} catch {
  execFileSync('powershell', ['-NoProfile', '-Command', `Compress-Archive -Path '${SORTIE}/*' -DestinationPath '${zip}' -Force`], { stdio: 'pipe' });
}
console.log(`archive : ${zip}`);
