﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using MatthiWare.CommandLine.Abstractions;
using MatthiWare.CommandLine.Abstractions.Command;
using MatthiWare.CommandLine.Abstractions.Models;
using MatthiWare.CommandLine.Abstractions.Parsing;
using MatthiWare.CommandLine.Abstractions.Parsing.Command;
using MatthiWare.CommandLine.Core.Exceptions;

namespace MatthiWare.CommandLine.Core.Command
{
    internal class CommandLineCommand<TOption, TCommandOption> :
        CommandLineCommandBase,
        ICommandBuilder<TOption, TCommandOption>
        where TOption : class
        where TCommandOption : class, new()
    {
        private readonly TCommandOption m_commandOption;
        private readonly Func<TOption> m_baseModelResolver;
        private readonly IResolverFactory m_resolverFactory;
        private Action<TOption> m_executor;
        private Action<TOption, TCommandOption> m_executor2;

        public CommandLineCommand(IResolverFactory resolverFactory, Func<TOption> baseModelResolver)
        {
            m_commandOption = new TCommandOption();
            m_resolverFactory = resolverFactory;
            m_baseModelResolver = baseModelResolver;
        }

        public override void Execute()
        {
            m_executor2?.Invoke(m_baseModelResolver(), m_commandOption);
            m_executor?.Invoke(m_baseModelResolver());
        }

        public IOptionBuilder Configure<TProperty>(Expression<Func<TCommandOption, TProperty>> selector)
        {
            var option = new CommandLineOption(m_commandOption, selector, m_resolverFactory);

            m_options.Add(option);

            return option;
        }

        public override ICommandParserResult Parse(IArgumentManager argumentManager)
        {
            var result = new CommandParserResult(this);
            var errors = new List<Exception>();

            foreach (var option in m_options)
            {
                if (!argumentManager.TryGetValue(option, out ArgumentModel model) && option.IsRequired)
                {
                    errors.Add(new OptionNotFoundException(option));

                    continue;
                }
                else if (!model.HasValue && option.HasDefault)
                {
                    option.UseDefault();

                    continue;
                }
                else if (!option.CanParse(model))
                {
                    errors.Add(new OptionParseException(option, model));

                    continue;
                }

                option.Parse(model);
            }

            result.MergeResult(errors);

            return result;
        }

        public ICommandBuilder<TOption, TCommandOption> Required(bool required = true)
        {
            IsRequired = required;

            return this;
        }

        ICommandBuilder<TOption, TCommandOption> ICommandBuilder<TOption, TCommandOption>.HelpText(string help)
        {
            HelpText = help;

            return this;
        }

        public ICommandBuilder<TOption, TCommandOption> Name(string shortName)
        {
            ShortName = shortName;

            return this;
        }

        public ICommandBuilder<TOption, TCommandOption> Name(string shortName, string longName)
        {
            ShortName = shortName;
            LongName = longName;

            return this;
        }

        public ICommandBuilder<TOption, TCommandOption> OnExecuting(Action<TOption> action)
        {
            m_executor = action;

            return this;
        }

        public ICommandBuilder<TOption, TCommandOption> OnExecuting(Action<TOption, TCommandOption> action)
        {
            m_executor2 = action;

            return this;
        }
    }
}