using reromanlee.ReactiveLocalizer.Documents;
using System.Collections.Generic;
using UnityEditor.AssetImporters;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Reports the problems found while importing a localization file, each as <c>path(line,column): error: ...</c>,
    /// the way compilers do, so the Console names the file and the position in every message.
    /// </summary>
    internal static class ImportReports
    {
        public static void Report(AssetImportContext context, IReadOnlyList<DocumentIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                DocumentIssue issue = issues[i];
                string message = $"{context.assetPath}{issue}";
                if (issue.Severity == IssueSeverity.Error)
                {
                    context.LogImportError(message);
                }
                else
                {
                    context.LogImportWarning(message);
                }
            }
        }
    }
}
