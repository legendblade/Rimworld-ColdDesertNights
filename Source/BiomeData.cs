using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ColdDesertNights.Utility;
using HugsLib.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColdDesertNights
{

    public class BiomeData
    {
        private const float HalfPi = Mathf.PI / 2;

        private static readonly Dictionary<TemperatureFunctions, Func<float, float, float>> Functions =
            new Dictionary<TemperatureFunctions, Func<float, float, float>>
            {
                {TemperatureFunctions.Vanilla, (f, m) => Mathf.Cos(f) * m},
                {TemperatureFunctions.Flatter, (f, m) => m * Mathf.Sin(HalfPi * Mathf.Cos(f))},
                {
                    TemperatureFunctions.Flattest,
                    (f, m) => m * Mathf.Sqrt(26 / (1 + 25 * Mathf.Pow(Mathf.Cos(f), 2))) * Mathf.Cos(f)
                }
            };

        /// <summary>
        /// Lists off the default temperatures per condition.  This, unfortunately, can't be
        /// grabbed automatically due to how the vanilla code handles it
        /// </summary>
        private static readonly Dictionary<GameConditionDef, float> DefaultConditionTemps = 
            new Dictionary<GameConditionDef, float>
            {
                { GameConditionDefOf.ColdSnap, -20f },
                { GameConditionDefOf.HeatWave, 17f }
            };

        // Our actual values
        private Func<float, float, float> function;
        private float multiplier;
        private float offset;

        private readonly Dictionary<WeatherDef, WeatherData> weatherCommonalities =
            new Dictionary<WeatherDef, WeatherData>();
        private readonly Dictionary<GameConditionDef, SettingHandle<float>> conditionOffsets =
            new Dictionary<GameConditionDef, SettingHandle<float>>();

        private SimpleCurve seasonalTempVariationCurve = new SimpleCurve
        {
            new CurvePoint(0.0f, 3f),
            new CurvePoint(0.1f, 4f),
            new CurvePoint(1f, 28f)
        };

        // Our setting handles
        private SettingHandle<TemperatureFunctions> settingFunc;
        private SettingHandle<float> settingMultiplier;
        private SettingHandle<float> settingOffset;
        private SettingHandle<bool> settingIgnoreRainLimit;
        private SettingHandle<float> minWeatherTemperature;
        private SettingHandle<float> maxWeatherTemperature;
        private SettingHandle<float> settingSeasonal;

        /// <summary>
        /// Initializes the biome data from the given <see cref="ModSettingsPack"/> settings
        /// and <see cref="BiomeDef"/>, creating our settings in the process.
        /// </summary>
        /// <param name="settings">The setting pack to use</param>
        /// <param name="biome">The biome to base this off of</param>
        /// <param name="visibilityFunc">Function which returns if we should display this now</param>
        /// <param name="currentPane">The current settings pane we're on</param>
        /// <param name="weathers">A list of weathers to iterate through</param>
        /// <param name="conditions">A list of game conditions to iterate through</param>
        public BiomeData(ModSettingsPack settings, BiomeDef biome, SettingHandle.ShouldDisplay visibilityFunc, 
            SettingHandle<SettingsPane> currentPane, 
            List<WeatherDef> weathers, List<GameConditionDef> conditions)
        {
            // Build out the key:
            var key = Regex.Replace(biome.defName, "[^A-Za-z]", "");

            // Init all of our various settings
            InitGeneralSettings(settings, biome, key,
                () => currentPane.Value == SettingsPane.General && visibilityFunc());
            InitWeatherSettings(settings, biome, key, weathers,
                () => currentPane.Value == SettingsPane.Weather && visibilityFunc());
            InitConditionSettings(settings, biome, key, conditions,
                () => currentPane.Value == SettingsPane.Conditions && visibilityFunc());

            // Port things from the v1 labeling:
            var v1Key = Regex.Replace(biome.label, "[^A-Za-z]", ""); // <-- This was a bad plan.

            if (!string.IsNullOrEmpty(v1Key))
            {
                var oldFunc = settings.PeekValue($"temp_func_{v1Key}");
                if (oldFunc != null && settingFunc.HasDefaultValue())
                {
                    settingFunc.StringValue = oldFunc;
                    settings.TryRemoveUnclaimedValue($"temp_func_{v1Key}");
                }

                var oldMult = settings.PeekValue($"temp_multiplier_{v1Key}");
                if (oldMult != null && settingMultiplier.HasDefaultValue())
                {
                    settingMultiplier.StringValue = oldMult;
                    settings.TryRemoveUnclaimedValue($"temp_multiplier_{v1Key}");
                }

                var oldOffset = settings.PeekValue($"temp_offset_{v1Key}");
                if (oldOffset != null && settingOffset.HasDefaultValue())
                {
                    settingOffset.StringValue = settings.PeekValue($"temp_offset_{v1Key}");
                    settings.TryRemoveUnclaimedValue($"temp_offset_{v1Key}");
                }
            }
        }

        /// <summary>
        /// Calculates the temperature for the biome.
        /// </summary>
        /// <param name="input">The value to calculate the temperature at</param>
        /// <returns>The calculated temperature</returns>
        public float CalculateTemp(float input)
        {
            return function.Invoke(input, multiplier) + offset;
        }

        /// <summary>
        /// Calculates the seasonal temperature for the biome.
        /// </summary>
        /// <param name="input">The value to calculate the temperature at</param>
        /// <returns>The calculated temperature</returns>
        public float CalculateSeasonalTemp(float input)
        {
            return seasonalTempVariationCurve.Evaluate(input);
        }

        /// <summary>
        /// Forces the choice weather to be within the given temperature bounds
        /// </summary>
        /// <param name="input">The input value</param>
        /// <returns>The bounded input</returns>
        public float BoundWeatherTemp(float input)
        {
            return Mathf.Max(Mathf.Min(input, minWeatherTemperature.Value), maxWeatherTemperature.Value);
        }

        /// <summary>
        /// Gets the <see cref="WeatherData"/> for this biome/weather
        /// </summary>
        /// <param name="weather">The weather to check</param>
        /// <returns>The weather data</returns>
        public WeatherData GetWeatherData(WeatherDef weather)
        {
            return weatherCommonalities[weather];
        }

        /// <summary>
        /// Checks if we can ignore the rain limit
        /// </summary>
        /// <returns>True if we can; false otherwise</returns>
        public bool CanIgnoreRainLimits()
        {
            return settingIgnoreRainLimit.Value;
        }

        /// <summary>
        /// Gets the temperature offset for a given game condition; returns false if we should use the
        /// vanilla cacluatation instead
        /// </summary>
        /// <param name="condition">The condition to check for</param>
        /// <returns>True if we overwrote the value, false if we should use vanilla instead.</returns>
        public float GetBiomeConditionTemperatureOffset(GameCondition condition)
        {
            // Check if we have a value:
            SettingHandle<float> offsetMax;
            if (!conditionOffsets.TryGetValue(condition.def, out offsetMax) || offsetMax.HasDefaultValue()) return condition.TemperatureOffset();

            // Otherwise, do the thing:
            return GameConditionUtility.LerpInOutValue(condition.TicksPassed, condition.TicksLeft, 12000f,
                offsetMax);
        }

        /// <summary>
        /// Safely updates our function, defaulting back to vanilla if it couldn't be found
        /// </summary>
        /// <param name="type">The type to try and use</param>
        private void UpdateFunction(TemperatureFunctions type)
        {
            if (!Functions.TryGetValue(type, out function)) function = Functions[TemperatureFunctions.Vanilla];
        }

        /// <summary>
        /// Recalculates our multiplier and offset, adjusting so that the hottest
        /// point in the day is roughly the same
        /// </summary>
        private void RecalculateMultiplierAndOffset()
        {
            multiplier = settingMultiplier.Value / 2;
            offset = 7f - multiplier + settingOffset.Value;
        }

        /// <summary>
        /// Recalculates the seasonal temperature curve in this biome
        /// </summary>
        /// <param name="value">The maximal shift</param>
        private void RecalculateSeasonalCurve(float value)
        {
            value = value / 2;
            var shift = value / 28;

            seasonalTempVariationCurve = new SimpleCurve
            {
                new CurvePoint(0.0f, 3f * shift),
                new CurvePoint(0.1f, 4f * shift),
                new CurvePoint(1f, value)
            };
        }

        /// <summary>
        /// Initalizes all of our 'general' tab settings.
        /// </summary>
        /// <param name="settings">The settings instance to use</param>
        /// <param name="biome">The biome we're working with</param>
        /// <param name="key">The key to use</param>
        /// <param name="visibilityFunc">Our base visibility function</param>
        private void InitGeneralSettings(ModSettingsPack settings, BiomeDef biome, string key,
            SettingHandle.ShouldDisplay visibilityFunc)
        {
            settingFunc = settings.GetHandle($"temp_func_{key}",
                "ColdDesertNights_Function".Translate(GenText.ToTitleCaseSmart(biome.label)),
                "ColdDesertNights_Function_Desc".Translate(),
                TemperatureFunctions.Vanilla, null, "ColdDesertNights_Function_Enum_");
            settingMultiplier = settings.GetHandle(
                $"temp_multiplier_{key}",
                "ColdDesertNights_Multiplier".Translate(GenText.ToTitleCaseSmart(biome.label)),
                "ColdDesertNights_Multiplier_Desc".Translate(), 14.0f,
                Validators.FloatRangeValidator(-200, 200));
            settingOffset = settings.GetHandle($"temp_offset_{key}",
                "ColdDesertNights_Offset".Translate(GenText.ToTitleCaseSmart(biome.label)),
                "ColdDesertNights_Offset_Desc".Translate(), 0.0f,
                Validators.FloatRangeValidator(-200, 200));
            settingSeasonal = settings.GetHandle($"temp_seasonal_{key}",
                "ColdDesertNights_Seasonal".Translate(GenText.ToTitleCaseSmart(biome.label)),
                "ColdDesertNights_Seasonal_Desc".Translate(), 56.0f,
                Validators.FloatRangeValidator(-400, 400));

            settingFunc.VisibilityPredicate =
                settingMultiplier.VisibilityPredicate =
                    settingOffset.VisibilityPredicate =
                        settingSeasonal.VisibilityPredicate =
                            visibilityFunc;


            // And use them to init our values...
            UpdateFunction(settingFunc.Value);
            RecalculateMultiplierAndOffset();
            RecalculateSeasonalCurve(settingSeasonal.Value);

            // Sync them up when they get changed
            settingFunc.OnValueChanged += UpdateFunction;
            settingMultiplier.OnValueChanged += value => RecalculateMultiplierAndOffset();
            settingOffset.OnValueChanged += value => RecalculateMultiplierAndOffset();
            settingSeasonal.OnValueChanged += RecalculateSeasonalCurve;
        }

        /// <summary>
        /// Initialize condition specific settings
        /// </summary>
        /// <param name="settings">The settings instance to use</param>
        /// <param name="biome">The biome we're working with</param>
        /// <param name="key">The key to use</param>
        /// <param name="conditions">The conditions to iterate through</param>
        /// <param name="visibilityFunc">Our base visibility function</param>
        private void InitConditionSettings(ModSettingsPack settings, BiomeDef biome, string key,
            List<GameConditionDef> conditions, SettingHandle.ShouldDisplay visibilityFunc)
        {
            // Iterate through each of our conditions...
            foreach (var condition in conditions)
            {
                var setting = settings.GetHandle($"condition_{key}_{condition.defName}_offset",
                    "ColdDesertNights_ConditionTemp".Translate(GenText.ToTitleCaseSmart(condition.label)),
                    "ColdDesertNights_ConditionTemp_Desc".Translate(),
                    DefaultConditionTemps.ContainsKey(condition) ? DefaultConditionTemps[condition] : 0f,
                    Validators.FloatRangeValidator(-400, 400));
                setting.VisibilityPredicate = visibilityFunc;
                conditionOffsets[condition] = setting;
            }
        }

        /// <summary>
        /// Initialize weather specific settings
        /// </summary>
        /// <param name="settings">The settings instance to use</param>
        /// <param name="biome">The biome we're working with</param>
        /// <param name="key">The key to use</param>
        /// <param name="weathers">The weathers to iterate through</param>
        /// <param name="visibilityFunc">Our base visibility function</param>
        private void InitWeatherSettings(ModSettingsPack settings, BiomeDef biome, string key, List<WeatherDef> weathers, 
            SettingHandle.ShouldDisplay visibilityFunc)
        {
            // Per-biome rain and snowfall multipliers
            foreach (var weather in weathers)
            {
                weatherCommonalities[weather] = new WeatherData(settings, biome, weather, visibilityFunc);
            }

            SpacerDrawer.GenerateSpacer("ColdDesertNights_WeatherMiscSettings".Translate(), settings, visibilityFunc);

            // If we're allowed to bypass the rain limits
            settingIgnoreRainLimit = settings.GetHandle(
                $"ignore_rain_limit_{key}",
                "    " + "ColdDesertNights_IgnoreRainLimit".Translate(),
                "ColdDesertNights_IgnoreRainLimit_Desc".Translate(), false);

            // Force weather into the given range
            minWeatherTemperature = settings.GetHandle(
                $"weather_temp_min_{key}",
                "    " + "ColdDesertNights_WeatherTempMin".Translate(),
                "ColdDesertNights_WeatherTempMin_Desc".Translate(), -999f);
            maxWeatherTemperature = settings.GetHandle(
                $"weather_temp_max_{key}",
                "    " + "ColdDesertNights_WeatherTempMax".Translate(),
                "ColdDesertNights_WeatherTempMax_Desc".Translate(), 999f);

            // Set our visibility predicates:
            settingIgnoreRainLimit.VisibilityPredicate =
                minWeatherTemperature.VisibilityPredicate =
                    maxWeatherTemperature.VisibilityPredicate =
                        visibilityFunc;
        }
    }
}
