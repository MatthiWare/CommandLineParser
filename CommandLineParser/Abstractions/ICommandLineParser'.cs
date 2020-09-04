﻿using MatthiWare.CommandLine.Abstractions.Command;
using MatthiWare.CommandLine.Abstractions.Parsing;
using MatthiWare.CommandLine.Abstractions.Usage;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MatthiWare.CommandLine.Abstractions
{
    /// <summary>
    /// Command line parser
    /// </summary>
    /// <typeparam name="TOption">Argument options model</typeparam>
    public interface ICommandLineParser<TOption>
        where TOption : class, new()
    {
        #region Properties

        /// <summary>
        /// Tool to print usage info.
        /// </summary>
        IUsagePrinter Printer { get; set; }

        /// <summary>
        /// Resolver that is used to instantiate types by an given container
        /// </summary>
        IServiceProvider Services { get; }

        #endregion

        #region Parsing

        /// <summary>
        /// Parses the arguments
        /// </summary>
        /// <param name="args">CLI Arguments</param>
        /// <returns>The <see cref="IParserResult{TOption}"/> result</returns>
        IParserResult<TOption> Parse(string[] args);

        /// <summary>
        /// Parses the arguments async
        /// </summary>
        /// <param name="args">CLI Arguments</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The task that resolves to the result. <see cref="IParserResult{TOption}"/></returns>
        Task<IParserResult<TOption>> ParseAsync(string[] args, CancellationToken cancellationToken);

        #endregion

        #region Configuration

        /// <summary>
        /// Discovers commands and registers them from any given assembly
        /// </summary>
        /// <param name="assembly">Assembly containing the command types</param>
        void DiscoverCommands(Assembly assembly);

        /// <summary>
        /// Discovers commands and registers them from any given assembly
        /// </summary>
        /// <param name="assemblies">Assemblies containing the command types</param>
        void DiscoverCommands(Assembly[] assemblies);

        /// <summary>
        /// Configures a new or existing option
        /// </summary>
        /// <typeparam name="TProperty">The property type</typeparam>
        /// <param name="selector">Property selector</param>
        /// <returns><see cref="IOptionBuilder"/></returns>
        IOptionBuilder<TProperty> Configure<TProperty>(Expression<Func<TOption, TProperty>> selector);

        /// <summary>
        /// Adds a new command  and allowes to configure it. 
        /// </summary>
        /// <typeparam name="TCommandOption">Command options model</typeparam>
        /// <returns>A command builder, <see cref="ICommandBuilder{TOption, TSource}"/> for more info.</returns>
        ICommandBuilder<TOption, TCommandOption> AddCommand<TCommandOption>()
            where TCommandOption : class, new();

        /// <summary>
        /// Adds a new command and allowes to configure it. 
        /// </summary>
        /// <returns>A command builder, see <see cref="ICommandBuilder{TOption}"/> for more info.</returns>
        ICommandBuilder<TOption> AddCommand();

        /// <summary>
        /// Registers a new command
        /// </summary>
        /// <typeparam name="TCommand">The command</typeparam>
        void RegisterCommand<TCommand>()
            where TCommand : Command<TOption>;

        /// <summary>
        /// Registers a new command
        /// </summary>
        /// <typeparam name="TCommand">The command</typeparam>
        /// <typeparam name="TCommandOption">Command options model</typeparam>
        void RegisterCommand<TCommand, TCommandOption>()
            where TCommand : Command<TOption, TCommandOption>
            where TCommandOption : class, new();

        /// <summary>
        /// Registers a new command
        /// </summary>
        /// <param name="commandType">The type of the command</param>
        /// <param name="optionsType">Command options model</param>
        void RegisterCommand(Type commandType, Type optionsType);

        /// <summary>
        /// Registers a new command
        /// </summary>
        /// <param name="commandType">The type of the command</param>
        void RegisterCommand(Type commandType);

        #endregion
    }
}
