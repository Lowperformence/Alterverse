using System;
using System.Globalization;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Shared searchable language dialog for menu and paused-match settings.</summary>
    internal sealed class LanguagePicker
    {
        public bool IsOpen { get; private set; }
        Vector2 scroll;
        string search = "";
        public void Open() { IsOpen = true; search = ""; scroll = Vector2.zero; GUI.FocusControl(null); }
        public void Close() { IsOpen = false; GUI.FocusControl(null); }
        public void DrawControl(FrontEndTheme theme, Rect rect)
        {
            var language = GameLanguage.Current;
            string label = language.Name + "  /  " + language.Code;
            if (theme.Action(rect, label, selected: true)) Open();
        }
        public void Draw(FrontEndTheme theme, Rect bounds)
        {
            if (!IsOpen) return;
            FrontEndTheme.Fill(bounds, new Color(.008f, .012f, .02f, .94f));
            float width = Mathf.Min(760, bounds.width - 32);
            float height = Mathf.Min(650, bounds.height - 24);
            Rect panel = new Rect(bounds.center.x - width / 2, bounds.center.y - height / 2, width, height);
            theme.Panel(panel);
            theme.FitLabel(new Rect(panel.x + 24, panel.y + 14, width - 160, 38), GameLanguage.Text("Choose language"), theme.Heading);
            if (theme.Action(new Rect(panel.xMax - 124, panel.y + 14, 100, 38), GameLanguage.Text("Close"))) { Close(); return; }
            theme.Label(new Rect(panel.x + 24, panel.y + 58, width - 48, 24), GameLanguage.Text("Search languages"), theme.Small, true);
            search = GUI.TextField(new Rect(panel.x + 24, panel.y + 86, width - 48, 42), search, 80, theme.Field);
            Rect viewport = new Rect(panel.x + 24, panel.y + 140, width - 48, height - 198);
            int matches = 1;
            foreach (var option in GameLanguage.Languages) if (Matches(option, search)) matches++;
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, viewport.width - 24, matches * 54));
            try
            {
                if (theme.Action(new Rect(0, 0, viewport.width - 28, 48), GameLanguage.Text("Use device language"),
                    selected: GameLanguage.SelectedCode == GameLanguage.DeviceDefault))
                { GameLanguage.Select(GameLanguage.DeviceDefault); Close(); }
                int row = 1;
                foreach (var option in GameLanguage.Languages)
                {
                    if (!Matches(option, search)) continue;
                    string label = option.Name == option.EnglishName ? option.Name : option.Name + "  /  " + option.EnglishName;
                    float top = row++ * 54;
                    if (top + 48 < scroll.y || top > scroll.y + viewport.height) continue;
                    if (theme.Action(new Rect(0, top, viewport.width - 28, 48), label,
                        selected: GameLanguage.SelectedCode == option.Code))
                    { GameLanguage.Select(option.Code); Close(); }
                }
            }
            finally { GUI.EndScrollView(); }
            theme.Label(new Rect(panel.x + 24, panel.yMax - 42, width - 48, 28), GameLanguage.Text("Changes apply immediately."), theme.Small, true);
        }
        static bool Matches(GameLanguage.Option language, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            value = value.Trim();
            const CompareOptions flags = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
            var compare = CultureInfo.InvariantCulture.CompareInfo;
            return compare.IndexOf(language.Name, value, flags) >= 0 ||
                compare.IndexOf(language.EnglishName, value, flags) >= 0 ||
                compare.IndexOf(language.Code, value, flags) >= 0;
        }
    }
}
