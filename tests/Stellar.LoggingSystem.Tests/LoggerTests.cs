using Stellar.Kernel;
using Stellar.Kernel.LoggingSystem;
using Stellar.Kernel.Quantization;

namespace Stellar.LoggingSystem.Tests;

public class LoggerTests
{
    private readonly StringWriter _fallback = new();
    private readonly LoggerCatalog _catalog = new();

    private Logger Logger(params RecordingLogger[] sinks)
    {
        foreach (RecordingLogger sink in sinks) _catalog.Add(sink).Should().BeTrue();
        return new Logger(_catalog, _fallback);
    }

    [Fact]
    public void EveryLogger_HasAnIdentifierOfItsOwn_AndALabelWithIt()
    {
        var a = new RecordingLogger("A");
        var b = new RecordingLogger("B");

        a.UID.Should().NotBe(b.UID);
        a.Label.Name.Should().Be("A");
        a.Label.UID.Should().Be(a.UID);
        a.FindMeta<LoggerMeta>().Should().BeSameAs(a.Meta);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AName_IsNeeded(string name)
    {
        FluentActions.Invoking(() => new RecordingLogger(name)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Format_CanBeReplaced()
    {
        var sink = new RecordingLogger("A");
        sink.Format.Should().Be(LoggerFormat.Default);

        LoggerFormat format = LoggerFormat.Default with { Info = new LogFormat("{10}") };
        sink.Format = format;

        sink.Format.Should().Be(format);
    }

    [Fact]
    public void Format_IsNeverSeenHalfWritten()
    {
        var sink = new RecordingLogger("A");
        int torn = 0;

        Parallel.For(0, 20_000, index =>
        {
            if (index % 2 == 0)
            {
                var one = new LogFormat(index.ToString());
                sink.Format = new LoggerFormat(one, one, one, one, one, one);
                return;
            }

            LoggerFormat format = sink.Format;
            if (format != LoggerFormat.Default && format.Info != format.Exception) Interlocked.Increment(ref torn);
        });

        torn.Should().Be(0);
    }

    [Fact]
    public void MinimumLevel_TakesTheLevelAndEveryOneMoreSerious()
    {
        var sink = new RecordingLogger("A", LogLevel.Success);

        foreach (LogLevel level in Enum.GetValues<LogLevel>()) sink.Log(level, "m");
        sink.Lines.Should().Equal("Success:m", "Warning:m", "Error:m", "Exception:m");

        sink.MinimumLevel = LogLevel.Exception;
        sink.Log(LogLevel.Error, "lost");
        sink.Lines.Should().HaveCount(4);
    }

    [Fact]
    public void AMessageWithoutFormat_IsTurnedAwayByNoLevel()
    {
        var sink = new RecordingLogger("A", LogLevel.Exception);
        sink.LogWithoutFormat("raw");
        sink.Lines.Should().Equal(":raw");
    }

    [Fact]
    public void NoMessage_IsIgnored()
    {
        var sink = new RecordingLogger("A");
        sink.Log(LogLevel.Error, null!);
        sink.LogWithoutFormat(null!);
        sink.Lines.Should().BeEmpty();
    }

    [Fact]
    public void AStoppedLogger_TakesNothing_TillItStarts()
    {
        var sink = new RecordingLogger("A");
        sink.Stop();
        sink.IsActive.Should().BeFalse();
        sink.Log(LogLevel.Error, "lost");
        sink.LogWithoutFormat("lost");
        sink.Start();
        sink.Log(LogLevel.Error, "kept");

        sink.Lines.Should().Equal("Error:kept");
    }

    [Fact]
    public void Logger_GivesAMessageToEverySinkThatTakesItsLevel()
    {
        var all = new RecordingLogger("All");
        var serious = new RecordingLogger("Serious", LogLevel.Warning);
        Logger logger = Logger(all, serious);

        logger.Log(LogLevel.Debug, "d");
        logger.Log(LogLevel.Error, "e");
        logger.LogWithoutFormat("raw");

        all.Lines.Should().Equal("Debug:d", "Error:e", ":raw");
        serious.Lines.Should().Equal("Error:e", ":raw");
    }

    [Fact]
    public void Extensions_LogAtTheirLevels()
    {
        var sink = new RecordingLogger("A");
        Logger logger = Logger(sink);

        logger.Debug("m");
        logger.Info("m");
        logger.Success("m");
        logger.Warning("m");
        logger.Error("m");
        logger.Separator();
        logger.Exception("doing", new InvalidOperationException("why"));

        sink.Lines[..6].Should().Equal("Debug:m", "Info:m", "Success:m", "Warning:m", "Error:m", ":");
        sink.Lines[6].Should().StartWith("Exception:doing").And.Contain("InvalidOperationException").And.Contain("why");
    }

    [Fact]
    public void Logger_SurvivesABrokenSink()
    {
        var healthy = new RecordingLogger("Healthy");
        Logger(new RecordingLogger("Broken", fails: true), healthy).Log(LogLevel.Info, "m");

        healthy.Lines.Should().Equal("Info:m");
        _fallback.ToString().Should().Contain("broken");
    }

    [Fact]
    public void Logger_SurvivesABrokenFallback()
    {
        _catalog.Add(new RecordingLogger("Broken", fails: true));
        var closed = new StreamWriter(new MemoryStream());
        closed.Dispose();

        new Logger(_catalog, closed).Invoking(logger => logger.Log(LogLevel.Info, "m")).Should().NotThrow();
    }

    [Fact]
    public void Logger_WithoutSinks_TellsOnlyWhatIsSerious()
    {
        Logger logger = Logger();
        logger.Info("quiet");
        logger.Warning("loud");

        _fallback.ToString().Should().Contain("loud").And.NotContain("quiet");
    }

    [Fact]
    public void ASinkThatLogs_DoesNotComeBackToItself()
    {
        Logger? logger = null;
        var sink = new RecordingLogger("Talkative") { OnWrite = () => logger!.Error("again") };
        logger = Logger(sink);

        logger.Info("m");

        sink.Lines.Should().Equal("Info:m");
        _fallback.ToString().Should().Contain("reentrant").And.Contain("again");

        // And the next message goes through as usual
        logger.Info("n");
        sink.Lines.Should().Equal("Info:m", "Info:n");
    }

    [Fact]
    public void TwoEngines_LogInsideOneAnother()
    {
        var inner = new RecordingLogger("Inner");
        var innerCatalog = new LoggerCatalog();
        innerCatalog.Add(inner);
        var innerLogger = new Logger(innerCatalog, _fallback);

        Logger(new RecordingLogger("Outer") { OnWrite = () => innerLogger.Info("from outer") }).Info("m");

        inner.Lines.Should().Equal("Info:from outer");
    }

    [Fact]
    public void Logger_TakesMessagesFromManyThreads()
    {
        var sink = new RecordingLogger("A");
        Logger logger = Logger(sink);

        Parallel.For(0, 1000, index => logger.Info(index.ToString()));

        sink.Lines.Should().HaveCount(1000).And.OnlyHaveUniqueItems();
        _fallback.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Logger_NeedsACatalog()
    {
        FluentActions.Invoking(() => new Logger(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Catalog_NamesInAnyCase()
    {
        var sink = new RecordingLogger("Audio");
        _catalog.Add(sink).Should().BeTrue();

        _catalog.Get("AUDIO").Should().BeSameAs(sink);
        _catalog.Add(new RecordingLogger("audio")).Should().BeFalse();
        _catalog.Remove(sink).Should().BeTrue();
        _catalog.Remove(sink).Should().BeFalse();
        _catalog.Count.Should().Be(0);
    }

    [Fact]
    public void Catalog_RefusesTheDefaultLogger_NoLogger_AndNoName()
    {
        _catalog.Invoking(it => it.Add(new Logger(it))).Should().Throw<ArgumentException>();
        _catalog.Invoking(it => it.Add(null!)).Should().Throw<ArgumentNullException>();
        _catalog.Invoking(it => it.Remove((ILogger)null!)).Should().Throw<ArgumentNullException>();
        _catalog.Invoking(it => it.TryAdd(" ", new RecordingLogger("A"))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Catalog_NamesALoggerOfAnotherModule_ByItsLabel_OrItsIdentifier()
    {
        LoggerCatalog.NameOf(new LabeledLogger(Label.New("Foreign"))).Should().Be("Foreign");

        var plain = new PlainLogger();
        LoggerCatalog.NameOf(plain).Should().Be(plain.UID.ToString());
    }

    private class PlainLogger
        : ILogger
    {
        public virtual Identifier UID { get; } = Identifier.New();
        public IMetaQuant Meta => throw new NotSupportedException();
        public IReadOnlyList<IMetaQuant> Metas => [];

        public TMeta? FindMeta<TMeta>()
            where TMeta : class, IMetaQuant
        {
            return null;
        }

        public void Log(LogLevel level, string message)
        {
        }

        public void LogWithoutFormat(string message)
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class LabeledLogger(Label label)
        : PlainLogger, ILabeled
    {
        public Label Label => label;
        public override Identifier UID => label.UID;
    }
}
