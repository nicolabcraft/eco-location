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
    [LocDisplayName("Rent Object"), LocDescription("A citizen rents an object (vehicle, boat...) from its owner for a duration.")]
    // Noms de proprietes inchanges depuis la 1.0.0 : les lois existantes y font reference.
    [ChangeParentConfigLoc("Citizen", "Renter")]
    public class LouerObjet : InteractAction, ICurrencyGameAction
    {
        // Le locataire n'a, par definition, aucun droit sur l'objet : l'action ne
        // doit pas etre refusee par le controle d'acces ordinaire.
        public LouerObjet() => this.AccessNeeded = AccessType.None;

        [Eco, LocDisplayName("Owner"), LocDescription("The owner of the rented object.")]
        public IAlias Proprietaire { get; set; }

        [Eco, LocDisplayName("Rented object"), LocDescription("The type of object rented.")]
        public Item ObjetLoue { get; set; }

        [Eco, LocDisplayName("Hours"), LocDescription("Rented duration, in hours.")]
        public float Heures { get; set; }

        [Eco, LocDisplayName("Price per hour"), LocDescription("Price asked for one hour.")]
        public float PrixParHeure { get; set; }

        [Eco, LocDescription("Currency used.")]
        public Currency Currency { get; set; }

        [Eco, LocDescription("Amount paid for the rental.")]
        public float CurrencyAmount { get; set; }

        [Eco, LocDescription("The renter's bank account.")]
        public BankAccount SourceBankAccount { get; set; }

        [Eco, LocDescription("The bank account receiving the rent.")]
        public BankAccount TargetBankAccount { get; set; }
    }
}
