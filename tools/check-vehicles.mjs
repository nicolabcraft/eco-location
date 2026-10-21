// Verifie que chaque classe etendue dans Vehicules.cs existe dans le jeu
// (Eco.Mods.dll du paquet NuGet Eco.ReferenceAssemblies, restaure par dotnet build).
// Une faute de frappe y creerait sinon une classe vide, sans onglet, sans erreur.
// Usage : node tools/check-vehicles.mjs   (apres « dotnet build »)
import { readFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';

const csproj = await readFile('EcoLocationMod.csproj', 'utf8');
const version = process.env.ECO_VERSION ?? /<EcoVersion[^>]*>([^<]+)</.exec(csproj)?.[1];
const racine = process.env.NUGET_PACKAGES ?? join(homedir(), '.nuget', 'packages');
const dll = join(racine, 'eco.referenceassemblies', version.toLowerCase(), 'lib', 'net10.0', 'Eco.Mods.dll');
if (!existsSync(dll)) {
  console.error(`Eco.Mods.dll introuvable (${dll}) : lancez d'abord « dotnet build ».`);
  process.exit(2);
}
// Les noms de types sont stockes en clair (UTF-8), termines par un octet nul, dans
// les metadonnees de l'assembly. Le compilateur partage les fins de chaines
// (« TruckObject » est rangee dans « SteamTruckObject ») : on cherche donc le nom
// suivi du nul. Une faute de frappe reste detectee.
const binaire = (await readFile(dll)).toString('latin1');
const classes = [...(await readFile('usercode/Location/Vehicules.cs', 'utf8')).matchAll(/partial class (\w+)/g)].map((m) => m[1]);
const absentes = classes.filter((c) => !binaire.includes(`${c}\0`));
for (const c of absentes) console.error(`Vehicules.cs : la classe ${c} n'existe pas dans Eco ${version}`);
if (absentes.length) process.exit(1);
console.log(`Vehicules.cs : ${classes.length} classe(s) de vehicule trouvees dans Eco ${version}.`);
