﻿using MatthiWare.CommandLine.Abstractions;
using MatthiWare.CommandLine.Abstractions.Command;
using MatthiWare.CommandLine.Abstractions.Models;
using MatthiWare.CommandLine.Abstractions.Parsing;
using MatthiWare.CommandLine.Abstractions.Parsing.Command;
using MatthiWare.CommandLine.Abstractions.Usage;
using MatthiWare.CommandLine.Abstractions.Validations;
using MatthiWare.CommandLine.Core;
using MatthiWare.CommandLine.Core.Attributes;
using MatthiWare.CommandLine.Core.Command;
using MatthiWare.CommandLine.Core.Exceptions;
using MatthiWare.CommandLine.Core.Parsing;
using MatthiWare.CommandLine.Core.Parsing.Command;
using MatthiWare.CommandLine.Core.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("CommandLineParser.Tests")]

namespace MatthiWare.CommandLine
{
    /// <summary>
    /// Command line parser
    /// </summary>
    /// <typeparam name="TOption">Options model</typeparam>
    public class CommandLineParser<TOption> : ICommandLineParser<TOption>, ICommandLineCommandContainer, IArgument
        where TOption : class, new()
    {
        private readonly TOption m_option;
        private readonly Dictionary<string, CommandLineOptionBase> m_options = new Dictionary<string, CommandLineOptionBase>();
        private readonly List<CommandLineCommandBase> m_commands = new List<CommandLineCommandBase>();
        private readonly string m_helpOptionName;
        private readonly string m_helpOptionNameLong;
        private readonly ILogger<CommandLineParser> logger;

        /// <summary>
        /// <see cref="CommandLineParserOptions"/> this parser is currently using. 
        /// NOTE: In order to use the options they need to be passed using the constructor. 
        /// </summary>
        public CommandLineParserOptions ParserOptions { get; }

        /// <inheritdoc/>
        public IUsagePrinter Printer => Services.GetRequiredService<IUsagePrinter>();

        /// <summary>
        /// Read-only collection of options specified
        /// </summary>
        public IReadOnlyList<ICommandLineOption> Options => new ReadOnlyCollectionWrapper<string, CommandLineOptionBase>(m_options.Values);

        /// <inheritdoc/>
        public IServiceProvider Services { get; }

        /// <summary>
        /// Read-only list of commands specified
        /// </summary>
        public IReadOnlyList<ICommandLineCommand> Commands => m_commands.AsReadOnly();

        /// <summary>
        /// Container for all validators
        /// </summary>
        public IValidatorsContainer Validators => Services.GetRequiredService<IValidatorsContainer>();

        /// <summary>
        /// Creates a new instance of the commandline parser
        /// </summary>
        public CommandLineParser()
            : this(new CommandLineParserOptions(), null)
        { }

        /// <summary>
        /// Creates a new instance of the commandline parser
        /// </summary>
        /// <param name="parserOptions">The parser options</param>
        public CommandLineParser(CommandLineParserOptions parserOptions)
            : this(parserOptions, null)
        { }

        /// <summary>
        /// Creates a new instance of the commandline parser
        /// </summary>
        /// <param name="servicesCollection">container resolver to use</param>
        public CommandLineParser(IServiceCollection servicesCollection)
            : this(new CommandLineParserOptions(), servicesCollection)
        { }

        /// <summary>
        /// Creates a new instance of the commandline parser
        /// </summary>
        /// <param name="servicesCollection">container resolver to use</param>
        /// <param name="parserOptions">The options the parser will use</param>
        public CommandLineParser(CommandLineParserOptions parserOptions, IServiceCollection servicesCollection)
        {
            ParserOptions = UpdateOptionsIfNeeded(parserOptions);

            var services = servicesCollection ?? new ServiceCollection();

            services.AddInternalCommandLineParserServices(this, ParserOptions);

            Services = services.BuildServiceProvider();

            logger = Services.GetRequiredService<ILogger<CommandLineParser>>();

            m_option = new TOption();

            (m_helpOptionName, m_helpOptionNameLong) = parserOptions.GetConfiguredHelpOption();

            InitialzeModel();
        }

        private CommandLineParserOptions UpdateOptionsIfNeeded(CommandLineParserOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.AppName))
            {
                return options;
            }

