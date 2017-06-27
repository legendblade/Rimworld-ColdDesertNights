using System;
using HugsLib.Settings;
using UnityEngine;
using Verse;

namespace ColdDesertNights.Utility
{
    public static class SpacerDrawer
    {
        private static readonly Color LineColor = new Color(0.3f, 0.3f, 0.3f);
        private static int spacers = 0;

        public static bool Draw(string label, Rect controlRect)
        {
            var textWidth = Text.CalcSize(label).x + 10;
            var left = controlRect.x + textWidth - controlRect.width;
            var color = GUI.color;
            GUI.color = LineColor;
            Widgets.DrawLineHorizontal(left, controlRect.height / 2 + controlRect.y, controlRect.width * 2 - textWidth);
            GUI.color = color;
            return false;
        }

        /// <summary>
        /// Generates a randomly named settings spacer
        /// </summary>
        /// <param name="label">The label to use</param>
        /// <param name="settings">The settings handle to use</param>
        /// <param name="visibilityFunc">The visibility predicate</param>
        public static void GenerateSpacer(string label, ModSettingsPack settings, SettingHandle.ShouldDisplay visibilityFunc)
        {
            var spacer = settings.GetHandle<bool>("s" + spacers++, label,
                string.Empty);
            spacer.CustomDrawer = r => Draw(label, r);
            spacer.Unsaved = true;
            spacer.VisibilityPredicate = visibilityFunc;
        }
    }
}
