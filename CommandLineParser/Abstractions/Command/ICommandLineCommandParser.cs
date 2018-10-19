﻿using System;
using System.Collections.Generic;
using System.Text;
using MatthiWare.CommandLine.Abstractions.Parsing;
using MatthiWare.CommandLine.Abstractions.Parsing.Command;

namespace MatthiWare.CommandLine.Abstractions.Command
{
    public interface ICommandLineCommandParser
    {
        IReadOnlyList<ICommandLineArgumentOption> Options { get; }
        ICommandParserResult Parse(List<string> args, int startIndex);
    }
}
