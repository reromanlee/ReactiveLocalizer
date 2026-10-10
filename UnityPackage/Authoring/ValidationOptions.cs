namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>Which optional checks a validation runs.</summary>
    public sealed class ValidationOptions
    {
        /// <summary>Every check but the optional ones.</summary>
        public static readonly ValidationOptions Default = new();

        /// <summary>
        /// Whether table and entry names are checked against the PascalCase convention without underscores, such as
        /// <c>MainMenu.PlayButton</c>. Off by default; the naming rule itself is always checked.
        /// </summary>
        public bool IsCheckingPascalCase { get; set; }

        /// <summary>
        /// Whether former names no reference uses are reported. On by default; a check that sees only some of the
        /// project's references, as a build does, turns it off.
        /// </summary>
        public bool IsCheckingUnusedAliases { get; set; } = true;
    }
}
