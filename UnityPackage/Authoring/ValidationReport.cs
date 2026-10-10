using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>Everything a validation found.</summary>
    public sealed class ValidationReport
    {
        /// <summary>Creates a report of <paramref name="issues"/>.</summary>
        public ValidationReport(IReadOnlyList<ValidationIssue> issues)
        {
            Issues = issues ?? Array.Empty<ValidationIssue>();
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i].Severity == IssueSeverity.Error)
                {
                    ErrorCount++;
                }
                else
                {
                    WarningCount++;
                }
            }
        }

        /// <summary>Every issue, ordered by table, then file, then line.</summary>
        public IReadOnlyList<ValidationIssue> Issues { get; }

        /// <summary>How many issues are errors.</summary>
        public int ErrorCount { get; }

        /// <summary>How many issues are warnings.</summary>
        public int WarningCount { get; }

        /// <summary>Whether a build may go ahead: nothing found is an error.</summary>
        public bool IsPassing => ErrorCount == 0;
    }
}
