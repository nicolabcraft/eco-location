// Mod de location : ajoute l'onglet « Louer » aux véhicules du jeu.
// Le reste du mod est dans les autres fichiers de ce dossier. Pour retirer un vehicule
// de la location, supprimez simplement sa ligne.
namespace Eco.Mods.TechTree
{
    using EcoLocation;
    using Eco.Gameplay.Objects;

    [RequireComponent(typeof(LocationComponent))] public partial class CraneObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class EgyptianCanoeObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class ExcavatorObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class HandPlowObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class IndustrialBargeObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class IndustrialElevatorObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class LargeCanoeObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class MediumFishingTrawlerObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class NorseCanoeObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class PoweredCartObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class ScorpionObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class SkidSteerObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class SmallCanoeObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class SmallWoodCartObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class SmallWoodenBoatObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class SteamTractorObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class SteamTruckObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class TrailerTruckObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class TruckObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class WheelbarrowObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class WoodCartObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class WoodShopCartObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class WoodenBargeObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class WoodenElevatorObject { }
    [RequireComponent(typeof(LocationComponent))] public partial class WoodenTransportShipObject { }
}
