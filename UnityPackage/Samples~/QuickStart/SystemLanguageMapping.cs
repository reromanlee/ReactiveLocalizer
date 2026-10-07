using System.Collections.Generic;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Samples.QuickStart
{
    /// <summary>
    /// Maps the OS language to one of a catalog's languages. Languages named after Unity's
    /// <see cref="SystemLanguage"/> values, such as English or ChineseSimplified, match on their own; the switch is
    /// the place for everything a project names its own way.
    /// </summary>
    public static class SystemLanguageMapping
    {
        /// <summary>Returns the catalog language for the OS language, or false when the catalog doesn't have one.</summary>
        public static bool TryMap(IReadOnlyList<LanguageInfo> languages, out LanguageKey language)
        {
            SystemLanguage system = Application.systemLanguage;
            switch (system)
            {
                case SystemLanguage.Unknown:
                    language = default;
                    return false;
                case SystemLanguage.Chinese:
                    // Some platforms only report Chinese; Simplified is what most of them mean.
                    return GlobalLocalizer.TryFind(languages, nameof(SystemLanguage.ChineseSimplified), out language);
                default:
                    return GlobalLocalizer.TryFind(languages, system.ToString(), out language);
            }
        }
    }
}
