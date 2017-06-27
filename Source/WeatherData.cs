using System.Linq;
using ColdDesertNights.Utility;
using HugsLib.Settings;
using RimWorld;
using Verse;

namespace ColdDesertNights
{
    public class WeatherData
    {
        private readonly WeatherDef weather;
        private readonly SettingHandle<float> commonality;
        private readonly SettingHandle<bool> allowRepeating;
        private readonly SettingHandle<bool> allowEarly;

        /// <summary>
        /// Initializes the weather data from the given <see cref="ModSettingsPack"/> settings
        /// and <see cref="WeatherDef"/>, creating our settings in the process.
        /// </summary>
        /// <param name="settings">The setting pack to use</param>
        /// <param name="biome">The biome to use</param>
        /// <param name="weather">The weather to base this off of</param>
        /// <param name="visibilityFunc">Function which returns if we should display this now</param>
        public WeatherData(ModSettingsPack settings, BiomeDef biome, WeatherDef weather, SettingHandle.ShouldDisplay visibilityFunc)
        {
            this.weather = weather;
            var curCommonality =
                biome.baseWeatherCommonalities.FirstOrDefault(wc => wc.weather == weather)?.commonality ?? 0f;

            // Init our settings...
            SpacerDrawer.GenerateSpacer(GenText.ToTitleCaseSmart(weather.label), settings, visibilityFunc);

            commonality = settings.GetHandle($"weather_{biome.defName}_{weather.defName}",
                "    " + "ColdDesertNights_BiomeWeather".Translate(),
                "ColdDesertNights_BiomeWeather_Desc".Translate(curCommonality), curCommonality,
                Validators.FloatRangeValidator(0f, float.MaxValue));

            allowRepeating = settings.GetHandle($"weather_{biome.defName}_{weather.defName}_repeating",
                "    " + "ColdDesertNights_BiomeWeatherRepeating".Translate(),
                "ColdDesertNights_BiomeWeatherRepeating_Desc".Translate(
                    (weather.repeatable ? "ColdDesertNights_Checked" : "ColdDesertNights_Unchecked").Translate()),
                weather.repeatable);

            allowEarly = settings.GetHandle($"weather_{biome.defName}_{weather.defName}_early",
                "    " + "ColdDesertNights_BiomeWeatherEarly".Translate(),
                "ColdDesertNights_BiomeWeatherEarly_Desc".Translate(
                    (Favorability.Neutral <= weather.favorability ? "ColdDesertNights_Checked" : "ColdDesertNights_Unchecked").Translate()),
                Favorability.Neutral <= weather.favorability);

            // And set our visibility predicates...
            commonality.VisibilityPredicate
                = allowRepeating.VisibilityPredicate
                    = allowEarly.VisibilityPredicate
                        = visibilityFunc;
        }

        /// <summary>
        /// Get the weather commonality based on the various factors
        /// </summary>
        /// <param name="map">The map to check it on</param>
        /// <param name="rainAllowed">The next tick rain is allowed at</param>
        /// <param name="weatherTemp">The bounded weather temperature to use</param>
        /// <param name="preventRain">If we should prevent rain due to active conditions</param>
        /// <returns></returns>
        public float GetCommonality(Map map, int rainAllowed, float weatherTemp, bool preventRain)
        {
            // Check the variety of things we need to check to see if we should use this weather or not
            if (!allowRepeating && weather == map.weatherManager.curWeather ||
                !weather.temperatureRange.Includes(weatherTemp) ||
                !allowEarly && GenDate.DaysPassed < 8 ||
                weather.rainRate > 0.100000001490116 && Find.TickManager.TicksGame < rainAllowed ||
                weather.rainRate > 0.100000001490116 && preventRain)
            {
                return 0.0f;
            }

            // Otherwise, find our base commonality:
            var currentCommonality = commonality.Value;

            // If we need to put out fires, increase rainfall considerably:
            if (map.fireWatcher.LargeFireDangerPresent && weather.rainRate > 0.100000001490116) currentCommonality *= 20f;

            // Otherwise calculate our whole curve for rainfall and stuff...
            if (weather.commonalityRainfallFactor != null)
            {
                currentCommonality *= weather.commonalityRainfallFactor.Evaluate(map.TileInfo.rainfall);
            }

            return currentCommonality;
        }
    }
}
