namespace reromanlee.ReactiveLocalizer.Hosting
{
    /// <summary>A problem a localizer reports to its host, which shows it wherever its platform shows problems.</summary>
    public readonly struct LocalizerReport
    {
        /// <summary>Creates a report, optionally about <paramref name="target"/>, such as the object a binding writes to.</summary>
        public LocalizerReport(ReportSeverity severity, string message, object target)
        {
            Severity = severity;
            Message = message;
            Target = target;
        }

        /// <summary>Whether something can't show its text because of the problem.</summary>
        public ReportSeverity Severity { get; }

        /// <summary>What happened, written for the person who fixes it.</summary>
        public string Message { get; }

        /// <summary>The object the report is about, such as a binding's target, or null. A host can point at it.</summary>
        public object Target { get; }

        /// <inheritdoc/>
        public override string ToString() => Message;
    }
}
