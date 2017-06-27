using System;
using System.Collections.Generic;
using System.Linq;
using HugsLib.Settings;
using UnityEngine;
using Verse;

namespace ColdDesertNights.Utility
{
    public class ListTypeDrawer<T>
    {
        private readonly SettingHandle<T> handle;
        private readonly List<T> options;
        private readonly Func<T, string> getLabelFunc;
        private readonly bool includeDefaultOption;
        private bool hasChanged;

        public ListTypeDrawer(SettingHandle<T> handle, IEnumerable<T> options, Func<T, string> getLabelFunc, bool includeDefaultOption)
        {
            this.handle = handle;
            this.options = new List<T>(options);
            this.getLabelFunc = getLabelFunc;
            this.includeDefaultOption = includeDefaultOption;
        }

        public bool Draw(Rect controlRect)
        {
            // Get our current label and try to draw it to the screen:
            var label = GetLabel(handle.Value);
            if (!Widgets.ButtonText(controlRect, label)) return true;

            // Iterate our options:
            var opts = options
                .Select(t => new FloatMenuOption(GetLabel(t), () =>
                {
                    handle.Value = t;
                    hasChanged = true;
                })).ToList();

            Find.WindowStack.Add(new FloatMenu(opts));

            if (!hasChanged) return false;

            hasChanged = false;
            return true;
        }

        /// <summary>
        /// Gets the label for the given instance
        /// </summary>
        /// <param name="opt">The option to get the label for</param>
        /// <returns>The text</returns>
        private string GetLabel(T opt)
        {
            return opt == null || includeDefaultOption && EqualityComparer<T>.Default.Equals(opt, default(T))
                ? "ColdDesertNights_SelectList_Default".Translate()
                : GenText.ToTitleCaseSmart(getLabelFunc.Invoke(opt));
        }
    }
}
