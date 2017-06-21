using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

        // Our actual values
        private Func<float, float, float> function;
        private float multiplier;
        private float offset;

        // Our setting handles
        private readonly SettingHandle<TemperatureFunctions> settingFunc;
        private readonly SettingHandle<float> settingMultiplier;
        private readonly SettingHandle<float> settingOffset;

        /// <summary>
        /// Initializes the biome data from the given <see cref="ModSettingsPack"/> settings
        /// and <see cref="BiomeDef"/>, creating our settings in the process.
        /// </summary>
        /// <param name="settings">The setting pack to use</param>
        /// <param name="biome">The biome to base this off of</param>
        /// <param name="visibilityFunc">Function which returns if we should display this now</param>
        public BiomeData(ModSettingsPack settings, BiomeDef biome, SettingHandle.ShouldDisplay visibilityFunc)
        {
            // Build out the key:
            var key = Regex.Replace(biome.defName, "[^A-Za-z]", "");

            // Create our settings handles:
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


            // And use them to init our values...
            UpdateFunction(settingFunc.Value);
            RecalculateMultiplierAndOffset();

            // Sync them up when they get changed
            settingFunc.OnValueChanged += UpdateFunction;
            settingMultiplier.OnValueChanged += value => RecalculateMultiplierAndOffset();
            settingOffset.OnValueChanged += value => RecalculateMultiplierAndOffset();

            // Set our visibility predicates:
            settingFunc.VisibilityPredicate =
                settingMultiplier.VisibilityPredicate = settingOffset.VisibilityPredicate = visibilityFunc;
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
    }

}
