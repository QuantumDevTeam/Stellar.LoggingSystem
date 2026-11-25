using System.Text;
using System.Text.RegularExpressions;

using Stellar.Kernel.LoggingSystem;
using Stellar.LoggingSystem.Sink;

namespace Stellar.LoggingSystem.Tests;

public class SinkTests
{
    private static readonly LoggerFormat Plain = Same(new LogFormat("{4}|{10}", "\e[1m{4}|{10}\e[0m"));
    private static readonly LoggerFormat Caller = Same(new LogFormat("{8}.{9}"));

    private readonly StringWriter _fallback = new();

    private static LoggerFormat Same(LogFormat format)
    {
        return new LoggerFormat(format, format, format, format, format, format);
    }

    private FileLogger File(Func<Stream?> open, bool autoFlush = true)
    {
        return new FileLogger(Stub.Engine(), open, autoFlush, Plain, LogLevel.Info, _fallback);
    }

    private static string Text(MemoryStream stream)
    {
        return Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n");
    }

    private static string Line(string format, double seconds = 0)
    {
        return LoggerTextFormatter.Format(new LogFormat(format), false, LogLevel.Info, "{hi}", "File",
            Stub.Engine(seconds));
    }

    // ---- The line

    [Fact]
    public void Formatter_FillsThePlaceholders()
    {
        Line("{2} {3} {4} {5} {6} {7} {10}", 3723.5).Should()
            .Be($"Engine 01:02:03.500 Info Main File {Environment.CurrentManagedThreadId} {{hi}}");
    }

    [Fact]
    public void Formatter_WritesDateAndTime()
    {
        Regex.IsMatch(Line("{0} {1}"), @"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{3}$").Should().BeTrue();
    }

    [Fact]
    public void ACaller_IsFoundThroughTheRouterAndTheExtensions()
    {
        var output = new StringWriter();
        var catalog = new LoggerCatalog();
        catalog.Add(new ConsoleLogger(Stub.Engine(), output, false, Caller));

        new Logger(catalog).Info("m");

        output.ToString().Trim().Should()
            .Be($"{typeof(SinkTests).FullName}.{nameof(ACaller_IsFoundThroughTheRouterAndTheExtensions)}");
    }

    [Fact]
    public async Task ACaller_InAnAsyncMethod_IsTheMethodAsWritten()
    {
        await Task.Yield();
        var output = new StringWriter();
        new ConsoleLogger(Stub.Engine(), output, false, Caller).Info("m");

        output.ToString().Trim().Should()
            .Be($"{typeof(SinkTests).FullName}.{nameof(ACaller_InAnAsyncMethod_IsTheMethodAsWritten)}");
    }

    [Fact]
    public void ACaller_InALambda_IsTheMethodAroundIt()
    {
        var output = new StringWriter();
        var logger = new ConsoleLogger(Stub.Engine(), output, false, Caller);
        Action log = () => logger.Info("m");
        log();

        output.ToString().Trim().Should()
            .Be($"{typeof(SinkTests).FullName}.{nameof(ACaller_InALambda_IsTheMethodAroundIt)}");
    }

    [Fact]
    public void TheDefaultFormats_WorkAtEveryLevel()
    {
        foreach (LogLevel level in Enum.GetValues<LogLevel>())
        foreach (bool colorized in new[] { false, true })
            LoggerTextFormatter
                .Format(LoggerFormat.Default.ForLevel(level), colorized, level, "message", "File", Stub.Engine())
                .Should().Contain("message").And.Contain(level.ToString());
    }

    [Fact]
    public void AnUnknownLevel_HasNoFormat()
    {
        FluentActions.Invoking(() => LoggerFormat.Default.ForLevel((LogLevel)99)).Should().Throw<ArgumentException>();
    }

    // ---- The console

    [Theory]
    [InlineData(null, "Info|m")]
    [InlineData(false, "Info|m")]
    [InlineData(true, "\e[1mInfo|m\e[0m")]
    public void Console_ColorsAnOutputOfItsOwn_OnlyWhenAsked(bool? colorized, string expected)
    {
        var output = new StringWriter();
        new ConsoleLogger(Stub.Engine(), output, colorized, Plain).Info("m");
        output.ToString().Trim().Should().Be(expected);
    }

    [Fact]
    public void AColorizedLine_WithoutAColorizedFormat_IsPlain()
    {
        var output = new StringWriter();
        new ConsoleLogger(Stub.Engine(), output, true, Same(new LogFormat("{10}"))).Info("m");
        output.ToString().Trim().Should().Be("m");
    }

    [Fact]
    public void Console_WritesLinesWhole_FromManyThreads()
    {
        var output = new StringWriter();
        var logger = new ConsoleLogger(Stub.Engine(), output, false, Plain);

        Parallel.For(0, 500, index => logger.Info(index.ToString()));

        output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Should().BeEquivalentTo(Enumerable.Range(0, 500).Select(index => $"Info|{index}"));
    }

