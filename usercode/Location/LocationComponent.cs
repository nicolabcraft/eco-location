namespace EcoLocation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Eco.Core.Controller;
    using Eco.Core.Utils;
    using Eco.Gameplay.Components;
    using Eco.Gameplay.Components.Auth;
    using Eco.Gameplay.Economy;
    using Eco.Gameplay.Economy.Transfer;
    using Eco.Gameplay.GameActions;
    using Eco.Gameplay.Items;
    using Eco.Gameplay.Objects;
    using Eco.Gameplay.Interactions.Interactors;
    using Eco.Gameplay.Players;
    using Eco.Gameplay.Systems.EnvVars;
    using Eco.Gameplay.UI;
    using Eco.Gameplay.Utils;
    using Eco.Shared.Items;
    using Eco.Shared.Localization;
    using Eco.Shared.Logging;
    using Eco.Shared.Networking;
    using Eco.Shared.Networking.Auth;
    using Eco.Shared.Serialization;
    using Eco.Shared.SharedTypes;
    using Eco.Simulation.Time;

    /// <summary>
    /// Onglet « Location » d'un objet (vehicule, bateau...).
    ///
    /// Le proprietaire fixe un prix par heure, une devise, le compte qui encaisse et
    /// une duree maximale. Un autre citoyen choisit une duree dans la grille, confirme,
    /// paie : il recoit l'acces « consommateur » de l'objet (conduire, s'asseoir) jusqu'a
    /// l'echeance, sans jamais pouvoir le ramasser ni changer ses droits.
    ///
    /// Le paiement passe par l'action de jeu <see cref="LouerObjet"/> : les lois peuvent
    /// l'interdire ou la taxer. Le temps compte en temps de monde : il s'arrete quand le
    /// serveur est eteint, le locataire ne perd donc rien pendant une coupure.
    /// </summary>
    [Serialized]
    // « Location » serait traduit par Eco en « Emplacement » : l'onglet s'appelle donc « Louer ».
    [CreateComponentTabLoc("Louer", true)]
    // Priorité basse : l'onglet passe après les autres (Stockage, Texte, Pièces...).
    [Priority(PriorityAttribute.VeryLow)]
    [HasIcon("HourglassItem")]
    [LocDescription("Permet de louer cet objet à d'autres citoyens, pour une durée et un prix par heure.")]
    public class LocationComponent : WorldObjectComponent, IHasEnvVars
    {
        /// <summary>Durees proposees au locataire, en heures, limitees par la duree maximale.</summary>
        private static readonly int[] DureesProposees = { 1, 2, 3, 4, 6, 8, 12, 24, 48, 72, 168 };

        private const int DureeMaxPossible = 168;

        private readonly object verrou = new object();
        private bool proposer;
        private float prixParHeure = 150;
        private Currency devise;
        private BankAccount compte;
        private int dureeMax = 24;

        // Avertissements de fin deja envoyes (non serialises : au pire, renvoyes apres un redemarrage).
        private bool averti5;
        private bool averti1;
        private int derniereMinute = -1;

        public override WorldObjectComponentClientAvailability Availability => WorldObjectComponentClientAvailability.Always;

        [SyncToView] public override string IconName => "HourglassItem";

        // --- Etat de la location (serialise avec l'objet) -------------------------

        /// <summary>Locataire en cours, ou null.</summary>
        [Serialized] public User Locataire { get; set; }

        /// <summary>Fin de la location, en secondes de monde.</summary>
        [Serialized] public double FinMonde { get; set; }

        /// <summary>Vrai si c'est la location qui a ajoute le locataire aux acces : on ne retire que ce qu'on a ajoute.</summary>
        [Serialized] public bool AccesAjoute { get; set; }

        // --- En-tete --------------------------------------------------------------

        [SyncToView, Autogen, Sort(0), UITypeName("StringTitle")]
        public string Statut(Player player) => this.TexteStatut(player?.User);

        // --- Locataire ------------------------------------------------------------

        [SyncToView, Autogen, Sort(10), UITypeName("GeneralHeader"), VisibilityParam(nameof(ShowLouer))]
        public string EnteteLouer(Player player) =>
            player?.User != null && player.User == this.Locataire ? Localizer.DoStr("Prolonger la location") : Localizer.DoStr("Choisir une durée");

        // Grille de boutons : un clic appelle SelectDurees(player, index).
        [SyncToView, Autogen, Sort(11), UIListTypeName("ButtonGrid"), VisibilityParam(nameof(ShowLouer))]
        public IEnumerable<string> Durees => this.DureesDisponibles().Select(h => $"{TexteDuree(h)}  ·  {Montant(h * this.prixParHeure)} {this.devise?.Name}").ToList();

        [SyncToView] public IEnumerable<string> DureesIcons => this.DureesDisponibles().Select(_ => "HourglassItem").ToList();

        [RPC(AccessType.None)]
        [UnauthenticatedRpcJustification("Louer est public par nature : le client n'a aucun droit sur l'objet. Tout est vérifié dans la méthode (location ouverte, objet libre, durée autorisée) puis par le transfert d'argent, depuis le propre compte du client, et par les lois.")]
        public void SelectDurees(Player player, int index)
        {
            var durees = this.DureesDisponibles().ToList();
            if (player?.User == null || index < 0 || index >= durees.Count) return;
            _ = this.LouerAsync(player, durees[index]);
        }

        [Autogen, RPC(AccessType.None), Sort(12), UITypeName("BigButton"), DynamicTitle(nameof(TitreRendre)), VisibilityParam(nameof(ShowRendre))]
        [Description("Termine votre location tout de suite. Le temps restant n'est pas remboursé.")]
        [UnauthenticatedRpcJustification("Seul le locataire en cours peut rendre l'objet : vérifié dans la méthode.")]
        public void Rendre(Player player)
        {
            if (player?.User == null || player.User != this.Locataire) return;
            this.Terminer(Localizer.Do($"Vous avez rendu {this.Parent.MarkedUpName}."), notifierProprietaires: true);
        }

        [SyncToView] public string TitreRendre => Localizer.DoStr("Rendre maintenant");

        // --- Location sans aucun droit sur l'objet --------------------------------
        //
        // Sans acces, un joueur ne peut pas ouvrir l'objet ni voir l'onglet. Comme
        // « Acheter » de l'onglet A vendre, la location passe donc par une interaction
        // ouverte a tous (Ctrl + clic droit), affichee seulement quand l'objet est libre.

        /// <summary>Location ouverte et objet libre : active l'interaction « Louer ».</summary>
        [EnvVar] public bool LouerPossible => this.Ouverte && this.Locataire == null;

        /// <summary>Prix affiche dans l'interaction.</summary>
        [EnvVar] public string PrixLocation => this.devise == null ? string.Empty : $"{Montant(this.prixParHeure)} {this.devise.Name} / h";

        [Interaction(InteractionTrigger.RightClick, "Louer (%PrixLocation%)", InteractionModifier.Ctrl, requiredEnvVars: new[] { nameof(LouerPossible) }, priority: 10, authRequired: AccessType.None)]
        public void LouerInteraction(Player player, InteractionTriggerInfo trigger, InteractionTarget target) => _ = this.ChoisirDureeAsync(player);

        private async Task ChoisirDureeAsync(Player player)
        {
            var user = player?.User;
            if (user == null) return;
            try
            {
                if (this.EstProprio(user)) { user.MsgLocStr("Vous avez déjà accès à cet objet."); return; }
                var durees = this.DureesDisponibles().ToList();
                var options = durees.Select(h => $"{TexteDuree(h)}  ·  {Montant(h * this.prixParHeure)} {this.devise?.Name}").ToList();
                var choix = await player.OptionBox(Localizer.Do($"Louer {this.Parent.MarkedUpName} : choisissez une durée."), options);
                if (choix < 0 || choix >= durees.Count) return;
                await this.LouerAsync(player, durees[choix], confirmer: false);
            }
            catch (Exception e)
            {
                Log.WriteErrorLine(Localizer.DoStr("[Location] choix de durée en échec : " + e));
            }
        }

        // --- Proprietaire ---------------------------------------------------------

        [SyncToView, Autogen, Sort(20), UITypeName("GeneralHeader"), VisibilityParam(nameof(ShowProprio))]
        public string EnteteProprio => Localizer.DoStr("Réglages du propriétaire");

        [Eco(AccessType.FullAccess), Serialized, Sort(21), LocDisplayName("Proposer à la location"), VisibilityParam(nameof(ShowProprio))]
        public bool Proposer
        {
            get => this.proposer;
            set { this.proposer = value; this.Rafraichir(); }
        }

        [Eco(AccessType.FullAccess), Serialized, Sort(22), LocDisplayName("Prix par heure"), VisibilityParam(nameof(ShowProprio))]
        public float PrixParHeure
        {
            get => this.prixParHeure;
            set { this.prixParHeure = Math.Max(0, value); this.Rafraichir(); }
        }

        [Eco(AccessType.FullAccess), Serialized, AllowNullInView, Sort(23), LocDisplayName("Devise"), VisibilityParam(nameof(ShowProprio))]
        public Currency Devise
        {
            get => this.devise;
            set { this.devise = value; this.Rafraichir(); }
        }

        [Eco(AccessType.FullAccess), Serialized, AllowNullInView, Sort(24), LocDisplayName("Compte qui encaisse"), VisibilityParam(nameof(ShowProprio))]
        public BankAccount Compte
        {
            get => this.compte;
            set { this.compte = value; this.Rafraichir(); }
        }

        [Eco(AccessType.FullAccess), Serialized, Sort(25), LocDisplayName("Durée maximale (heures)"), VisibilityParam(nameof(ShowProprio))]
        public int DureeMax
        {
            get => this.dureeMax;
            set { this.dureeMax = Math.Clamp(value, 1, DureeMaxPossible); this.Rafraichir(); }
        }

        [Autogen, RPC(AccessType.FullAccess), Sort(26), UITypeName("BigButton"), DynamicTitle(nameof(TitreMettreFin)), VisibilityParam(nameof(ShowMettreFin))]
        [Description("Reprend l'objet tout de suite. Le locataire n'est pas remboursé automatiquement.")]
        public void MettreFin(Player player)
        {
            if (this.Locataire == null) return;
            this.Terminer(Localizer.Do($"Le propriétaire a mis fin à votre location de {this.Parent.MarkedUpName}."), notifierProprietaires: false);
            player?.User?.MsgLocStr("Location terminée.");
        }

        [SyncToView] public string TitreMettreFin => Localizer.DoStr("Mettre fin à la location");

        // --- Visibilite -----------------------------------------------------------

        [SyncToView] public bool ShowProprio(Player player) => this.EstProprio(player?.User);

        [SyncToView]
        public bool ShowLouer(Player player)
        {
            var u = player?.User;
            if (u == null || this.EstProprio(u) || !this.Ouverte) return false;
            return this.Locataire == null || this.Locataire == u;
        }

        [SyncToView] public bool ShowRendre(Player player) => player?.User != null && player.User == this.Locataire;

        [SyncToView] public bool ShowMettreFin(Player player) => this.Locataire != null && this.EstProprio(player?.User);

        // --- Cycle de vie ---------------------------------------------------------

        public override void Initialize()
        {
            base.Initialize();
            LocationRegistre.Ajouter(this);
        }

        public override void Destroy()
        {
            // Pas de fin de location ici : Destroy n'est pas une echeance, et l'etat
            // serialise doit survivre. Le registre ne garde simplement plus l'objet.
            LocationRegistre.Retirer(this);
            base.Destroy();
        }

        // --- Logique --------------------------------------------------------------

        /// <summary>Location ouverte : proposee, et assez configuree pour encaisser.</summary>
        private bool Ouverte => this.proposer && this.devise != null && this.compte != null && this.prixParHeure >= 0;

        private bool EstProprio(User u) => u != null && this.Parent != null && this.Parent.IsAuthorized(u, AccessType.FullAccess, this);

        private IEnumerable<int> DureesDisponibles() => DureesProposees.Where(h => h <= this.dureeMax);

        private AuthComponent Auth => this.Parent?.Components?.OfType<AuthComponent>().FirstOrDefault();

        private async Task LouerAsync(Player player, int heures, bool confirmer = true)
        {
            var user = player.User;
            try
            {
                var erreur = this.VerifierDemande(user, heures, out var fin);
                if (erreur != null) { user.MsgLocStr(erreur); return; }

                var prix = heures * this.prixParHeure;
                var verbe = user == this.Locataire ? "Prolonger la location de" : "Louer";
                // Depuis la fenetre de choix, la duree et le prix viennent d'etre lus : pas de seconde question.
                if (confirmer && !await player.ConfirmBoxLoc($"{verbe} {this.Parent.MarkedUpName} pendant {TexteDuree(heures)} pour {Montant(prix)} {this.devise.Name} ?")) return;

                lock (this.verrou)
                {
                    // L'etat a pu changer pendant la confirmation.
                    erreur = this.VerifierDemande(user, heures, out fin);
                    if (erreur != null) { user.MsgLocStr(erreur); return; }
                    this.Payer(user, heures, prix, fin);
                }
            }
            catch (Exception e)
            {
                Log.WriteErrorLine(Localizer.DoStr("[Location] location en échec : " + e));
                user.MsgLocStr("La location a échoué (erreur du serveur).");
            }
        }

        /// <summary>Null si la demande est recevable ; sinon le motif du refus.</summary>
        private string VerifierDemande(User user, int heures, out double fin)
        {
            fin = 0;
            if (!this.Ouverte) return "Cet objet n'est pas proposé à la location.";
            if (this.EstProprio(user)) return "Vous avez déjà accès à cet objet.";
            if (this.Locataire != null && this.Locataire != user) return $"Cet objet est déjà loué par {this.Locataire.Name}.";
            if (!this.DureesDisponibles().Contains(heures)) return "Cette durée n'est pas proposée.";
            if (user.BankAccount == null) return "Vous n'avez pas de compte personnel.";

            var maintenant = WorldTime.Seconds;
            var depart = this.Locataire == user ? Math.Max(this.FinMonde, maintenant) : maintenant;
            fin = depart + heures * 3600.0;
            if (fin - maintenant > this.dureeMax * 3600.0 + 1)
            {
                return $"Durée maximale : {TexteDuree(this.dureeMax)} à la fois. Il vous reste {TexteReste(this.FinMonde - maintenant)}.";
            }
            return null;
        }

        /// <summary>
        /// Encaisse et accorde l'acces dans un meme paquet d'actions : si une loi
        /// refuse, ou si le locataire ne peut pas payer, rien ne se passe.
        /// </summary>
        private void Payer(User user, int heures, float prix, double fin)
        {
            var action = new LouerObjet
            {
                Citizen = user,
                ActionLocation = this.Parent.Position3i,
                Proprietaire = this.Parent.Owners,
                ObjetLoue = this.Parent.CreatingItem as Item,
                Heures = heures,
                PrixParHeure = this.prixParHeure,
                Currency = this.devise,
                CurrencyAmount = prix,
                SourceBankAccount = user.BankAccount,
                TargetBankAccount = this.compte,
            };

            using var pack = new GameActionPack();
            pack.AddGameAction(action);
            if (prix > 0)
            {
                var (transfert, _) = Transfers.Transfer(pack, new TransferData
                {
                    Currency = this.devise,
                    Amount = prix,
                    SourceAccount = user.BankAccount,
                    TargetAccount = this.compte,
                    Sender = user,
                    TransferType = TransferType.Rent,
                    TransferDescription = Localizer.Do($"Location de {this.Parent.MarkedUpName} ({TexteDuree(heures)})"),
                    TaxPayer = user,
                    TaxSettlementScopes = action.SettlementScopes,
                });
                // Solde insuffisant, compte inaccessible... : refuse avant toute action.
                var echec = transfert != null && transfert.Failed ? transfert : pack.EarlyResult;
                if (echec != null && echec.Failed)
                {
                    echec.Notify(user);
                    return;
                }
            }

            pack.AddPostEffect(() => this.Accorder(user, heures, fin));
            pack.TryPerform(user);
        }

        private void Accorder(User user, int heures, double fin)
        {
            var nouvelle = this.Locataire != user;
            if (nouvelle)
            {
                this.Locataire = user;
                var acces = this.Auth?.UsersWithConsumerAccess;
                this.AccesAjoute = acces != null && !acces.ContainsExactAlias(user);
                if (this.AccesAjoute) acces.Add(user);
            }
            this.FinMonde = fin;
            this.averti5 = this.averti1 = false;
            this.Rafraichir();

            var reste = TexteReste(fin - WorldTime.Seconds);
            user.MsgLocStr(nouvelle
                ? $"Location confirmée : {this.Parent.MarkedUpName} est à vous pour {reste}."
                : $"Location prolongée : il vous reste {reste}.");
            this.NotifierProprietaires($"{user.Name} {(nouvelle ? "loue" : "prolonge la location de")} {this.Parent.MarkedUpName} ({TexteDuree(heures)}).");
        }

        /// <summary>Appele toutes les 5 secondes par LocationRegistre : avertissements et echeance.</summary>
        internal void Verifier(double maintenant)
        {
            var locataire = this.Locataire;
            if (locataire == null) return;

            var reste = this.FinMonde - maintenant;
            if (reste <= 0)
            {
                lock (this.verrou)
                {
                    if (this.Locataire == null || this.FinMonde > WorldTime.Seconds) return;
                    this.Terminer(Localizer.Do($"Votre location de {this.Parent.MarkedUpName} est terminée."), notifierProprietaires: true);
                }
                return;
            }

            if (reste <= 60 && !this.averti1)
            {
                this.averti1 = this.averti5 = true;
                locataire.MsgLocStr($"Votre location de {this.Parent.MarkedUpName} se termine dans moins d'une minute.");
            }
            else if (reste <= 300 && !this.averti5)
            {
                this.averti5 = true;
                locataire.MsgLocStr($"Votre location de {this.Parent.MarkedUpName} se termine dans 5 minutes.");
            }

            // Le temps restant affiche dans l'onglet suit la minute.
            var minute = (int)(reste / 60);
            if (minute != this.derniereMinute)
            {
                this.derniereMinute = minute;
                this.Changed(nameof(this.Statut));
            }
        }

        private void Terminer(LocString messageLocataire, bool notifierProprietaires)
        {
            var locataire = this.Locataire;
            if (locataire == null) return;

            if (this.AccesAjoute) this.Auth?.UsersWithConsumerAccess?.Delete(locataire);

            // Le locataire descend s'il est encore a bord.
            if (this.Parent.Components.OfType<MountComponent>().FirstOrDefault() is { } mount)
            {
                var abord = mount.MountedPlayers?.FirstOrDefault(p => p?.User == locataire);
                if (abord != null) mount.TryDismountPlayer(abord);
            }

            this.Locataire = null;
            this.FinMonde = 0;
            this.AccesAjoute = false;
            this.Rafraichir();

            locataire.MsgLoc($"{messageLocataire}");
            if (notifierProprietaires) this.NotifierProprietaires($"La location de {this.Parent.MarkedUpName} par {locataire.Name} est terminée.");
        }

        private void NotifierProprietaires(string message)
        {
            foreach (var u in UserManager.Users.Where(u => u.IsOnline))
            {
                if (u != this.Locataire && this.EstProprio(u)) u.MsgLocStr(message);
            }
        }

        private void Rafraichir()
        {
            if (this.Parent == null) return;
            this.Changed(nameof(this.Statut));
            this.Changed(nameof(this.Durees));
            this.Changed(nameof(this.DureesIcons));
            this.Changed(nameof(this.EnteteLouer));
            this.Changed(nameof(this.ShowLouer));
            this.Changed(nameof(this.ShowRendre));
            this.Changed(nameof(this.ShowMettreFin));
            this.Changed(nameof(this.LouerPossible));
            this.Changed(nameof(this.PrixLocation));
        }

        private string TexteStatut(User viewer)
        {
            var reste = this.FinMonde - WorldTime.Seconds;
            if (this.Locataire != null)
            {
                return viewer != null && viewer == this.Locataire
                    ? $"Vous louez cet objet. Temps restant : {TexteReste(reste)}."
                    : $"Loué par {this.Locataire.Name}. Libre dans {TexteReste(reste)}.";
            }
            if (!this.proposer) return "Cet objet n'est pas proposé à la location.";
            if (this.devise == null || this.compte == null)
            {
                return this.EstProprio(viewer)
                    ? "Choisissez une devise et un compte pour ouvrir la location."
                    : "La location n'est pas encore ouverte.";
            }
            return $"Libre. Prix : {Montant(this.prixParHeure)} {this.devise.Name} par heure, jusqu'à {TexteDuree(this.dureeMax)}.";
        }

        internal static string TexteDuree(int heures) =>
            heures >= 24 && heures % 24 == 0 ? $"{heures / 24} j" : $"{heures} h";

        internal static string TexteReste(double secondes)
        {
            if (secondes <= 0) return "0 min";
            var t = TimeSpan.FromSeconds(Math.Ceiling(secondes));
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours} h {t.Minutes:00} min";
            if (t.TotalMinutes >= 1) return $"{(int)Math.Ceiling(t.TotalMinutes)} min";
            return $"{t.Seconds} s";
        }

        private static string Montant(float v) => v.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));
    }
}
