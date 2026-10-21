# Changelog

## 1.1.0 (2026-10-21)

### Français

**Nouveautés**
- Mod **traduisible** : les textes sont en anglais dans le code et traduits par Eco dans la langue de chaque joueur, grâce à `Mods/Translations/Location.csv` (fourni en français). Ajoutez une colonne au CSV pour une nouvelle langue.
- L'archive se décompresse directement à la racine du serveur (`Mods/UserCode/Location/` et `Mods/Translations/Location.csv`).
- Projet ouvert aux contributions : guide de contribution, modèles d'issues, intégration continue sur GitHub (compilation, vérification des véhicules et des traductions) et releases automatiques.

**Changements**
- L'onglet s'appelle *Vehicle Rental* en anglais (*Louer* en français) et l'interaction *Rent* (*Louer*). L'action de loi `LouerObjet` s'affiche « Louer un objet » ; ses champs gardent leurs noms, les lois existantes continuent de fonctionner.
- Prix affichés avec un point décimal quelle que soit la langue.

**Mise à jour depuis 1.0.0** : remplacez les fichiers de `Mods/UserCode/Location/`, ajoutez `Mods/Translations/Location.csv`, puis redémarrez.

### English

**New**
- **Translatable** mod: texts are in English in the code and translated by Eco into each player's language through `Mods/Translations/Location.csv` (French included). Add a column to the CSV for a new language.
- The archive unzips straight at the server root (`Mods/UserCode/Location/` and `Mods/Translations/Location.csv`).
- Open to contributions: contributing guide, issue templates, GitHub CI (build, vehicle and translation checks) and automatic releases.

**Changed**
- The tab is named *Vehicle Rental* (*Louer* in French) and the interaction *Rent*. The `LouerObjet` law action is shown as "Rent Object"; its fields keep their names, so existing laws keep working.
- Prices use a decimal point whatever the language.

**Updating from 1.0.0**: replace the files in `Mods/UserCode/Location/`, add `Mods/Translations/Location.csv`, then restart.

## 1.0.0 (2026-10-19)

Première version publique / first public release.

- Onglet de location sur les 25 véhicules : prix par heure, devise, compte, durée maximale.
- Location sans accès préalable par Ctrl + clic droit ; accès « consommateur » (conduite et cale) le temps de la location, retiré à l'échéance.
- Action de jeu `LouerObjet` pour les lois (taxe, interdiction).