    [Fact]
    public void Sinks_NeedAnEngineAndAStream()
    {
        FluentActions.Invoking(() => new ConsoleLogger(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new FileLogger(null!, () => null)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new FileLogger(Stub.Engine(), null!)).Should().Throw<ArgumentNullException>();
    }

    // ---- The file

    [Fact]
    public void File_OpensOnce_AtTheFirstMessageItTakes()
    {
        var stream = new MemoryStream();
        int opened = 0;
        FileLogger file = File(() =>
        {
            opened++;
            return stream;
        });

        file.Debug("below the level");
        opened.Should().Be(0);

        file.Info("a");
        file.Error("b");
        file.LogWithoutFormat("raw");
        file.Stop();

        opened.Should().Be(1);
        Text(stream).Should().Be("Info|a\nError|b\nraw\n");
    }

    [Fact]
    public void File_WithoutAutoFlush_FlushesErrors()
    {
        var stream = new MemoryStream();
        FileLogger file = File(() => stream, false);

        file.Info("a");
        Text(stream).Should().BeEmpty();
        file.Error("b");
        Text(stream).Should().Be("Info|a\nError|b\n");
        file.Dispose();
    }

    [Fact]
    public void File_KeepsTheLines_TillThereIsSomewhereToWrite()
    {
        var stream = new MemoryStream();
        bool ready = false;
        FileLogger file = File(() => ready ? stream : null);

        file.Info("a");
        file.LogWithoutFormat("b");
        ready = true;
        file.Info("c");

        Text(stream).Should().Be("Info|a\nb\nInfo|c\n");
    }

    [Fact]
    public void File_ThatCouldBeOpenedOnlyAtTheEnd_IsWrittenThen()
    {
        var stream = new MemoryStream();
        bool ready = false;
        FileLogger file = File(() => ready ? stream : null);

        file.Info("a");
        ready = true;
        file.Stop();

        Text(stream).Should().Be("Info|a\n");
        _fallback.ToString().Should().BeEmpty();
    }

    [Fact]
    public void File_WithNowhereToWrite_SaysHowMuchWasLost()
    {
        FileLogger file = File(() => null);
        file.Info("a");
        file.Info("b");
        file.Stop();

        _fallback.ToString().Should().Contain("2 message(s) were not written");
    }

    [Fact]
    public void File_KeepsTheLatestLines_AndSaysHowManyItDropped()
    {
        var stream = new MemoryStream();
        bool ready = false;
        FileLogger file = File(() => ready ? stream : null);

        for (int index = 0; index < FileLogger.MaxPending + 3; index++) file.Info(index.ToString());
        ready = true;
        file.Info("last");

        string[] lines = Text(stream).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(FileLogger.MaxPending + 2);
        lines[0].Should().Contain("3 message(s) were dropped");
        lines[1].Should().Be("Info|3");
        lines[^1].Should().Be("Info|last");
    }

    [Fact]
    public void File_ThatFails_IsToldOnce_AndTriesAgainWhenStarted()
    {
        var stream = new MemoryStream();
        bool broken = true;
        FileLogger file = File(() => broken ? throw new IOException("no disk") : stream);

        file.Info("a");
        file.Info("b");
        _fallback.ToString().Split("no disk").Should().HaveCount(2);

        broken = false;
        file.Info("still given up");
        file.Start();
        file.Info("c");

        Text(stream).Should().Be("Info|c\n");
    }

    [Fact]
    public void AStreamThatFailsToWriteAndToClose_BreaksNothing()
    {
        FileLogger file = File(() => new BrokenStream());

        file.Invoking(it => it.Info("a")).Should().NotThrow();
        file.Invoking(it => it.Stop()).Should().NotThrow();
        _fallback.ToString().Should().Contain("The file logger failed");
    }

    [Fact]
    public void File_StartsAgainAfterAStop()
    {
        List<MemoryStream> streams = new();
        FileLogger file = File(() =>
        {
            streams.Add(new MemoryStream());
            return streams[^1];
        });

        file.Info("a");
        file.Stop();
        file.Info("lost");
        file.Start();
        file.Info("b");
        file.Dispose();
        file.Dispose();

        streams.Select(Text).Should().Equal("Info|a\n", "Info|b\n");
    }

    [Fact]
    public void File_WritesUtf8WithoutAMark()
    {
        var stream = new MemoryStream();
        File(() => stream).Info("звезда");
        stream.ToArray().Should().StartWith(Encoding.UTF8.GetBytes("Info|звезда"));
    }

    [Fact]
    public void File_WritesLinesWhole_FromManyThreads()
    {
        var stream = new MemoryStream();
        FileLogger file = File(() => stream);

        Parallel.For(0, 500, index => file.Info(index.ToString()));

        Text(stream).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Should().BeEquivalentTo(Enumerable.Range(0, 500).Select(index => $"Info|{index}"));
    }

    private sealed class BrokenStream
        : MemoryStream
    {
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new IOException("broken");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new IOException("broken");
        }

        public override void Flush()
        {
            throw new IOException("broken");
        }
    }
}
