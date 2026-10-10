using reromanlee.ReactiveLocalizer.Documents;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>A problem a validation found, with where it is.</summary>
    public sealed class ValidationIssue
    {
        /// <summary>Creates an issue found at <paramref name="location"/>.</summary>
        /// <param name="severity">Whether it fails builds.</param>
        /// <param name="location">Where it is: a file with its line and column, or an asset; empty when it is nowhere in particular.</param>
        /// <param name="message">What is wrong.</param>
        public ValidationIssue(IssueSeverity severity, string location, string message)
        {
            Severity = severity;
            Location = location ?? string.Empty;
            Message = message;
        }

        /// <summary>Errors fail builds; warnings are worth fixing but never do.</summary>
        public IssueSeverity Severity { get; }

        /// <summary>Where the problem is: a file with its line and column, such as <c>Shop.Russian.lang(12,1)</c>, or an asset.</summary>
        public string Location { get; }

        /// <summary>What is wrong, written for the person who fixes it.</summary>
        public string Message { get; }

        /// <summary>Returns the issue the way compilers print one, its location first, so the Console points at it.</summary>
        public override string ToString()
        {
            string severity = Severity == IssueSeverity.Error ? "error" : "warning";
            return Location.Length > 0 ? $"{Location}: {severity}: {Message}" : $"{severity}: {Message}";
        }
    }
}
