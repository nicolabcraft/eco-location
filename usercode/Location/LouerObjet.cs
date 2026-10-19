namespace EcoLocation
{
    using Eco.Gameplay.Aliases;
    using Eco.Gameplay.Economy;
    using Eco.Gameplay.GameActions;
    using Eco.Gameplay.Items;
    using Eco.Shared.Items;
    using Eco.Shared.Localization;
    using Eco.Shared.Networking;

    /// <summary>
    /// Action de jeu emise a chaque location (ou prolongation). C'est elle que les
    /// lois voient : une loi peut l'interdire, la limiter ou la taxer comme un
    /// loyer, avec les memes conditions (qui loue, a qui, quoi, combien, ou).
    /// </summary>
    [Eco(true, Serialized = false), LocCategory("Finance")]
    [LocDescription("Un citoyen loue un objet (véhicule, bateau...) à son propriétaire pour une durée.")]
    [ChangeParentConfigLoc("Citizen", "Locataire")]
    public class LouerObjet : InteractAction, ICurrencyGameAction
    {
        // Le locataire n'a, par definition, aucun droit sur l'objet : l'action ne
        // doit pas etre refusee par le controle d'acces ordinaire.
        public LouerObjet() => this.AccessNeeded = AccessType.None;

        [Eco, LocDescription("Le propriétaire de l'objet loué.")]
        public IAlias Proprietaire { get; set; }

        [Eco, LocDescription("Le type d'objet loué.")]
        public Item ObjetLoue { get; set; }

        [Eco, LocDescription("Durée louée, en heures.")]
        public float Heures { get; set; }

        [Eco, LocDescription("Prix demandé pour une heure.")]
        public float PrixParHeure { get; set; }

        [Eco, LocDescription("Devise utilisée.")]
        public Currency Currency { get; set; }

        [Eco, LocDescription("Montant payé pour la location.")]
        public float CurrencyAmount { get; set; }

        [Eco, LocDescription("Le compte du locataire.")]
        public BankAccount SourceBankAccount { get; set; }

        [Eco, LocDescription("Le compte qui reçoit le loyer.")]
        public BankAccount TargetBankAccount { get; set; }
    }
}
