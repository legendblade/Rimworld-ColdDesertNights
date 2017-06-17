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
        private bool hasChanged;

        public ListTypeDrawer(SettingHandle<T> handle, List<T> options, Func<T, string> getLabelFunc)
        {
            this.handle = handle;
            this.options = options;
            this.getLabelFunc = getLabelFunc;
        }

        public bool Draw(Rect controlRect)
        {
            // Get our current label and try to draw it to the screen:
            var label = GenText.ToTitleCaseSmart(getLabelFunc.Invoke(handle.Value));
            if (!Widgets.ButtonText(controlRect, label)) return true;

            // Iterate our options:
            var opts = options.Select(name => new {name, optLabel = GenText.ToTitleCaseSmart(getLabelFunc.Invoke(name))})
                .Select(t => new FloatMenuOption(t.optLabel, () =>
                {
                    handle.Value = t.name;
                    hasChanged = true;
                })).ToList();

            Find.WindowStack.Add(new FloatMenu(opts));

            if (!hasChanged) return false;

            hasChanged = false;
            return true;
        }
    }
}
