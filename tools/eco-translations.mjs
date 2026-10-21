#!/usr/bin/env node
// Traductions des textes en jeu d'un mod Eco, au format des fichiers
// Mods/Translations/*.csv lus par le serveur : "Context","English",<langues...>.
// La colonne English est la cle ; chaque autre colonne porte le nom d'une
// langue d'Eco (French, German, Spanish...).
//
//   node tools/eco-translations.mjs extract <csv> <dossier-sources...>
//       met le CSV a jour : ajoute les nouveaux textes, retire ceux qui ne sont
//       plus dans le code, garde toutes les traductions existantes.
//   node tools/eco-translations.mjs check <csv> <dossier-sources...>
//       echoue si le CSV n'est pas a jour ; affiche la couverture par langue.
//
// Textes reconnus dans le code C# (sources en anglais) :
//   Localizer.DoStr("..."), MsgLocStr("..."), FailLocStr("..."), ErrorLocStr("..."),
//   InfoBoxLocStr("...") ; Localizer.Do($"..."), MsgLoc($"..."), ConfirmBoxLoc($"..."),
//   FailLoc($"..."), ErrorLoc($"..."), InfoBoxLoc($"...") ; attributs LocDisplayName,
//   LocDescription, CreateComponentTabLoc, Description, ChangeParentConfigLoc
//   (nouveau nom), Interaction (description).
// Dans une chaine interpolee, chaque {expression} devient {0}, {1}... comme le
// fait Eco a l'execution : « Rented by {0}. Free in {1}. ».
import { readFile, readdir, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { join } from 'node:path';

/** Langues d'Eco (enum SupportedLanguage), pour l'aide et la verification des colonnes. */
export const LANGUES_ECO = [
  'French', 'Spanish', 'German', 'Korean', 'BrazilianPortuguese', 'SimplifedChinese', 'Russian',
  'Italian', 'Portuguese', 'Hungarian', 'Japanese', 'Norwegian', 'Polish', 'Dutch', 'Romanian',
  'Danish', 'Czech', 'Swedish', 'Ukrainian', 'Greek', 'Arabic', 'Vietnamese', 'Turkish',
];

const SIMPLES = ['Localizer\\.DoStr', 'MsgLocStr', 'FailLocStr', 'ErrorLocStr', 'InfoBoxLocStr', 'LocDisplayName', 'LocDescription', 'CreateComponentTabLoc', 'Description'];
const INTERPOLEES = ['Localizer\\.Do', 'MsgLoc', 'ConfirmBoxLoc', 'FailLoc', 'ErrorLoc', 'InfoBoxLoc'];

/** Lit un litteral C# "..." a partir de l'indice du guillemet ouvrant. */
function litteral(src, i) {
  let out = '';
  for (let j = i + 1; j < src.length; j++) {
    const c = src[j];
    if (c === '\\') {
      const n = src[j + 1];
      out += n === 'n' ? '\n' : n === 't' ? '\t' : n;
      j++;
    } else if (c === '"') {
      return { texte: out, fin: j + 1 };
    } else {
      out += c;
    }
  }
  return null;
}

/** Lit $"..." et remplace chaque {expression[:format]} par {n[:format]}. */
function interpole(src, i) {
  let out = '';
  let n = 0;
  for (let j = i + 2; j < src.length; j++) {
    const c = src[j];
    if (c === '\\') { const s = src[j + 1]; out += s === 'n' ? '\n' : s; j++; continue; }
    if (c === '{' && src[j + 1] === '{') { out += '{{'; j++; continue; }
    if (c === '}' && src[j + 1] === '}') { out += '}}'; j++; continue; }
    if (c === '"') return { texte: out, fin: j + 1 };
    if (c === '{') {
      // Expression : jusqu'a l'accolade fermante de meme niveau, en sautant les chaines.
      let profondeur = 1;
      let k = j + 1;
      let format = '';
      let dansChaine = false;
      for (; k < src.length && profondeur > 0; k++) {
        const d = src[k];
        if (dansChaine) { if (d === '\\') k++; else if (d === '"') dansChaine = false; continue; }
        if (d === '"') dansChaine = true;
        else if (d === '(' || d === '[' || d === '{') profondeur++;
        else if (d === ')' || d === ']' || d === '}') profondeur--;
        else if (d === ':' && profondeur === 1 && src[k - 1] !== '?' && !/\?[^:]*$/.test(src.slice(j + 1, k))) {
          const fin = src.indexOf('}', k);
          format = src.slice(k, fin);
          k = fin;
          profondeur = 0;
          break;
        }
      }
      out += `{${n++}${format}}`;
      j = k - (format ? 0 : 1);
      continue;
    }
    out += c;
  }
  return null;
}

export function extraireTextes(source) {
  const textes = [];
  const ajouter = (t) => { if (t && /[A-Za-z]/.test(t)) textes.push(t); };
  for (const nom of SIMPLES) {
    const re = new RegExp(`(?<!\\w)${nom}\\(\\s*"`, 'g');
    for (const m of source.matchAll(re)) { const r = litteral(source, m.index + m[0].length - 1); if (r) ajouter(r.texte); }
  }
  for (const nom of INTERPOLEES) {
    const re = new RegExp(`(?<!\\w)${nom}\\(\\s*\\$"`, 'g');
    for (const m of source.matchAll(re)) { const r = interpole(source, m.index + m[0].length - 2); if (r) ajouter(r.texte); }
  }
  // ChangeParentConfigLoc("Ancien", "Nouveau") : seul le nouveau nom est affiche.
  for (const m of source.matchAll(/ChangeParentConfigLoc\(\s*"[^"]*"\s*,\s*"/g)) { const r = litteral(source, m.index + m[0].length - 1); if (r) ajouter(r.texte); }
  // [Interaction(InteractionTrigger.X, "Description", ...)]
  for (const m of source.matchAll(/Interaction\(\s*InteractionTrigger\.\w+\s*,\s*"/g)) { const r = litteral(source, m.index + m[0].length - 1); if (r) ajouter(r.texte); }
  return textes;
}

async function fichiersCs(dossier) {
  const out = [];
  for (const e of await readdir(dossier, { withFileTypes: true, recursive: true })) {
    if (e.isFile() && e.name.endsWith('.cs')) out.push(join(e.parentPath ?? dossier, e.name));
  }
  return out.sort();
}

// --- CSV (guillemets doubles, separateur virgule, retours a la ligne permis) ---

function lireCsv(texte) {
  const lignes = [];
  let ligne = [];
  let champ = '';
  let guillemets = false;
  for (let i = 0; i < texte.length; i++) {
    const c = texte[i];
    if (guillemets) {
      if (c === '"' && texte[i + 1] === '"') { champ += '"'; i++; }
      else if (c === '"') guillemets = false;
      else champ += c;
    } else if (c === '"') guillemets = true;
    else if (c === ',') { ligne.push(champ); champ = ''; }
    else if (c === '\n' || c === '\r') {
      if (c === '\r' && texte[i + 1] === '\n') i++;
      ligne.push(champ); champ = '';
      if (ligne.some((x) => x !== '')) lignes.push(ligne);
      ligne = [];
    } else champ += c;
  }
  if (champ !== '' || ligne.length) { ligne.push(champ); lignes.push(ligne); }
  return lignes;
}

const cellule = (v) => `"${String(v ?? '').replace(/"/g, '""')}"`;

async function charger(chemin) {
  if (!existsSync(chemin)) return { colonnes: ['Context', 'English', 'French'], lignes: new Map() };
  const [entete, ...corps] = lireCsv((await readFile(chemin, 'utf8')).replace(/^﻿/, ''));
  const iAnglais = entete.indexOf('English');
  if (iAnglais < 0) throw new Error(`${chemin} : colonne English absente`);
  const lignes = new Map();
  for (const l of corps) lignes.set(l[iAnglais], Object.fromEntries(entete.map((c, i) => [c, l[i] ?? ''])));
  return { colonnes: entete, lignes };
}

async function textesDuCode(dossiers) {
  const tous = new Set();
  for (const d of dossiers) for (const f of await fichiersCs(d)) for (const t of extraireTextes(await readFile(f, 'utf8'))) tous.add(t);
  return [...tous];
}

function couverture(colonnes, lignes, cles) {
  return colonnes.filter((c) => c !== 'Context' && c !== 'English').map((langue) => {
    const faites = cles.filter((k) => (lignes.get(k)?.[langue] ?? '').trim() !== '').length;
    return { langue, faites, total: cles.length };
  });
}

const [commande, csv, ...dossiers] = process.argv.slice(2);
if (!['extract', 'check'].includes(commande) || !csv || dossiers.length === 0) {
  console.error('Usage : node tools/eco-translations.mjs <extract|check> <fichier.csv> <dossier-sources...>');
  process.exit(2);
}

const { colonnes, lignes } = await charger(csv);
const cles = await textesDuCode(dossiers);
const inconnues = colonnes.filter((c) => c !== 'Context' && c !== 'English' && !LANGUES_ECO.includes(c) && c !== 'Gibberish');
if (inconnues.length) console.warn(`Colonnes inconnues d'Eco (ignorees par le jeu) : ${inconnues.join(', ')}. Langues valides : ${LANGUES_ECO.join(', ')}`);

if (commande === 'extract') {
  const ajoutees = cles.filter((k) => !lignes.has(k));
  const retirees = [...lignes.keys()].filter((k) => !cles.includes(k));
  const corps = cles.map((k) => colonnes.map((c) => cellule(c === 'English' ? k : lignes.get(k)?.[c] ?? '')).join(','));
  await writeFile(csv, [colonnes.map(cellule).join(','), ...corps].join('\n') + '\n', 'utf8');
  console.log(`${csv} : ${cles.length} texte(s), ${ajoutees.length} ajoute(s), ${retirees.length} retire(s).`);
  for (const k of ajoutees) console.log(`  + ${k}`);
  for (const k of retirees) console.log(`  - ${k}`);
} else {
  const manquantes = cles.filter((k) => !lignes.has(k));
  const orphelines = [...lignes.keys()].filter((k) => !cles.includes(k));
  for (const c of couverture(colonnes, lignes, cles)) {
    console.log(`${c.langue.padEnd(20)} ${c.faites}/${c.total} (${c.total ? Math.round((c.faites / c.total) * 100) : 100} %)`);
  }
  // Une traduction doit garder les memes marqueurs {0}, {1}... que le texte anglais.
  const marqueurs = (t) => [...String(t).matchAll(/\{(\d+)(:[^}]*)?\}/g)].map((m) => m[1]).sort().join(',');
  let fautes = 0;
  for (const k of cles) {
    for (const c of colonnes.filter((x) => x !== 'Context' && x !== 'English')) {
      const v = lignes.get(k)?.[c] ?? '';
      if (v && marqueurs(v) !== marqueurs(k)) { console.error(`${c} : marqueurs differents pour « ${k} » -> « ${v} »`); fautes++; }
    }
  }
  if (fautes) process.exit(1);
  if (manquantes.length || orphelines.length) {
    for (const k of manquantes) console.error(`absent du CSV : ${k}`);
    for (const k of orphelines) console.error(`plus dans le code : ${k}`);
    console.error(`Le CSV n'est pas a jour : lancez « node tools/eco-translations.mjs extract ${csv} ${dossiers.join(' ')} ».`);
    process.exit(1);
  }
  console.log(`${csv} est a jour (${cles.length} texte(s)).`);
}
