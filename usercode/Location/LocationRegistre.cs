namespace EcoLocation
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using Eco.Shared.Localization;
    using Eco.Shared.Logging;
    using Eco.Simulation.Time;

    /// <summary>
    /// Objets louables charges dans le monde, et surveillance des echeances toutes
    /// les 5 secondes. La minuterie demarre avec le premier objet louable : pas de
    /// plugin a declarer, le mod tient entierement dans Mods/UserCode/Location.
    /// </summary>
    internal static class LocationRegistre
    {
        private static readonly ConcurrentDictionary<LocationComponent, byte> Composants = new();
        private static Timer minuterie;

        public static void Ajouter(LocationComponent c)
        {
            Composants[c] = 0;
            if (minuterie == null)
            {
                var t = new Timer(_ => Verifier(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));
                if (Interlocked.CompareExchange(ref minuterie, t, null) != null) t.Dispose();
                else Log.WriteLine(Localizer.NotLocalizedStr("[Location] rental watcher started"));
            }
        }

        public static void Retirer(LocationComponent c) => Composants.TryRemove(c, out _);

        private static int enCours;

        private static void Verifier()
        {
            // Une verification lente ne doit pas se chevaucher avec la suivante.
            if (Interlocked.Exchange(ref enCours, 1) == 1) return;
            try
            {
                var maintenant = WorldTime.Seconds;
                foreach (var c in Composants.Keys)
                {
                    try { c.Verifier(maintenant); }
                    catch (Exception e) { Log.WriteErrorLine(Localizer.NotLocalizedStr("[Location] check failed: " + e.Message)); }
                }
            }
            finally
            {
                Interlocked.Exchange(ref enCours, 0);
            }
        }
    }
}
