using System;
using System.Linq;
using Harmony;
using RimWorld;
using Verse;

namespace ColdDesertNights.Patches
{
    [HarmonyPatch(typeof(GameConditionManager), "AggregateTemperatureOffset")]
    // ReSharper disable once UnusedMember.Global
    public static class AggregateTemperatureOffsetPatch
    {
        public static bool Prefix(GameConditionManager __instance, ref float __result)
        {
            try
            {
                __result = GetOffset(__instance, Main.BiomeSettings[__instance.map.Biome]);
                return false;
            }
            catch (Exception)
            {
                Log.Error("Unable to override game condition temperature offsets; falling back to vanilla.");
                return true;
            }
        }

        /// <summary>
        /// Gets the offset for the given condition manager, recursively checking any parent managers we have
        /// </summary>
        /// <param name="manager">The condition manager to check</param>
        /// <param name="data">The <see cref="BiomeData"/> to use</param>
        /// <returns>The aggregated offset</returns>
        private static float GetOffset(GameConditionManager manager, BiomeData data)
        {
            // Add up all the various offsets we have:
            var num = manager.ActiveConditions.Sum(condition => data.GetBiomeConditionTemperatureOffset(condition));

            if (manager.Parent != null)
            {
                num += GetOffset(manager.Parent, data);
            }
            return num;
        }
    }
}
