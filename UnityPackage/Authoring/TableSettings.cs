using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// The settings a table's source-language file chooses at its top: <c>@loading</c>, <c>@delivery</c> and
    /// <c>@generateCode</c>. A setting left out, or given a value it doesn't know, keeps its default.
    /// </summary>
    public readonly struct TableSettings
    {
        /// <summary>The settings of a table whose file chooses none.</summary>
        public static readonly TableSettings Default = new(TableLoading.Preload, TableDelivery.Embedded, true);

        /// <summary>Creates a set of table settings.</summary>
        public TableSettings(TableLoading loading, TableDelivery delivery, bool generatesCode)
        {
            Loading = loading;
            Delivery = delivery;
            GeneratesCode = generatesCode;
        }

        /// <summary>When the table is in memory.</summary>
        public TableLoading Loading { get; }

        /// <summary>How the table ships in builds.</summary>
        public TableDelivery Delivery { get; }

        /// <summary>Whether the table gets generated keys.</summary>
        public bool GeneratesCode { get; }

        /// <summary>
        /// Reads the settings of a table's source-language <paramref name="document"/>, adding an error to
        /// <paramref name="issues"/> for every value that isn't one of the allowed ones.
        /// </summary>
        public static TableSettings Read(TableDocument document, ICollection<DocumentIssue> issues)
        {
            if (document == null)
            {
                return Default;
            }
            TableLoading loading = Default.Loading;
            TableDelivery delivery = Default.Delivery;
            bool generatesCode = Default.GeneratesCode;
            for (int i = 0; i < document.Settings.Count; i++)
            {
                DocumentProperty setting = document.Settings[i];
                switch (setting.Name)
                {
                    case DocumentNames.Loading:
                        loading = ReadChoice(setting, TableLoading.Preload, TableLoading.OnDemand, loading, issues);
                        break;
                    case DocumentNames.Delivery:
                        delivery = ReadChoice(setting, TableDelivery.Embedded, TableDelivery.Streaming, delivery, issues);
                        break;
                    case DocumentNames.GenerateCode:
                        generatesCode = ReadChoice(setting, true, false, generatesCode, issues);
                        break;
                }
            }
            return new TableSettings(loading, delivery, generatesCode);
        }

        /// <summary>Returns which of two allowed values a setting names, ignoring case, or reports it and keeps <paramref name="fallback"/>.</summary>
        private static T ReadChoice<T>(DocumentProperty setting, T first, T second, T fallback, ICollection<DocumentIssue> issues)
        {
            string firstName = ToSettingText(first);
            string secondName = ToSettingText(second);
            if (string.Equals(setting.Value, firstName, StringComparison.OrdinalIgnoreCase))
            {
                return first;
            }
            if (string.Equals(setting.Value, secondName, StringComparison.OrdinalIgnoreCase))
            {
                return second;
            }
            issues?.Add(new DocumentIssue(IssueSeverity.Error, setting.Line, 1,
                $"'@{setting.Name}' is '{firstName}' or '{secondName}', not '{setting.Value}'; it stays '{ToSettingText(fallback)}'."));
            return fallback;
        }

        private static string ToSettingText<T>(T value) => value is bool flag ? (flag ? "true" : "false") : value.ToString();
    }
}
