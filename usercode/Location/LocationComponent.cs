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
    // Textes sources en anglais, traduits par Eco (Mods/Translations/Location.csv). Nom
    // d'onglet propre au mod : « Location » ou « Rent » existent deja dans les
    // traductions du jeu (« Emplacement », « Loyer »).
    [CreateComponentTabLoc("Vehicle Rental", true)]
    // Priorité basse : l'onglet passe après les autres (Stockage, Texte, Pièces...).
    [Priority(PriorityAttribute.VeryLow)]
    [HasIcon("HourglassItem")]
    [LocDescription("Lets other citizens rent this object for a chosen duration, at a price per hour.")]
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
            player?.User != null && player.User == this.Locataire ? Localizer.DoStr("Extend the rental") : Localizer.DoStr("Choose a duration");

        // Grille de boutons : un clic appelle SelectDurees(player, index).
        [SyncToView, Autogen, Sort(11), UIListTypeName("ButtonGrid"), VisibilityParam(nameof(ShowLouer))]
        public IEnumerable<string> Durees => this.DureesDisponibles().Select(h => $"{TexteDuree(h)}  ·  {Montant(h * this.prixParHeure)} {this.devise?.Name}").ToList();

        [SyncToView] public IEnumerable<string> DureesIcons => this.DureesDisponibles().Select(_ => "HourglassItem").ToList();

        [RPC(AccessType.None)]
        [UnauthenticatedRpcJustification("Renting is public by nature: the customer holds no access on the object. Everything is checked in the method (rental open, object free, allowed duration), then by the money transfer from the customer's own account, and by the laws.")]
        public void SelectDurees(Player player, int index)
        {
            var durees = this.DureesDisponibles().ToList();
            if (player?.User == null || index < 0 || index >= durees.Count) return;
            _ = this.LouerAsync(player, durees[index]);
        }

        [Autogen, RPC(AccessType.None), Sort(12), UITypeName("BigButton"), DynamicTitle(nameof(TitreRendre)), VisibilityParam(nameof(ShowRendre))]
        [Description("Ends your rental right away. The remaining time is not refunded.")]
        [UnauthenticatedRpcJustification("Only the current renter can return the object: checked in the method.")]
        public void Rendre(Player player)
        {
            if (player?.User == null || player.User != this.Locataire) return;
            this.Terminer(u => u.MsgLoc($"You returned {this.Parent.MarkedUpName}."), notifierProprietaires: true);
        }

        [SyncToView] public string TitreRendre => Localizer.DoStr("Return now");

        // --- Location sans aucun droit sur l'objet --------------------------------
        //
        // Sans acces, un joueur ne peut pas ouvrir l'objet ni voir l'onglet. Comme
        // « Acheter » de l'onglet A vendre, la location passe donc par une interaction
        // ouverte a tous (Ctrl + clic droit), affichee seulement quand l'objet est libre.

        /// <summary>Location ouverte et objet libre : active l'interaction « Louer ».</summary>
        [EnvVar] public bool LouerPossible => this.Ouverte && this.Locataire == null;

        /// <summary>Prix affiche dans l'interaction.</summary>
        [EnvVar] public string PrixLocation => this.devise == null ? string.Empty : $"{Montant(this.prixParHeure)} {this.devise.Name} / h";

        [Interaction(InteractionTrigger.RightClick, "Rent (%PrixLocation%)", InteractionModifier.Ctrl, requiredEnvVars: new[] { nameof(LouerPossible) }, priority: 10, authRequired: AccessType.None)]
        public void LouerInteraction(Player player, InteractionTriggerInfo trigger, InteractionTarget target) => _ = this.ChoisirDureeAsync(player);

        private async Task ChoisirDureeAsync(Player player)
        {
            var user = player?.User;
            if (user == null) return;
            try
            {
                if (this.EstProprio(user)) { user.MsgLocStr("You already have access to this object."); return; }
                var durees = this.DureesDisponibles().ToList();
                var options = durees.Select(h => $"{TexteDuree(h)}  ·  {Montant(h * this.prixParHeure)} {this.devise?.Name}").ToList();
                var choix = await player.OptionBox(Localizer.Do($"Rent {this.Parent.MarkedUpName}: choose a duration."), options);
                if (choix < 0 || choix >= durees.Count) return;
                await this.LouerAsync(player, durees[choix], confirmer: false);
            }
            catch (Exception e)
            {
                Log.WriteErrorLine(Localizer.NotLocalizedStr("[Location] duration choice failed: " + e));
            }
        }

        // --- Proprietaire ---------------------------------------------------------

        [SyncToView, Autogen, Sort(20), UITypeName("GeneralHeader"), VisibilityParam(nameof(ShowProprio))]
        public string EnteteProprio => Localizer.DoStr("Owner settings");

        [Eco(AccessType.FullAccess), Serialized, Sort(21), LocDisplayName("Offer for rent"), VisibilityParam(nameof(ShowProprio))]
        public bool Proposer
        {
            get => this.proposer;
            set { this.proposer = value; this.Rafraichir(); }
        }

        [Eco(AccessType.FullAccess), Serialized, Sort(22), LocDisplayName("Price per hour"), VisibilityParam(nameof(ShowProprio))]
        public float PrixParHeure
        {
            get => this.prixParHeure;
            set { this.prixParHeure = Math.Max(0, value); this.Rafraichir(); }
        }

        [Eco(AccessType.FullAccess), Serialized, AllowNullInView, Sort(23), LocDisplayName("Rental currency"), VisibilityParam(nameof(ShowProprio))]
        public Currency Devise
        {
            get => this.devise;
            set { this.devise = value; this.Rafraichir(); }
        }

        [Eco(AccessType.FullAccess), Serialized, AllowNullInView, Sort(24), LocDisplayName("Receiving account"), VisibilityParam(nameof(ShowProprio))]
        public BankAccount Compte
        {
            get => this.compte;
            set { this.compte = value; this.Rafraichir(); }
        }

        [Eco(AccessType.FullAccess), Serialized, Sort(25), LocDisplayName("Maximum duration (hours)"), VisibilityParam(nameof(ShowProprio))]
        public int DureeMax
        {
            get => this.dureeMax;
            set { this.dureeMax = Math.Clamp(value, 1, DureeMaxPossible); this.Rafraichir(); }
        }

        [Autogen, RPC(AccessType.FullAccess), Sort(26), UITypeName("BigButton"), DynamicTitle(nameof(TitreMettreFin)), VisibilityParam(nameof(ShowMettreFin))]
        [Description("Takes the object back right away. The renter is not refunded automatically.")]
        public void MettreFin(Player player)
        {
            if (this.Locataire == null) return;
            this.Terminer(u => u.MsgLoc($"The owner ended your rental of {this.Parent.MarkedUpName}."), notifierProprietaires: false);
            player?.User?.MsgLocStr("Rental ended.");
        }

        [SyncToView] public string TitreMettreFin => Localizer.DoStr("End the rental");

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
                if (erreur != null) { user.Msg(erreur.Value); return; }

                var prix = heures * this.prixParHeure;
                // Depuis la fenetre de choix, la duree et le prix viennent d'etre lus : pas de seconde question.
                if (confirmer)
                {
                    var question = user == this.Locataire
                        ? Localizer.Do($"Extend the rental of {this.Parent.MarkedUpName} by {TexteDuree(heures)} for {Montant(prix)} {this.devise.Name}?")
                        : Localizer.Do($"Rent {this.Parent.MarkedUpName} for {TexteDuree(heures)} for {Montant(prix)} {this.devise.Name}?");
                    if (!await player.ConfirmBox(question)) return;
                }

                lock (this.verrou)
                {
                    // L'etat a pu changer pendant la confirmation.
                    erreur = this.VerifierDemande(user, heures, out fin);
                    if (erreur != null) { user.Msg(erreur.Value); return; }
                    this.Payer(user, heures, prix, fin);
                }
            }
            catch (Exception e)
            {
                Log.WriteErrorLine(Localizer.NotLocalizedStr("[Location] rental failed: " + e));
                user.MsgLocStr("The rental failed (server error).");
            }
        }

        /// <summary>Null si la demande est recevable ; sinon le motif du refus, deja traduit.</summary>
        private LocString? VerifierDemande(User user, int heures, out double fin)
        {
            fin = 0;
            if (!this.Ouverte) return Localizer.DoStr("This object is not offered for rent.");
            if (this.EstProprio(user)) return Localizer.DoStr("You already have access to this object.");
            if (this.Locataire != null && this.Locataire != user) return Localizer.Do($"This object is already rented by {this.Locataire.Name}.");
            if (!this.DureesDisponibles().Contains(heures)) return Localizer.DoStr("This duration is not offered.");
            if (user.BankAccount == null) return Localizer.DoStr("You have no personal bank account.");

            var maintenant = WorldTime.Seconds;
            var depart = this.Locataire == user ? Math.Max(this.FinMonde, maintenant) : maintenant;
            fin = depart + heures * 3600.0;
            if (fin - maintenant > this.dureeMax * 3600.0 + 1)
            {
                return Localizer.Do($"Maximum duration: {TexteDuree(this.dureeMax)} at a time. You still have {TexteReste(this.FinMonde - maintenant)}.");
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
                    TransferDescription = Localizer.Do($"Rental of {this.Parent.MarkedUpName} ({TexteDuree(heures)})"),
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
            if (nouvelle) user.MsgLoc($"Rental confirmed: {this.Parent.MarkedUpName} is yours for {reste}.");
            else user.MsgLoc($"Rental extended: you have {reste} left.");
            var objet = this.Parent.MarkedUpName;
            var duree = TexteDuree(heures);
            if (nouvelle) this.NotifierProprietaires(u => u.MsgLoc($"{user.Name} rents {objet} ({duree})."));
            else this.NotifierProprietaires(u => u.MsgLoc($"{user.Name} extends the rental of {objet} ({duree})."));
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
                    this.Terminer(u => u.MsgLoc($"Your rental of {this.Parent.MarkedUpName} has ended."), notifierProprietaires: true);
                }
                return;
            }

            if (reste <= 60 && !this.averti1)
            {
                this.averti1 = this.averti5 = true;
                locataire.MsgLoc($"Your rental of {this.Parent.MarkedUpName} ends in less than a minute.");
            }
            else if (reste <= 300 && !this.averti5)
            {
                this.averti5 = true;
                locataire.MsgLoc($"Your rental of {this.Parent.MarkedUpName} ends in 5 minutes.");
            }

            // Le temps restant affiche dans l'onglet suit la minute.
            var minute = (int)(reste / 60);
            if (minute != this.derniereMinute)
            {
                this.derniereMinute = minute;
                this.Changed(nameof(this.Statut));
            }
        }

        /// <summary>Fin de location ; le message au locataire est envoye dans sa langue.</summary>
        private void Terminer(Action<User> prevenirLocataire, bool notifierProprietaires)
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

            prevenirLocataire(locataire);
            var objet = this.Parent.MarkedUpName;
            if (notifierProprietaires) this.NotifierProprietaires(u => u.MsgLoc($"The rental of {objet} by {locataire.Name} has ended."));
        }

        /// <summary>Previent les proprietaires connectes, chacun dans sa langue.</summary>
        private void NotifierProprietaires(Action<User> prevenir)
        {
            foreach (var u in UserManager.Users.Where(u => u.IsOnline))
            {
                if (u != this.Locataire && this.EstProprio(u)) prevenir(u);
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
                    ? Localizer.Do($"You are renting this object. Time left: {TexteReste(reste)}.")
                    : Localizer.Do($"Rented by {this.Locataire.Name}. Free in {TexteReste(reste)}.");
            }
            if (!this.proposer) return Localizer.DoStr("This object is not offered for rent.");
            if (this.devise == null || this.compte == null)
            {
                return this.EstProprio(viewer)
                    ? Localizer.DoStr("Choose a currency and an account to open the rental.")
                    : Localizer.DoStr("Renting is not open yet.");
            }
            return Localizer.Do($"Available. Price: {Montant(this.prixParHeure)} {this.devise.Name} per hour, up to {TexteDuree(this.dureeMax)}.");
        }

        // Unites traduites aussi (« d » devient « j » en francais).
        internal static string TexteDuree(int heures) =>
            heures >= 24 && heures % 24 == 0 ? Localizer.Do($"{heures / 24} d") : Localizer.Do($"{heures} h");

        internal static string TexteReste(double secondes)
        {
            if (secondes <= 0) return Localizer.Do($"{0} min");
            var t = TimeSpan.FromSeconds(Math.Ceiling(secondes));
            if (t.TotalHours >= 1) return Localizer.Do($"{(int)t.TotalHours} h {t.Minutes:00} min");
            if (t.TotalMinutes >= 1) return Localizer.Do($"{(int)Math.Ceiling(t.TotalMinutes)} min");
            return Localizer.Do($"{t.Seconds} s");
        }

        private static string Montant(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
