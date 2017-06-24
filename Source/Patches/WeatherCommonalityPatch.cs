using System;
using Harmony;
using RimWorld;
using Verse;

namespace ColdDesertNights.Patches
{
    [HarmonyPatch(typeof(WeatherDecider), "CurrentWeatherCommonality")]
    // ReSharper disable once UnusedMember.Global
    public static class WeatherCommonalityPatch
    {
        public static void Postfix(WeatherDecider __instance, WeatherDef weather, ref float __result)
        {
            try
            {
                __result =
                    Main.BiomeSettings[Traverse.Create(__instance).Field("map").GetValue<Map>().Biome]
                        .AdjustWeatherCommonality(weather, __result);
            }
            catch (Exception)
            {
                Log.Warning("[ColdDesertNights] Unable to adjust weather commonality.");
            }
        }
    }
}
