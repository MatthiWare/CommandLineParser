﻿using MatthiWare.CommandLine.Abstractions;
using MatthiWare.CommandLine.Abstractions.Command;
using MatthiWare.CommandLine.Abstractions.Parsing;
using MatthiWare.CommandLine.Abstractions.Usage;
using MatthiWare.CommandLine.Core.Attributes;
using MatthiWare.CommandLine.Core.Command;
using MatthiWare.CommandLine.Core.Exceptions;
using MatthiWare.CommandLine.Core.Usage;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MatthiWare.CommandLine.Tests.Usage
{
    public class UsagePrinterTests : TestBase
    {
        public UsagePrinterTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

        #region Issue_60

        private class Options_Issue60
        {
            [Name("c", "check")]
            [Description("Reports the amount of duplicates without changing anything")]
            [DefaultValue(true)] // Note defaults to true and not required
            public bool OnlyCheck { get; set; }
        }

        [Theory]
        // https://github.com/MatthiWare/CommandLineParser.Core/issues/60
        [InlineData(new string[] { }, false)]
        [InlineData(new string[] { "-c" }, false)]
        [InlineData(new string[] { "-c", "true" }, false)]
        [InlineData(new string[] { "-c", "false" }, false)]
        public void AllOptionsHaveDefaultValueShouldNotPrintUsages(string[] args, bool called)
        {
            var printerMock = new Mock<IUsagePrinter>();

            Services.AddSingleton(printerMock.Object);

            Services.AddCommandLineParser<Options_Issue60>();
            var parser = ResolveParser<Options_Issue60>();

            parser.Parse(args);

            printerMock.Verify(mock => mock.PrintUsage(), called ? Times.Once() : Times.Never());
        }

        #endregion

        private class UsagePrinterGetsCalledOptions
        {
            [Name("o"), Required]
            public string Option { get; set; }
        }

        private class UsagePrinterCommandOptions
        {
            [Name("x"), Required]
            public string Option { get; set; }
        }

        [Theory]
        [InlineData(new string[] { }, true)]
        [InlineData(new string[] { "-o", "bla" }, false)]
        [InlineData(new string[] { "-xd", "bla" }, true)]
        [InlineData(new string[] { "--", "test" }, true)]
        public void UsagePrintGetsCalledInCorrectCases(string[] args, bool called)
        {
            var printerMock = new Mock<IUsagePrinter>();

            Services.AddSingleton(printerMock.Object);

            var opt = new CommandLineParserOptions { StopParsingAfter = "--" };
            Services.AddCommandLineParser<UsagePrinterGetsCalledOptions>(opt);
            var parser = ResolveParser<UsagePrinterGetsCalledOptions>();

            parser.Parse(args);

            printerMock.Verify(mock => mock.PrintUsage(), called ? Times.Once() : Times.Never());
        }

        [Theory]
        [InlineData(new string[] { "--", "get-al" }, false)]
        [InlineData(new string[] { "--", "get-all" }, false)]
        public async Task PrintUsage_ShouldBeCalled_When_Command_Is_Defined_After_StopParsingFlag(string[] args, bool _)
        {
            var printerMock = new Mock<IUsagePrinter>();

            Services.AddSingleton(printerMock.Object);

            var opt = new CommandLineParserOptions { StopParsingAfter = "--" };
            Services.AddCommandLineParser(opt);
            var parser = ResolveParser();

            parser.AddCommand().Name("get-all");

            await parser.ParseAsync(args);

            printerMock.Verify(mock => mock.PrintUsage(), Times.Once());
            printerMock.Verify(mock => mock.PrintSuggestion(It.IsAny<UnusedArgumentModel>()), Times.Never());
        }

        [Fact]
        public void UsagePrinterPrintsOptionCorrectly()
        {
            var printerMock = new Mock<IUsagePrinter>();

            Services.AddSingleton(printerMock.Object);

            Services.AddCommandLineParser< UsagePrinterGetsCalledOptions>();
            var parser = ResolveParser< UsagePrinterGetsCalledOptions>();

            parser.Parse(new[] { "-o", "--help" });

            printerMock.Verify(mock => mock.PrintOptionUsage(It.IsAny<ICommandLineOption>()), Times.Once());
        }

        [Fact]
        public void UsagePrinterPrintsCommandCorrectly()
        {
            var printerMock = new Mock<IUsagePrinter>();

            Services.AddSingleton(printerMock.Object);

            Services.AddCommandLineParser<UsagePrinterGetsCalledOptions>();
            var parser = ResolveParser<UsagePrinterGetsCalledOptions>();

            parser.AddCommand<UsagePrinterCommandOptions>()
                .Name("cmd")
                .Required();

            parser.Parse(new[] { "-o", "bla", "cmd", "--help" });

            printerMock.Verify(mock => mock.PrintCommandUsage(It.IsAny<ICommandLineCommand>()), Times.Once());
        }

        [Theory]
        [InlineData(new string[] { "-o", "bla", "cmd" }, true, false)]
        [InlineData(new string[] { "-o", "bla", "cmd", "-x", "bla" }, false, false)]
        [InlineData(new string[] { "cmd", "-x", "bla" }, false, true)]
        public void CustomInvokedPrinterWorksCorrectly(string[] args, bool cmdPassed, bool optPassed)
        {
            var builderMock = new Mock<IUsageBuilder>();

            var parserOptions = new CommandLineParserOptions
            {
                AutoPrintUsageAndErrors = false
            };

            Services.AddSingleton(builderMock.Object);

            Services.AddCommandLineParser<UsagePrinterGetsCalledOptions>(parserOptions);
            var parser = ResolveParser<UsagePrinterGetsCalledOptions>();

            parser.AddCommand<UsagePrinterCommandOptions>()
                .Name("cmd")
                .Required();

            var result = parser.Parse(args);

            builderMock.Verify(mock => mock.Build(), Times.Never());
            builderMock.Verify(mock => mock.AddCommand(It.IsAny<string>(), It.IsAny<ICommandLineCommandContainer>()), Times.Never());
            builderMock.Verify(mock => mock.AddOption(It.IsAny<ICommandLineOption>()), Times.Never());

            if (result.HelpRequested)
            {
                parser.Printer.PrintUsage(result.HelpRequestedFor);
            }

            if (result.HasErrors)
            {
                foreach (var err in result.Errors)
                {
                    if (!(err is BaseParserException baseParserException))
                    {
                        continue;
                    }

                    parser.Printer.PrintUsage(baseParserException.Argument);
                }
            }

            builderMock.Verify(
                mock => mock.Build(),
                ToTimes(result.HelpRequested || result.HasErrors));

            builderMock.Verify(
                mock => mock.AddCommand(It.IsAny<string>(), It.IsAny<ICommandLineCommand>()),
                ToTimes(cmdPassed));

            builderMock.Verify(
                mock => mock.AddOption(It.IsAny<ICommandLineOption>()),
                ToTimes(optPassed));
        }

        [Fact]
        public void TestSuggestion()
        {
            // SETUP
            string result = string.Empty;
            var expected = $"'tst' is not recognized as a valid command or option.{Environment.NewLine}{Environment.NewLine}Did you mean: {Environment.NewLine}\tTest{Environment.NewLine}";

            var consoleMock = new Mock<IConsole>();
            consoleMock.Setup(_ => _.WriteLine(It.IsAny<string>())).Callback((string s) => result = s).Verifiable();

            Services.AddSingleton(consoleMock.Object);
            Services.AddSingleton(Logger);

            Services.AddCommandLineParser<OptionModel>();
            var parser = ResolveParser<OptionModel>();

            var cmdConfig = parser.AddCommand<OptionModel>();
            cmdConfig.Name("ZZZZZZZZZZZZZZ").Configure(o => o.Option).Name("tst");

            parser.AddCommand().Name("Test");
            parser.Configure(o => o.Option).Name("Test1");

            var model = new UnusedArgumentModel("tst", (IArgument)parser);
            var printer = parser.Services.GetRequiredService<IUsagePrinter>();

            // ACT
            printer.PrintSuggestion(model);

            // ASSERT
            consoleMock.VerifyAll();
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TestNoSuggestion()
        {
            var usageBuilderMock = new Mock<IUsageBuilder>();
            var suggestionProviderMock = new Mock<ISuggestionProvider>();

            suggestionProviderMock
                .Setup(_ => _.GetSuggestions(It.IsAny<string>(), It.IsAny<ICommandLineCommandContainer>()))
                .Returns(Array.Empty<string>());

            Services.AddSingleton(usageBuilderMock.Object);
            Services.AddSingleton(suggestionProviderMock.Object);
            Services.AddSingleton(Logger);

            Services.AddCommandLineParser<OptionModel>();
            var parser = ResolveParser<OptionModel>();

            // ACT
            parser.Parse(new[] { "tst" }).AssertNoErrors();

            // ASSERT
            usageBuilderMock.Verify(_ => _.AddSuggestion(It.IsAny<string>()), Times.Never());
            usageBuilderMock.Verify(_ => _.AddSuggestionHeader(It.IsAny<string>()), Times.Never());
        }

        [Fact]
        public void TestSuggestionWithParsing()
        {
            // SETUP
            string result = string.Empty;
            var expected = $"'tst' is not recognized as a valid command or option.{Environment.NewLine}{Environment.NewLine}Did you mean: {Environment.NewLine}\tTest{Environment.NewLine}";

            var consoleMock = new Mock<IConsole>();
            consoleMock.Setup(_ => _.WriteLine(It.IsAny<string>())).Callback((string s) => result += s).Verifiable();

            Services.AddSingleton(consoleMock.Object);
            Services.AddSingleton(Logger);

            Services.AddCommandLineParser<OptionModel>();
            var parser = ResolveParser<OptionModel>();

            var cmdConfig = parser.AddCommand<OptionModel>();
            cmdConfig.Name("ZZZZZZZZZZZZZZ").Configure(o => o.Option).Name("tst");

            parser.AddCommand().Name("Test");
            parser.Configure(o => o.Option).Name("Test1");

            // ACT
            parser.Parse(new[] { "tst" }).AssertNoErrors();

            // ASSERT
            consoleMock.VerifyAll();
            Assert.Contains(expected, result);
        }

        private Times ToTimes(bool input)
            => input ? Times.Once() : Times.Never();

        private class OptionModel
        {
            public string Option { get; set; }
        }
    }
}
