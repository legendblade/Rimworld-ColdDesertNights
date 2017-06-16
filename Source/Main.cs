using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Harmony;
using HugsLib;
using HugsLib.Settings;
using RimWorld;
using UnityEngine;
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
        public static IDictionary<BiomeDef, SettingHandle<float>> NightTemperatureDifferential { get; private set; }

        /// <summary>
        /// Called when defs are loaded
        /// </summary>
        public override void DefsLoaded()
        {
            GetBiomes();
        }

        /// <summary>
        /// Called when a map is loaded
        /// </summary>
        /// <param name="map">The loaded map</param>
        public override void MapLoaded(Map map)
        {
            var tile = Find.WorldGrid[map.Tile];
            var baseTemp = tile.temperature;
            for (var i = 0; i < 24; i++)
            {
                var ticks = i * 2500;
                var hourOffset = GenTemperature.OffsetFromSunCycle(ticks, map.Tile);
                Logger.Trace($"{i.ToString().PadLeft(2)}: base {baseTemp}, hour {hourOffset}");
            }
        }

        [HarmonyPatch(typeof(GenTemperature), nameof(GenTemperature.OffsetFromSunCycle))]
        // ReSharper disable once UnusedMember.Global
        public static class OffsetFromSunCyclePatch
        {
            // ReSharper disable once UnusedMember.Global
            public static bool Prefix(ref float __result, int absTick, int tile)
            {
                var num = GenDate.DayPercent(absTick, Find.WorldGrid.LongLatOf(tile).x);
                var f = 6.28318548f * (num + 0.32f);
                __result = Mathf.Cos(f) * 7f;
                Log.Message($"num: {num}, f: {f}, result: {__result}");
                return false;
            }
        }

        /// <summary>
        /// Initializes the list of available <see cref="BiomeDef"/>s.
        /// </summary>
        private void GetBiomes()
        {
            NightTemperatureDifferential =
                DefDatabase<BiomeDef>.AllDefs.Where(b => b.implemented && b.canBuildBase)
                    .ToDictionary(t => t,
                        v => Settings.GetHandle($"nightTemp_{Regex.Replace(v.label, "[^A-Za-z]", "_")}", v.label,
                            "The temperature offset for nights in this biome.", 0.0f,
                            Validators.FloatRangeValidator(-200, 200)));

            foreach (var biome in NightTemperatureDifferential)
            {
                biome.Value.SpinnerIncrement = 1;
                Logger.Message($"Found {biome.Key.label}, which we are assigning a temperature difference of {biome.Value}");
            }
        }
    }
}
