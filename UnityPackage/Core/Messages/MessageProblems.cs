using System;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>
    /// What went wrong while rendering one message, one bit per argument of the message, so the localizer can report
    /// each problem once without the renderer allocating anything.
    /// </summary>
    internal struct MessageProblems
    {
        /// <summary>Arguments the message needed but wasn't given; each shows as <c>{name}</c>.</summary>
        public uint MissingArguments;

        /// <summary>Arguments given a value of the wrong kind, such as text for a plural.</summary>
        public uint MistypedArguments;

        /// <summary>Arguments whose type has no registered formatter.</summary>
        public uint UnformattedArguments;

        /// <summary>Where the type of the first argument without a formatter starts in the table's characters.</summary>
        public int UnformattedTypeStart;

        /// <summary>How long the type of the first argument without a formatter is.</summary>
        public int UnformattedTypeLength;

        /// <summary>Arguments whose formatter threw or kept asking for more room.</summary>
        public uint FailedArguments;

        /// <summary>The first exception a formatter threw, if any.</summary>
        public Exception FormatterException;

        public bool HasAny => (MissingArguments | MistypedArguments | UnformattedArguments | FailedArguments) != 0;
    }
}
