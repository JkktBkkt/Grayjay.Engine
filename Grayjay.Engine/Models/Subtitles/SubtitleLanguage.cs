using System;
using System.Globalization;
using System.Linq;

namespace Grayjay.Engine.Models.Subtitles
{
    public static class SubtitleLanguage
    {
        private static readonly CultureInfo[] Cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .Where(c => !string.IsNullOrEmpty(c.Name)).ToArray();

        public static string Resolve(string? language, string? name)
        {
            var tag = language?.Trim().Replace('_', '-');
            if (!string.IsNullOrEmpty(tag) && tag != "und" && tag != "df")
            {
                var culture = Cultures.FirstOrDefault(c => string.Equals(c.Name, tag.Split('-')[0], StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.ThreeLetterISOLanguageName, tag, StringComparison.OrdinalIgnoreCase));
                if (culture != null) return tag.Length == 3 ? culture.Name : tag;
            }
            var label = name?.Split(new[] { " (", " [", " - " }, StringSplitOptions.None)[0].Trim();
            var match = Cultures.FirstOrDefault(c => string.Equals(c.EnglishName, label, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.NativeName, label, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.DisplayName, label, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Name, label, StringComparison.OrdinalIgnoreCase));
            return match?.Name ?? "und";
        }
    }
}
