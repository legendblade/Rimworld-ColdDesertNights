using System;
using Harmony;
using Verse;

namespace ColdDesertNights.Patches
{
    [HarmonyPatch(typeof(GenTemperature), nameof(GenTemperature.SeasonalShiftAmplitudeAt))]
    // ReSharper disable once UnusedMember.Global
    public static class SeasonalShiftAmplitudeAtPatch
    {
        public static bool Prefix(int tile, ref float __result)
        {
            try
            {
                var dist = Find.WorldGrid.DistanceFromEquatorNormalized(tile);
                var settings = Main.BiomeSettings[Find.WorldGrid.tiles[tile].biome];
                __result = Find.WorldGrid.LongLatOf(tile).y >= 0.0
                    ? settings.CalculateSeasonalTemp(dist)
                    : -settings.CalculateSeasonalTemp(dist);
                return false;
            }
            catch (Exception)
            {
                Log.Error("Unable to adjust seasonal temperature; falling back to vanilla.");
                return true;
            }
        }
    }
}
