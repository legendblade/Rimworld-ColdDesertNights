using System;
using Harmony;
using RimWorld;
using Verse;

namespace ColdDesertNights.Patches
{
    [HarmonyPatch(typeof(WeatherDecider), "ChooseNextWeather")]
    // ReSharper disable once UnusedMember.Global
    public static class ChooseNextWeatherPatch
    {
        public static bool Prefix(WeatherDecider __instance, ref WeatherDef __result)
        {
            try
            {
                // Get all of our ducks in order...
                var traverse = Traverse.Create(__instance);
                var map = traverse.Field("map").GetValue<Map>();
                var biomeSetting = Main.BiomeSettings[map.Biome];
                var weatherTemp = biomeSetting.BoundWeatherTemp(map.mapTemperature.OutdoorTemp);
                var rainAllowed = biomeSetting.CanIgnoreRainLimits() ? 0 : traverse.Field("ticksWhenRainAllowedAgain").GetValue<int>();
                var preventRain = map.gameConditionManager.ActiveConditions.Any(x => x.def.preventRain);

                // If we're in the tutorial, just let it be
                if (TutorSystem.TutorialMode)
                {
                    __result = WeatherDefOf.Clear;
                    return false;
                }

                // Otherwise, try and figure out the weather by weight
                WeatherDef result;
                if (
                    DefDatabase<WeatherDef>.AllDefs.TryRandomElementByWeight(
                        w => CurrentWeatherCommonality(w, map, rainAllowed, biomeSetting, weatherTemp, preventRain),
                        out result))
                {
                    __result = result;
                    return false;
                }

                // If we didn't, use the default weather
                Log.Warning("Unable to choose suitable weather; this may mean your biome specific settings don't produce a viable range of weathers.");
                __result = WeatherDefOf.Clear;
                return false;
            }
            catch (Exception)
            {
                Log.Error("Unable to override choosing the next weather; falling back to vanilla.");
                return true;
            }
        }

        /// <summary>
        /// Get the weather commonality based on the various factors
        /// </summary>
        /// <param name="weather">The weather to check</param>
        /// <param name="map">The map to check it on</param>
        /// <param name="rainAllowed">The next tick rain is allowed at</param>
        /// <param name="biomeSetting">The actual settings data</param>
        /// <param name="weatherTemp">The bounded weather temperature to use</param>
        /// <param name="preventRain">If we should prevent rain due to active conditions</param>
        /// <returns></returns>
        private static float CurrentWeatherCommonality(WeatherDef weather, Map map, int rainAllowed, BiomeData biomeSetting, float weatherTemp, bool preventRain)
        {
            // Check the variety of things we need to check to see if we should use this weather or not
            if (!map.weatherManager.curWeather.repeatable && weather == map.weatherManager.curWeather ||
                !weather.temperatureRange.Includes(weatherTemp) ||
                weather.favorability < Favorability.Neutral && GenDate.DaysPassed < 8 ||
                weather.rainRate > 0.100000001490116 && Find.TickManager.TicksGame < rainAllowed ||
                weather.rainRate > 0.100000001490116 && preventRain)
            {
                return 0.0f;
            }

            // Otherwise, find our base commonality:
            var commonality = biomeSetting.GetWeatherCommonality(weather);

            // If we need to put out fires, increase rainfall considerably:
            if (map.fireWatcher.LargeFireDangerPresent && weather.rainRate > 0.100000001490116) commonality *= 20f;

            // Otherwise calculate our whole curve for rainfall and stuff...
            if (weather.commonalityRainfallFactor != null)
            {
                commonality *= weather.commonalityRainfallFactor.Evaluate(map.TileInfo.rainfall);
            }

            return commonality;
        }
    }
}
