using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using HugsLib;
using RimWorld;
using Verse;

namespace ColdDesertNights
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Main : ModBase
    {
        /// <summary>
        /// Gets the mod's identifier
        /// </summary>
        public override string ModIdentifier => "ColdDesertNights";

        /// <summary>
        /// Gets a map of <see cref="BiomeDef"/>'s to their nightly temperature difference.
        /// </summary>
        public static IDictionary<BiomeDef, BiomeData> BiomeSettings { get; private set; }

        /// <summary>
        /// Called when defs are loaded
        /// </summary>
        public override void DefsLoaded()
        {
            GetBiomes();
        }

        [HarmonyPatch(typeof(GenTemperature), nameof(GenTemperature.OffsetFromSunCycle))]
        // ReSharper disable once UnusedMember.Global
        public static class OffsetFromSunCyclePatch
        {
            // ReSharper disable once UnusedMember.Global
            public static bool Prefix(ref float __result, int absTick, int tile)
            {
                try
                {
                    var num = GenDate.DayPercent(absTick, Find.WorldGrid.LongLatOf(tile).x);
                    var f = 6.28318548f * (num + 0.32f);
                    __result = BiomeSettings[Find.WorldGrid.tiles[tile].biome].CalculateTemp(f);
                    return false;
                }
                catch (Exception e)
                {
                    Log.Error($"Error getting biome for tile {tile} on world grid due to {e} - {e.StackTrace}");
                    return true;
                }
            }
        }

        /// <summary>
        /// Initializes the list of available <see cref="BiomeDef"/>s.
        /// </summary>
        private void GetBiomes()
        {
            BiomeSettings =
                DefDatabase<BiomeDef>.AllDefs.Where(b => b.implemented && b.canBuildBase)
                    .ToDictionary(t => t, v => new BiomeData(Settings, v));
        }
    }
}
