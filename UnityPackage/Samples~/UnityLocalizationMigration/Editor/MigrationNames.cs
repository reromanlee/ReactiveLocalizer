using System;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Samples.UnityLocalizationMigration.Editor
{
    /// <summary>
    /// Turns Unity Localization's table, key and locale names into names the naming rule accepts, in PascalCase:
    /// <c>Settings Page</c> becomes <c>SettingsPage</c> and <c>menu/play_button</c> becomes <c>MenuPlayButton</c>.
    /// A name that already follows the rule stays as it is.
    /// </summary>
    public static class MigrationNames
    {
        /// <summary>
        /// Returns <paramref name="text"/> as a valid name: words joined, each starting upper case, everything but
        /// ASCII letters and digits dropped, and <paramref name="prefix"/> in front when nothing or a digit is left first.
        /// </summary>
        public static string ToName(string text, string prefix)
        {
            if (NameRules.IsValid(text) && text.IndexOf('_') < 0 && !char.IsLower(text[0]))
            {
                return text;
            }
            StringBuilder name = new(text?.Length ?? 0);
            bool isWordStart = true;
            foreach (char character in text ?? string.Empty)
            {
                bool isLetter = (uint)((character | 0x20) - 'a') <= 'z' - 'a';
                bool isDigit = (uint)(character - '0') <= 9;
                if (!isLetter && !isDigit)
                {
                    isWordStart = true;
                    continue;
                }
                name.Append(isWordStart ? char.ToUpperInvariant(character) : character);
                isWordStart = false;
            }
            if (name.Length == 0 || char.IsDigit(name[0]))
            {
                name.Insert(0, prefix);
            }
            if (name.Length > NameRules.MaximumLength)
            {
                name.Length = NameRules.MaximumLength;
            }
            return name.ToString();
        }

        /// <summary>
        /// Returns <paramref name="name"/>, or it with the lowest number appended that no name in
        /// <paramref name="taken"/> has, ignoring case as names do, and adds the result to <paramref name="taken"/>.
        /// </summary>
        public static string MakeUnique(string name, HashSet<string> taken)
        {
            string unique = name;
            for (int number = 2; taken.Contains(unique); number++)
            {
                string suffix = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                unique = name.Length + suffix.Length > NameRules.MaximumLength
                    ? name.Substring(0, NameRules.MaximumLength - suffix.Length) + suffix
                    : name + suffix;
            }
            taken.Add(unique);
            return unique;
        }

        /// <summary>Returns a set for <see cref="MakeUnique"/> that compares names as the naming rule does, ignoring case.</summary>
        public static HashSet<string> CreateTakenSet(params string[] reserved)
        {
            return new HashSet<string>(reserved ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
