﻿using MatthiWare.CommandLine.Abstractions.Command;
using System;
using System.Collections.Generic;

namespace MatthiWare.CommandLine.Abstractions.Usage
{
    /// <summary>
    /// CLI Usage Output Printer
    /// </summary>
    public interface IUsagePrinter
    {
        /// <summary>
        /// Gets the usage builder <see cref="IUsageBuilder"/>
        /// </summary>
        IUsageBuilder Builder { get; }

        /// <summary>
        /// Print global usage
        /// </summary>
        void PrintUsage();

        /// <summary>
        /// Print an argument
        /// </summary>
        /// <param name="argument">The given argument</param>
        [Obsolete("Use PrintCommandUsage or PrintOptionUsage instead")]
        void PrintUsage(IArgument argument);

        /// <summary>
        /// Print command usage
        /// </summary>
        /// <param name="command">The given command</param>
        [Obsolete("Use PrintCommandUsage instead")]
        void PrintUsage(ICommandLineCommand command);

        /// <summary>
        /// Print command usage
        /// </summary>
        /// <param name="command">The given command</param>
        void PrintCommandUsage(ICommandLineCommand command);

        /// <summary>
        /// Print option usage
        /// </summary>
        /// <param name="option">The given option</param>
        [Obsolete("Use PrintCommandUsage instead")]
        void PrintUsage(ICommandLineOption option);

        /// <summary>
        /// Print option usage
        /// </summary>
        /// <param name="option">The given option</param>
        void PrintOptionUsage(ICommandLineOption option);
        
        /// <summary>
        /// Print errors
        /// </summary>
        /// <param name="errors">list of errors</param>
        void PrintErrors(IReadOnlyCollection<Exception> errors);
    }
}
