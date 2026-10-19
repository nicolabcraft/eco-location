<p align="center"><img src="modio/logo.png" alt="Location : louez vos véhicules et bateaux dans Eco" width="760"></p>

# Location : louez vos véhicules dans Eco

Mod serveur pour **Eco** (Strange Loop Games) : les joueurs peuvent louer un
véhicule ou un bateau à son propriétaire, pour une durée choisie et un prix par
heure (par exemple 1 h pour 150 crédits). La location est une **action de jeu** :
vos lois peuvent la taxer ou l'interdire.

*[English below](#english)*

## Fonctionnement

**Propriétaire**, dans l'onglet **Louer** du véhicule :

- cochez *Proposer à la location* ;
- fixez le *Prix par heure*, la *Devise* et le *Compte qui encaisse* ;
- fixez la *Durée maximale* (1 à 168 heures, 24 par défaut) ;
- *Mettre fin à la location* reprend le véhicule à tout moment.

Gardez l'autorisation du véhicule **fermée** (pas « Tout le monde ») : la
location ouvre l'accès au seul locataire, le temps de la location.

**Locataire**, sans aucun accès au véhicule :

- visez le véhicule et faites **Ctrl + clic droit : « Louer (150 Crédits / h) »** ;
- choisissez une durée (1 h · 150, 2 h · 300…) : le prix est payé depuis votre compte personnel ;
- pendant la location, conduisez et utilisez la **cale** (prendre et déposer) ;
- l'onglet *Louer* permet de **prolonger** ou de **rendre maintenant**.

Le locataire est prévenu 5 minutes puis 1 minute avant la fin. À l'échéance, il
perd l'accès et descend du véhicule. Il ne reçoit que l'accès « consommateur » :
il ne peut ni ramasser le véhicule ni changer ses droits. Le temps compte en
temps de monde et s'arrête quand le serveur est éteint.

## Lois

Chaque location et chaque prolongation déclenche l'action **LouerObjet**
(catégorie Finance) :

| Champ | Contenu |
|---|---|
| `Locataire` | le citoyen qui loue |
| `Proprietaire` | le propriétaire du véhicule |
| `ObjetLoue` | le type de véhicule |
| `Heures` | la durée louée |
| `PrixParHeure` | le prix demandé pour une heure |
| `CurrencyAmount`, `Currency` | le montant payé et la devise |
| `SourceBankAccount`, `TargetBankAccount` | les comptes du locataire et du propriétaire |
| `ActionLocation` | l'endroit où se trouve le véhicule |

Exemples : une taxe de 10 % sur `CurrencyAmount`, ou l'interdiction de louer
les bateaux avec une condition sur `ObjetLoue`. Si une loi refuse, ou si le
paiement échoue, rien ne se passe.

## Installation

Copiez le dossier `usercode/Location/` dans `Mods/UserCode/` du serveur, puis
redémarrez. Pas de DLL, pas de réglage. Ou abonnez votre serveur au mod sur
mod.io, mais **pas les deux à la fois** : les fichiers seraient compilés deux
fois et le serveur ne démarrerait pas.

Les 25 véhicules du jeu reçoivent l'onglet ; pour en exclure un, supprimez sa
ligne dans `Vehicules.cs`. Compatibilité : Eco 0.14.2.1.

## Vérifier la compilation

```bash
dotnet build -p:EcoServerDir="D:/MonServeurEco"
```

Le projet compile les fichiers du mod contre l'EcoModKit livré avec le serveur
(`EcoModKit/ReferenceAssemblies`). `Vehicules.cs` est exclu : il étend les
classes du jeu, que seul le serveur compile. Le compilateur du serveur reste
plus strict (pas de `Console`, références limitées) : le test final est le
démarrage d'un serveur.

## Licence

Gratuit et à code ouvert, sous **PolyForm Noncommercial 1.0.0** (`LICENSE.md`) :
utilisez-le, modifiez-le, partagez vos versions, mais **jamais à des fins
commerciales**. Eco est un jeu de Strange Loop Games ; ce mod est un projet
communautaire indépendant.

---

## English

**Location** lets players rent a vehicle or boat from its owner, for a chosen
duration and a price per hour (e.g. 150 credits for 1 hour). Renting is a
**game action**, so your laws can tax or forbid it. The in-game interface is in
French.

**Owners**, in the vehicle's **Louer** tab: enable renting, set the price per
hour, the currency, the receiving account and the maximum duration (1 to 168
hours). They can end a rental at any time. Keep the vehicle's permissions
private: renting grants access to the renter only, for the rental's duration.

**Renters** need no access: aim at the vehicle and use **Ctrl + right click
"Louer"**, pick a duration (price shown) and pay from their personal account.
While renting they can drive the vehicle and use its storage, extend the rental
or return it early. They are warned 5 and 1 minutes before the end; when it
expires, access is removed and they are dismounted. Renters only get consumer
access: they can neither pick up the vehicle nor change its permissions.

**Laws**: every rental triggers the **LouerObjet** game action (Finance
category) with the renter, owner, vehicle type, hours, price, amount, currency,
bank accounts and location. If a law blocks it or the payment fails, nothing
happens.

**Install**: copy `usercode/Location/` into the server's `Mods/UserCode/` and
restart, or subscribe your server on mod.io, but not both (the files would be
compiled twice and the server would not start). Compatibility: Eco 0.14.2.1.

**License**: free and source available under **PolyForm Noncommercial 1.0.0**:
use it, modify it, share your versions, but never use it commercially. Eco is a
game by Strange Loop Games; this mod is an independent community project.