            options.AppName = Process.GetCurrentProcess().ProcessName;

            return options;
        }

        /// <summary>
        /// Configures an option in the model
        /// </summary>
        /// <typeparam name="TProperty">Type of the property</typeparam>
        /// <param name="selector">Model property to configure</param>
        /// <returns><see cref="IOptionBuilder"/></returns>
        public IOptionBuilder<TProperty> Configure<TProperty>(Expression<Func<TOption, TProperty>> selector)
        {
            var memberInfo = ((MemberExpression)selector.Body).Member;
            var key = $"{memberInfo.DeclaringType.FullName}.{memberInfo.Name}";

            return ConfigureInternal<TProperty>(selector, key);
        }

        private IOptionBuilder<T> ConfigureInternal<T>(LambdaExpression selector, string key)
        {
            if (!m_options.ContainsKey(key))
            {
                var option = ActivatorUtilities.CreateInstance<CommandLineOption<T>>(Services, m_option, selector);

                logger.LogDebug("Add option builder for {Expression}", key);

                m_options.Add(key, option);
            }

            return m_options[key] as IOptionBuilder<T>;
        }

        /// <summary>
        /// Parses the commandline arguments
        /// </summary>
        /// <param name="args">arguments from the commandline</param>
        /// <returns>The result of the parsing, <see cref="IParserResult{TResult}"/></returns>
        public IParserResult<TOption> Parse(string[] args)
        {
            var errors = new List<Exception>();

            var result = new ParseResult<TOption>();

            var argumentManager = new ArgumentManager(args, ParserOptions, m_helpOptionName, m_helpOptionNameLong, m_commands, m_options.Values.Cast<ICommandLineOption>().ToArray());

            ParseCommands(errors, result, argumentManager);

            ParseOptions(errors, result, argumentManager);

            CheckForExtraHelpArguments(result, argumentManager);

            Validate(m_option, errors);

            result.MergeResult(errors);

            AutoExecuteCommands(result);

            AutoPrintUsageAndErrors(result, args.Length == 0);

            return result;
        }

        /// <summary>
        /// Parses the commandline arguments async
        /// </summary>
        /// <param name="args">arguments from the commandline</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The result of the parsing, <see cref="IParserResult{TResult}"/></returns>
        public async Task<IParserResult<TOption>> ParseAsync(string[] args, CancellationToken cancellationToken = default)
        {
            var errors = new List<Exception>();

            var result = new ParseResult<TOption>();

            var argumentManager = new ArgumentManager(args, ParserOptions, m_helpOptionName, m_helpOptionNameLong, m_commands, m_options.Values.Cast<ICommandLineOption>().ToArray());

            await ParseCommandsAsync(errors, result, argumentManager, cancellationToken);

            ParseOptions(errors, result, argumentManager);

            CheckForExtraHelpArguments(result, argumentManager);

            await ValidateAsync(m_option, errors, cancellationToken);

            result.MergeResult(errors);

            await AutoExecuteCommandsAsync(result, cancellationToken);

            AutoPrintUsageAndErrors(result, args.Length == 0);

            return result;
        }

        private void Validate<T>(T @object, List<Exception> errors)
        {
            if (!Validators.HasValidatorFor<T>())
            {
                return;
            }

            var results = Validators.GetValidators<T>().Select(validator => validator.Validate(@object)).ToArray();

            foreach (var result in results)
            {
                if (result.IsValid)
                {
                    continue;
                }

                errors.Add(result.Error);
            }
        }

        private async Task ValidateAsync<T>(T @object, List<Exception> errors, CancellationToken token)
        {
            if (!Validators.HasValidatorFor<T>())
            {
                return;
            }

            var results = (await Task.WhenAll(Validators.GetValidators<T>()
                .Select(async validator => await validator.ValidateAsync(@object, token)))).ToArray();

            foreach (var result in results)
            {
                if (result.IsValid)
                {
                    continue;
                }

                errors.Add(result.Error);
            }
        }

        private void CheckForExtraHelpArguments(ParseResult<TOption> result, ArgumentManager argumentManager)
        {
            var unusedArg = argumentManager.UnusedArguments
                .Where(a => string.Equals(a.Argument, m_helpOptionName, StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals(a.Argument, m_helpOptionNameLong, StringComparison.InvariantCultureIgnoreCase))
                .FirstOrDefault();

            if (unusedArg == null)
            {
                return;
            }

            result.HelpRequestedFor = unusedArg.ArgModel ?? this;
        }

        private void AutoPrintUsageAndErrors(ParseResult<TOption> result, bool noArgsSupplied)
        {
            if (!ParserOptions.AutoPrintUsageAndErrors)
            {
                return;
            }

            if (noArgsSupplied && (Options.Any(opt => !opt.HasDefault) || Commands.Any(cmd => cmd.IsRequired)))
            {
                PrintHelp();
            }
            else if (result.HelpRequested)
            {
                PrintHelpRequestedForArgument(result.HelpRequestedFor);
            }
            else if (result.HasErrors)
            {
                PrintErrors(result.Errors);
            }
        }

        private void PrintHelpRequestedForArgument(IArgument argument)
        {
            switch (argument)
            {
                case ICommandLineCommand cmd:
                    Printer.PrintCommandUsage(cmd);
                    break;
                case ICommandLineOption opt:
                    Printer.PrintOptionUsage(opt);
                    break;
                default:
                    PrintHelp();
                    break;
            }
        }

        private void PrintErrors(IReadOnlyCollection<Exception> errors)
        {
            Printer.PrintErrors(errors);
            Printer.PrintUsage();
        }

        private void PrintHelp() => Printer.PrintUsage();

        private void AutoExecuteCommands(ParseResult<TOption> result)
        {
            if (result.HasErrors)
            {
                return;
            }

            ExecuteCommandParserResults(result, result.CommandResults.Where(sub => sub.Command.AutoExecute));
        }

        private async Task AutoExecuteCommandsAsync(ParseResult<TOption> result, CancellationToken cancellationToken)
        {
            if (result.HasErrors)
            {
                return;
            }

            await ExecuteCommandParserResultsAsync(result, result.CommandResults.Where(sub => sub.Command.AutoExecute), cancellationToken);
        }

        private bool HelpRequested(ParseResult<TOption> result, CommandLineOptionBase option, ArgumentModel model)
        {
            if (!ParserOptions.EnableHelpOption)
            {
                return false;
            }

            if (model.HasValue &&
              (model.Value.Equals(m_helpOptionName, StringComparison.InvariantCultureIgnoreCase) ||
              model.Value.Equals(m_helpOptionNameLong, StringComparison.InvariantCultureIgnoreCase)))
            {
                result.HelpRequestedFor = option;

                return true;
            }

            return false;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Commands can throw all sorts of exceptions when executing")]
        private void ExecuteCommandParserResults(ParseResult<TOption> results, IEnumerable<ICommandParserResult> cmds)
        {
            var errors = new List<Exception>();

            foreach (var cmd in cmds)
            {
                try
                {
                    cmd.ExecuteCommand();
                }
                catch (Exception ex)
                {
                    errors.Add(new CommandExecutionFailedException(cmd.Command, ex));
                }
            }

            if (errors.Any())
            {
                results.MergeResult(errors);
            }

            foreach (var cmd in cmds)
            {
                ExecuteCommandParserResults(results, cmd.SubCommands.Where(sub => sub.Command.AutoExecute));
            }
        }

        private async Task ExecuteCommandParserResultsAsync(ParseResult<TOption> results, IEnumerable<ICommandParserResult> cmds, CancellationToken cancellationToken)
        {
            var errors = new List<Exception>();

            foreach (var cmd in cmds)
            {
                try
                {
                    await cmd.ExecuteCommandAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    errors.Add(new CommandExecutionFailedException(cmd.Command, ex));
                }
            }

            if (errors.Any())
            {
                results.MergeResult(errors);
            }

            foreach (var cmd in cmds)
            {
                await ExecuteCommandParserResultsAsync(results, cmd.SubCommands.Where(sub => sub.Command.AutoExecute), cancellationToken);
            }
        }

        private void ParseCommands(IList<Exception> errors, ParseResult<TOption> result, IArgumentManager argumentManager)
        {
            foreach (var cmd in m_commands)
            {
                try
                {
                    ParseCommand(cmd, result, argumentManager);

                    if (result.HelpRequested)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }
        }

        private void ParseCommand(CommandLineCommandBase cmd, ParseResult<TOption> result, IArgumentManager argumentManager)
        {
            if (!argumentManager.TryGetValue(cmd, out _))
            {
                result.MergeResult(new CommandNotFoundParserResult(cmd));

                if (cmd.IsRequired)
                {
                    throw new CommandNotFoundException(cmd);
                }

                return;
            }

            var cmdParseResult = cmd.Parse(argumentManager);

            result.MergeResult(cmdParseResult);

            if (cmdParseResult.HasErrors)
            {
                throw new CommandParseException(cmd, cmdParseResult.Errors);
            }
        }

        private async Task ParseCommandsAsync(IList<Exception> errors, ParseResult<TOption> result, IArgumentManager argumentManager, CancellationToken cancellationToken)
        {
            foreach (var cmd in m_commands)
            {
                try
                {
                    await ParseCommandAsync(cmd, result, argumentManager, cancellationToken);

                    if (result.HelpRequested)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }
        }

        private async Task ParseCommandAsync(CommandLineCommandBase cmd, ParseResult<TOption> result, IArgumentManager argumentManager, CancellationToken cancellationToken)
        {
            if (!argumentManager.TryGetValue(cmd, out _))
            {
                result.MergeResult(new CommandNotFoundParserResult(cmd));

                if (cmd.IsRequired)
                {
                    throw new CommandNotFoundException(cmd);
                }

                return;
            }

            var cmdParseResult = await cmd.ParseAsync(argumentManager, cancellationToken);

            result.MergeResult(cmdParseResult);

            if (cmdParseResult.HasErrors)
            {
                throw new CommandParseException(cmd, cmdParseResult.Errors);
            }
        }

        private void ParseOptions(IList<Exception> errors, ParseResult<TOption> result, IArgumentManager argumentManager)
        {
            foreach (var o in m_options)
            {
                try
                {
                    if (ParseOption(o.Value, result, argumentManager))
                    {
                        break; // break here because help is requested!
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }

            result.MergeResult(m_option);
        }

        private bool ParseOption(CommandLineOptionBase option, ParseResult<TOption> result, IArgumentManager argumentManager)
        {
            bool found = argumentManager.TryGetValue(option, out ArgumentModel model);

            if (found && HelpRequested(result, option, model))
            {
                return true;
            }
            else if (!found && option.IsRequired && !option.HasDefault)
            {
                throw new OptionNotFoundException(option);
            }
            else if ((!found && !model.HasValue && option.HasDefault) ||
                (found && !option.CanParse(model) && option.HasDefault))
            {
                option.UseDefault();

                return false;
            }
            else if (found && !option.CanParse(model))
            {
                throw new OptionParseException(option, model);
            }

            option.Parse(model);

            return false;
        }

        /// <summary>
        /// Adds a command to the parser
        /// </summary>
        /// <typeparam name="TCommandOption">Options model for the command</typeparam>
        /// <returns>Builder for the command, <see cref="ICommandBuilder{TOption,TCommandOption}"/></returns>
        public ICommandBuilder<TOption, TCommandOption> AddCommand<TCommandOption>() where TCommandOption : class, new()
        {
            var command = ActivatorUtilities.CreateInstance<CommandLineCommand<TOption, TCommandOption>>(Services, m_option);

            m_commands.Add(command);

            return command;
        }

        /// <summary>
        /// Registers a command type
        /// </summary>
        public void RegisterCommand<TCommand>()
            where TCommand : Command
        {
            var command = ActivatorUtilities.CreateInstance<CommandLineCommand<TOption, object>>(Services, m_option);

            if (typeof(TCommand).IsAssignableToGenericType(typeof(Command<>)))
            {
                RegisterGenericCommandInternal<TCommand>(command);
            }
            else
            {
                RegisterNonGenericCommandInternal<TCommand>(command);
            }

            m_commands.Add(command);
        }

        private void RegisterGenericCommandInternal<TCommand>(CommandLineCommand<TOption, object> command) 
            where TCommand : Command
        {
            var cmdConfigurator = (Command<TOption>)(Command)(ActivatorUtilities.GetServiceOrCreateInstance<TCommand>(Services));

            cmdConfigurator.OnConfigure(command);

            command.OnExecuting((Action<TOption>)cmdConfigurator.OnExecute);
            command.OnExecutingAsync((Func<TOption, CancellationToken, Task>)cmdConfigurator.OnExecuteAsync);
        }

        private void RegisterNonGenericCommandInternal<TCommand>(CommandLineCommand<TOption, object> command)
            where TCommand : Command
        {
            var cmdConfigurator = ActivatorUtilities.GetServiceOrCreateInstance<TCommand>(Services);

            cmdConfigurator.OnConfigure(command);

            command.OnExecuting(cmdConfigurator.OnExecute);
            command.OnExecutingAsync(cmdConfigurator.OnExecuteAsync);
        }

        /// <summary>
        /// Registers a new command
        /// </summary>
        /// <param name="commandType">The type of the command</param>
        public void RegisterCommand(Type commandType) => RegisterCommand(commandType, null);

        /// <summary>
        /// Registers a command type
        /// </summary>
        /// <typeparam name="TCommand">Command type, must be inherit <see cref="Command{TOptions,TCommandOption}"/></typeparam>
        /// <typeparam name="TCommandOption">The command options</typeparam>
        public void RegisterCommand<TCommand, TCommandOption>()
           where TCommand : Command<TOption, TCommandOption>
           where TCommandOption : class, new()
        {
            var cmdConfigurator = ActivatorUtilities.GetServiceOrCreateInstance<TCommand>(Services);

            var command = ActivatorUtilities.CreateInstance<CommandLineCommand<TOption, TCommandOption>>(Services, m_option);

            cmdConfigurator.OnConfigure((ICommandConfigurationBuilder<TCommandOption>)command);

            command.OnExecuting((Action<TOption, TCommandOption>)cmdConfigurator.OnExecute);
            command.OnExecutingAsync((Func<TOption, TCommandOption, CancellationToken, Task>)cmdConfigurator.OnExecuteAsync);

            m_commands.Add(command);
        }

        /// <summary>
        /// Registers a new command
        /// </summary>
        /// <param name="commandType">The type of the command</param>
        /// <param name="optionsType">Command options model</param>
        public void RegisterCommand(Type commandType, Type optionsType)
        {
            bool isAssignableToGenericCommand = commandType.IsAssignableToGenericType(typeof(Command<>));
            bool isAssignableToCommand = typeof(Command).IsAssignableFrom(commandType);

            if (!isAssignableToCommand && !isAssignableToGenericCommand)
            {
                throw new ArgumentException($"Provided command {commandType} is not assignable to {typeof(Command<>)}");
            }
            else if (!isAssignableToCommand)
            {
                throw new ArgumentException($"Provided command {commandType} is not assignable to {typeof(Command)}");
            }

            this.ExecuteGenericRegisterCommand(nameof(RegisterCommand), commandType, optionsType);
        }

        /// <summary>
        /// Adds a command to the parser
        /// </summary>
        /// <returns>Builder for the command, <see cref="ICommandBuilder{TOption}"/></returns>
        public ICommandBuilder<TOption> AddCommand()
        {
            var command = ActivatorUtilities.CreateInstance<CommandLineCommand<TOption, object>>(Services, m_option);

            m_commands.Add(command);

            return command;
        }

        /// <summary>
        /// Initializes the model class with the attributes specified.
        /// </summary>
        private void InitialzeModel()
        {
            var modelInitializer = Services.GetRequiredService<IModelInitializer>();

            modelInitializer.InitializeModel(typeof(TOption), this, nameof(ConfigureInternal), nameof(RegisterCommand));
        }

        /// <summary>
        /// Discovers commands and registers them from any given assembly
        /// </summary>
        /// <param name="assembly">Assembly containing the command types</param>
        public void DiscoverCommands(Assembly assembly) => DiscoverCommands(new[] { assembly });

        /// <summary>
        /// Discovers commands and registers them from any given assembly
        /// </summary>
        /// <param name="assemblies">Assemblies containing the command types</param>
        public void DiscoverCommands(Assembly[] assemblies)
        {
            var commandDiscoverer = Services.GetRequiredService<ICommandDiscoverer>();

            var commandTypes = commandDiscoverer.DiscoverCommandTypes(typeof(TOption), assemblies);

            foreach (var commandType in commandTypes)
            {
                this.ExecuteGenericRegisterCommand(nameof(RegisterCommand), commandType);
            }
        }
    }
}
