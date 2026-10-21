# Contribuer à Location

Merci de votre aide ! Corrections, idées, traductions : tout est bienvenu.
*[English below](#english)*

## Signaler un problème ou proposer une idée

Ouvrez une [issue](../../issues/new/choose) avec le modèle qui correspond (bug,
idée, traduction). Pour un bug, indiquez la version d'Eco et de Location, le
véhicule concerné, ce que vous avez fait et ce qui s'est passé.

## Traduire

Tous les textes du mod sont dans `translations/Location.csv`, au format lu par
Eco (`Mods/Translations/`) : une colonne `English` (la référence, à ne pas
modifier) et une colonne par langue, au nom exact d'Eco : `French`, `German`,
`Spanish`, `Italian`, `Portuguese`, `BrazilianPortuguese`, `Russian`, `Polish`,
`Dutch`, `Turkish`, `Japanese`, `Korean`, `SimplifedChinese`…

- **Ajouter une langue** : ajoutez une colonne avec son nom et traduisez chaque
  ligne. Gardez les marqueurs `{0}`, `{1:00}` et `%PrixLocation%` à l'identique.
- Un tableur convient (LibreOffice, Excel) : enregistrez en CSV UTF-8, virgule
  comme séparateur, guillemets autour des textes.
- Vérifiez : `node tools/eco-translations.mjs check translations/Location.csv usercode/Location`.

Vous pouvez aussi ouvrir une issue « Traduction » avec vos textes : nous les
intégrerons.

## Branches et versions

| Branche | Rôle |
|---|---|
| `main` | **Versions publiées** uniquement. Chaque version y porte un tag `vX.Y.Z`, qui déclenche la release automatique. On n'y pousse jamais directement. |
| `dev` | **Intégration** : la prochaine version en préparation. Toutes les pull requests visent `dev`. |
| `feature/<sujet>` | Une **nouvelle fonction**, partie de `dev` (ex. `feature/location-a-la-journee`). |
| `fix/<sujet>` | Une **correction**, partie de `dev` (ex. `fix/fin-de-location`). |

Travailler sur une fonction :

```bash
git checkout dev && git pull
git checkout -b feature/mon-sujet
# ... modifications, commits ...
git push -u origin feature/mon-sujet
# puis pull request vers dev
```

La CI vérifie chaque pull request et chaque push sur `main` et `dev`, et fournit
un paquet de test dans l'onglet *Actions*.

Publier une version (mainteneurs) :

1. mettre à jour `CHANGELOG.md` et le numéro de version (`VERSION`) sur `dev` ;
2. pull request `dev` → `main`, puis fusion ;
3. tag sur `main` : `git tag vX.Y.Z && git push origin vX.Y.Z` ; le workflow
   *Release* fabrique l'archive et publie la release.

Une correction urgente peut partir de `main` (`fix/…`), puis être fusionnée dans
`main` **et** `dev`.

## Proposer du code

1. Forkez le dépôt et créez une branche `feature/…` ou `fix/…` à partir de `dev`.
2. Outils : .NET 10 SDK et Node.js 20 ou plus. **Aucun serveur Eco n'est
   nécessaire** : les assemblies d'Eco viennent du paquet NuGet officiel
   `Eco.ReferenceAssemblies`.
3. Avant d'envoyer :
   ```bash
   dotnet build -c Release
   node tools/check-vehicles.mjs
   node tools/eco-translations.mjs check translations/Location.csv usercode/Location
   ```
   `Vehicules.cs` n'est pas compilé par ce projet (il étend les classes du jeu) :
   `check-vehicles` vérifie que chaque véhicule existe bien dans Eco.
4. Testez sur un serveur Eco local : une erreur dans `Mods/UserCode` empêche le
   serveur de démarrer.
5. Ouvrez une pull request **vers `dev`** : la CI refait ces vérifications et fabrique un
   paquet de test, téléchargeable depuis l'onglet *Actions*.

### Règles

- Les textes du code sont en **anglais** et passent par `Localizer.Do`,
  `Localizer.DoStr`, `MsgLoc`, `MsgLocStr` ou un attribut `Loc…` : Eco les traduit
  dans la langue de chaque joueur. Après un ajout ou une modification de texte :
  `node tools/eco-translations.mjs extract translations/Location.csv usercode/Location`.
- Les noms des propriétés de l'action `LouerObjet` sont utilisés par les lois
  existantes : ne les renommez pas.

## Sécurité

Ne signalez jamais une faille dans une issue publique : voir [SECURITY.md](SECURITY.md).

## Licence

En contribuant, vous acceptez que votre contribution soit publiée sous la
licence du projet, [PolyForm Noncommercial 1.0.0](LICENSE.md) : gratuit,
réutilisable, jamais commercial.

---

## English

Thanks for helping! Fixes, ideas and translations are all welcome.

- **Report a bug or suggest an idea**: open an [issue](../../issues/new/choose)
  with the matching template.
- **Translate**: every text lives in `translations/Location.csv` (Eco's
  `Mods/Translations` format). Add a column named after the Eco language
  (`German`, `Spanish`, `Italian`, `Russian`, `Polish`…), keep `{0}` markers and
  `%PrixLocation%`, save as UTF-8 CSV. Check with
  `node tools/eco-translations.mjs check translations/Location.csv usercode/Location`.
  You can also just open a "Translation" issue with your texts.
- **Code**: .NET 10 SDK and Node.js 20+; no Eco server needed to build. Run
  `dotnet build -c Release`, `node tools/check-vehicles.mjs` and the translation
  check before opening a pull request, and test on a local Eco server. Source
  texts are English and go through `Localizer` / `MsgLoc` / `Loc…` attributes;
  run the `extract` command after changing them. Do not rename `LouerObjet`
  properties: existing laws reference them.
- **Branches**: `main` only holds released versions (each tagged `vX.Y.Z`,
  which triggers the automatic release); `dev` is the integration branch for the
  next version, and **every pull request targets `dev`**; work in
  `feature/<topic>` or `fix/<topic>` branches created from `dev`. To release,
  maintainers update `CHANGELOG.md` and the version (`VERSION`) on `dev`, merge
  `dev` into `main` through a pull request, then push a `vX.Y.Z` tag on `main`.
- **Security**: report vulnerabilities privately, see [SECURITY.md](SECURITY.md).
- **License**: contributions are published under the project's
  [PolyForm Noncommercial 1.0.0](LICENSE.md) license.
