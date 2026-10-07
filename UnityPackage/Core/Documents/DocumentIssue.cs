namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>A problem found in a localization file, with the position it was found at.</summary>
    public readonly struct DocumentIssue
    {
        /// <summary>Creates an issue at a 1-based <paramref name="line"/> and <paramref name="column"/>.</summary>
        public DocumentIssue(IssueSeverity severity, int line, int column, string message)
        {
            Severity = severity;
            Line = line;
            Column = column;
            Message = message;
        }

        /// <summary>Whether the issue makes the affected line unusable.</summary>
        public IssueSeverity Severity { get; }

        /// <summary>Line of the issue, counted from 1.</summary>
        public int Line { get; }

        /// <summary>Column of the issue within its line, counted from 1.</summary>
        public int Column { get; }

        /// <summary>What is wrong, written for the person who fixes it.</summary>
        public string Message { get; }

        /// <summary>
        /// Returns the issue the way compilers print one, <c>(12,5): error: ...</c>, so a file path placed in front of
        /// it makes Unity's Console link to the line.
        /// </summary>
        public override string ToString()
        {
            string severity = Severity == IssueSeverity.Error ? "error" : "warning";
            return $"({Line},{Column}): {severity}: {Message}";
        }
    }
}
