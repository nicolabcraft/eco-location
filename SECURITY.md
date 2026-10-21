# Politique de sécurité / Security policy

## Versions suivies / Supported versions

Seule la **dernière version publiée** de Location reçoit des correctifs de sécurité.
Mettez à jour avant de signaler un problème.

Only the **latest release** of Location receives security fixes. Please update
before reporting an issue.

## Signaler une vulnérabilité / Reporting a vulnerability

**N'ouvrez pas d'issue publique pour une faille de sécurité.** Utilisez le
signalement privé de GitHub : onglet **Security** du dépôt, puis
**Report a vulnerability**
([lien direct](https://github.com/nicolabcraft/eco-location/security/advisories/new)).

**Do not open a public issue for a security problem.** Use GitHub private
vulnerability reporting: the repository's **Security** tab, then
**Report a vulnerability**
([direct link](https://github.com/nicolabcraft/eco-location/security/advisories/new)).

Indiquez / Please include:

- la version de Location et d'Eco / the Location and Eco versions;
- la description du problème et son impact / what the issue is and its impact;
- les étapes pour le reproduire / steps to reproduce;
- si possible, une piste de correction / a suggested fix, if you have one.

Vous recevrez une réponse sous 7 jours. Une fois le correctif publié, l'avis de
sécurité est rendu public et vous êtes crédité si vous le souhaitez.

You will get an answer within 7 days. Once a fix is released, the advisory is
published and you are credited if you wish.

## Bon à savoir / Good to know

- Le mod tourne dans le serveur Eco : une faille peut toucher le serveur entier.
  Testez toujours sur un serveur de test.
- Ne publiez jamais de clé d'administration (`APIAdminAuthToken`), de mot de
  passe ou de jeton dans une issue, une pull request ou une capture d'écran.

- The mod runs inside the Eco server: a flaw can affect the whole server.
  Always test on a test server.
- Never post an admin key (`APIAdminAuthToken`), password or token in an issue,
  pull request or screenshot.
