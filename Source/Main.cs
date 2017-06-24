﻿using System.Collections.Generic;
using System.Linq;
using ColdDesertNights.Utility;
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

        /// <summary>
        /// Initializes the list of available <see cref="BiomeDef"/>s.
        /// </summary>
        private void GetBiomes()
        {
            // Get our biome list
            var biomes = DefDatabase<BiomeDef>.AllDefs.Where(b => b.implemented && b.canBuildBase).ToList();

            // Set our visibility field:
            var currentBiomeSetting = Settings.GetHandle("tempCurBiome", "Biome".Translate(),
                "ColdDesertNights_BiomeSelector".Translate(), biomes.First());
            currentBiomeSetting.Unsaved = true;
            currentBiomeSetting.CustomDrawer = new ListTypeDrawer<BiomeDef>(currentBiomeSetting, biomes, b => b.label).Draw;

            BiomeSettings = biomes.ToDictionary(t => t, v => new BiomeData(Settings, v, () => currentBiomeSetting.Value.Equals(v)));
        }
    }
}
