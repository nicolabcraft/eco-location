# Fiche mod.io : Location 1.0.0

À copier dans le formulaire mod.io (jeu : Eco).

## Champs

| Champ | Valeur |
|---|---|
| Nom | Location : rent vehicles & boats |
| URL (name_id) | `location-rent-vehicles` |
| Logo | `logo.png` (1280×720, 16:9) |
| Fichier | `../release/Location-1.0.0.zip` |
| Version | `1.0.0` |
| Tags | Server, Economy, Vehicles, Laws (selon les tags proposés par mod.io pour Eco) |
| Compatibilité | Eco 0.14.2.1 |
| Licence | **PolyForm Noncommercial 1.0.0** (gratuit, réutilisable, pas d’usage commercial) |

**Résumé** (250 caractères max) :

> Louez vos véhicules et bateaux à d'autres joueurs : prix par heure, durée au choix, accès à la conduite et à la cale, fin automatique. La location est une action de jeu : les lois peuvent la taxer ou l'interdire.

## Description (français)

**Location** ajoute un onglet *Louer* à tous les véhicules du jeu : bateaux, charrettes, camions, engins.

**Pour le propriétaire**
- Proposer l'objet à la location, fixer le prix par heure (ex. 1 h = 150 crédits), la devise, le compte qui encaisse et la durée maximale (1 h à 7 jours).
- Mettre fin à une location à tout moment.
- Garder l'autorisation du véhicule fermée (pas « Tout le monde ») : seul le locataire reçoit l'accès, le temps de la location.

**Pour le locataire**
- Sans aucun accès au véhicule : **Ctrl + clic droit « Louer »**, choisir une durée (1 h, 2 h, 3 h… jusqu’à 7 j, prix affiché), payer depuis son compte.
- Conduire le véhicule et utiliser sa cale pendant toute la durée ; prolonger ou rendre l'objet plus tôt.
- Avertissements 5 min et 1 min avant la fin ; à l'échéance l'accès est retiré et le locataire descend du véhicule.

**Sécurité** : le locataire reçoit seulement l'accès « consommateur » : il ne peut ni ramasser le véhicule ni modifier ses droits.

**Lois** : chaque location déclenche l'action **LouerObjet** (catégorie Finance), avec le locataire, le propriétaire, l'objet, la durée, le montant et la devise. Une loi peut la taxer ou l'interdire (par exemple : pas de location de bateaux, ou taxe de 10 %). Si une loi refuse ou si le paiement échoue, rien ne se passe.

Le temps de location compte en temps de monde : il s'arrête quand le serveur est éteint.

**Installation** : abonnez le serveur au mod, ou copiez les fichiers de l'archive dans `Mods/UserCode/Location/`, puis redémarrez. Ne faites pas les deux : les fichiers seraient compilés deux fois et le serveur ne démarrerait pas.

Interface en français.

## Description (English)

**Location** adds a *Louer* (rent) tab to every vehicle in the game: boats, carts, trucks, machines.

**Owners** can offer the object for rent, set a price per hour (e.g. 1 h = 150 credits), the currency, the receiving bank account and a maximum duration (1 h to 7 days), and end a rental at any time.

**Renters** need no access: **Ctrl + right-click "Louer"** on the vehicle, pick a duration (price shown) and pay from their own account. Owners keep the vehicle's permissions private. They can drive the vehicle and use its storage for the whole rental, extend it, or return it early. They are warned 5 and 1 minutes before the end; when it expires, access is removed and they are dismounted.

**Safety**: renters only get *consumer* access, so they can neither pick up the vehicle nor change its permissions.

**Laws**: every rental triggers the **LouerObjet** game action (Finance category) with the renter, owner, object, duration, amount and currency, so laws can tax or forbid it. If a law blocks it or the payment fails, nothing happens.

Rental time counts in world time and stops while the server is off.

**Install**: subscribe your server to the mod, or copy the archive's files into `Mods/UserCode/Location/` and restart. Don't do both, or the files are compiled twice and the server won't start.

The in-game interface is in French.
