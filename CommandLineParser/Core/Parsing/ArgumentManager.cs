﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MatthiWare.CommandLine.Abstractions;
using MatthiWare.CommandLine.Abstractions.Command;
using MatthiWare.CommandLine.Abstractions.Models;
using MatthiWare.CommandLine.Abstractions.Parsing;
using MatthiWare.CommandLine.Core.Command;

namespace MatthiWare.CommandLine.Core.Parsing
{
    internal class ArgumentManager : IArgumentManager, IDisposable
    {
        private readonly IDictionary<IArgument, ArgumentModel> resultCache;
        private readonly List<ArgumentValueHolder> args;

        public ArgumentManager(string[] args, ICollection<CommandLineCommandBase> commands, ICollection<CommandLineOptionBase> options)
        {
            resultCache = new Dictionary<IArgument, ArgumentModel>(commands.Count + options.Count);

            this.args = new List<ArgumentValueHolder>(args.Select(arg => new ArgumentValueHolder
            {
                Argument = arg,
                Used = false
            }));

            ParseCommands(commands);

            ParseOptions(options);

            // pre cache results
            foreach (var item in this.args)
            {
                if (item.ArgModel == null) continue;

                int nextIndex = item.Index + 1;

                var argValue = nextIndex < this.args.Count ? this.args[nextIndex] : null;

                var argModel = new ArgumentModel
                {
                    Key = item.Argument,
                    // this checks if the argument is used in an other command/option. 
                    Value = (argValue?.Used ?? true) ? null : argValue.Argument
                };

                resultCache.Add(item.ArgModel, argModel);
            }
        }

        private void ParseOptions(IEnumerable<ICommandLineOption> options)
        {
            foreach (var option in options)
            {
                int idx = FindIndex(option);

                if (idx == -1) continue; // not found issue #12

                SetArgumentUsed(idx, option);
            }
        }

        private void ParseCommands(IEnumerable<CommandLineCommandBase> cmds)
        {
            foreach (var cmd in cmds)
            {
                int idx = FindIndex(cmd);

                if (idx == -1) continue;

                SetArgumentUsed(idx, cmd);

                foreach (var option in cmd.Options)
                {
                    // find the option index starting at the command index
                    int optionIdx = FindIndex(option, idx);

                    SetArgumentUsed(optionIdx, option);
                }

                ParseCommands(cmd.Commands.Cast<CommandLineCommandBase>());
            }
        }

        private void SetArgumentUsed(int idx, IArgument option)
        {
            args[idx].Used = true;
            args[idx].ArgModel = option;
            args[idx].Index = idx;
        }

        /// <summary>
        /// Finds the index of the first unused argument
        /// </summary>
        /// <param name="args">List of arguments to search</param>
        /// <param name="model">Argument model to find</param>
        /// <param name="startOffset">Search offset</param>
        /// <returns></returns>
        private int FindIndex(IArgument model, int startOffset = 0)
        {
            return args.FindIndex(startOffset, arg =>
                {
                    if (arg.Used) return false;

                    switch (model)
                    {
                        case ICommandLineOption opt:
                            return (opt.HasShortName && string.Equals(opt.ShortName, arg.Argument, StringComparison.InvariantCultureIgnoreCase)) ||
                                    (opt.HasLongName && string.Equals(opt.LongName, arg.Argument, StringComparison.InvariantCultureIgnoreCase));
                        case ICommandLineCommand cmd:
                            return string.Equals(cmd.Name, arg.Argument, StringComparison.InvariantCultureIgnoreCase);
                        default:
                            return false;
                    }


                });
        }



        public void Dispose() => args.Clear();

        public bool TryGetValue(IArgument argument, out ArgumentModel model) => resultCache.TryGetValue(argument, out model);

        [DebuggerDisplay("{Argument}, used: {Used}, index: {Index}")]
        private class ArgumentValueHolder
        {
            public string Argument { get; set; }
            public bool Used { get; set; }
            public IArgument ArgModel { get; set; }
            public int Index { get; set; }
        }
    }
}
