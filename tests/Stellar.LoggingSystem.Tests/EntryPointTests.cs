using System.Text;

using Stellar.Core.EntryPoints;
using Stellar.Core.Services;
using Stellar.Kernel.Configuration;
using Stellar.Kernel.Data.Catalog;
using Stellar.Kernel.EntryPoint;
using Stellar.Kernel.FileSystem;
using Stellar.Kernel.LoggingSystem;
using Stellar.Kernel.Services;

namespace Stellar.LoggingSystem.Tests;

public sealed class EntryPointTests
    : IDisposable
{
    private readonly ServiceCatalog _services = new();
    private readonly TempFolder _folder = new();

    public void Dispose()
    {
        _folder.Dispose();
    }

    private static LoggingSystemEntryPoint Make(params (string Name, string Value)[] settings)
    {
        return new LoggingSystemEntryPoint(new RuntimeConfiguration
            { Settings = settings.ToDictionary(it => it.Name, it => it.Value) });
    }

    private static int Run(LoggingSystemEntryPoint entryPoint, ServiceCatalog services)
    {
        var context = new RunContext(null, services: services, engine: Stub.Engine());
        return entryPoint.Run(in context);
    }

    private static void Stop(LoggingSystemEntryPoint entryPoint)
    {
        var context = new StopContext(null, StopReason.Regular);
        entryPoint.RequestStop(in context);
        entryPoint.Dispose();
    }

    // A file system with one domain, mounted when it is told to
    private static IFileSystem Files(List<string> calls, Func<bool> mounted, MemoryStream written)
    {
        var domain = Stub.Of<IDomain>((member, args) =>
        {
            calls.Add($"{member} {args[0]}");
            return member == nameof(IDomain.Open) ? written : null;
        });
        IReadOnlyCatalog<string, IDomain> domains = Stub.Of<IReadOnlyCatalog<string, IDomain>>((member, _) =>
            member == "Contains" ? mounted() : domain);
        return Stub.Of<IFileSystem>((_, _) => domains);
    }

    [Fact]
    public void Run_PublishesTheLoggerAndTheCatalog_AndStop_TakesThemBack()
    {
        LoggingSystemEntryPoint entryPoint = Make();
        Run(entryPoint, _services).Should().Be(0);

        _services.Find<ILogger>().Should().BeOfType<Logger>();
        _services.Get<ILoggerCatalog>().Keys.Should().Equal(LoggerNames.Console);

        Stop(entryPoint);
        _services.Find<ILogger>().Should().BeNull();
        _services.Find<ILoggerCatalog>().Should().BeNull();
    }

    [Fact]
    public void Run_OutsideAnEngine_DoesNotStart_AndStillStops()
    {
        LoggingSystemEntryPoint entryPoint = Make();
        var nothing = new RunContext(null);
        entryPoint.Run(in nothing).Should().NotBe(0);
        var withoutEngine = new RunContext(null, services: _services);
        entryPoint.Run(in withoutEngine).Should().NotBe(0);

        _services.Count.Should().Be(0);
        FluentActions.Invoking(() => Stop(entryPoint)).Should().NotThrow();
    }

    [Fact]
    public void APathOfTheDisk_NeedsNoFileSystem()
    {
        string path = Path.Combine(_folder.Path, "logs", "game.log");
        LoggingSystemEntryPoint entryPoint = Make(("LogPath", path));
        Run(entryPoint, _services);
        _services.Get<ILogger>().Error("hello");

        // Can be read while the engine runs
        using (var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
        {
            reader.ReadToEnd().Should().Contain("hello");
        }

        Stop(entryPoint);
        File.ReadAllText(path).Should().Contain("Logging started: 2").And.Contain("Logging stopped");
    }

    [Fact]
    public void ALogOfTheDisk_IsAppendedTo()
    {
        string path = Path.Combine(_folder.Path, "game.log");

        for (int run = 0; run < 2; run++)
        {
            LoggingSystemEntryPoint entryPoint = Make(("LogPath", path));
            Run(entryPoint, _services);
            Stop(entryPoint);
        }

        File.ReadAllText(path).Split("Logging started").Should().HaveCount(3);
    }

    [Fact]
    public void APathOfTheEngine_WaitsForTheFileSystemAndForItsDomain()
    {
        List<string> calls = new();
        bool mounted = false;
        var written = new MemoryStream();
        LoggingSystemEntryPoint entryPoint = Make(("LogPath", "user://logs/game.log"));
        Run(entryPoint, _services);
        var logger = _services.Get<ILogger>();

        logger.Error("before the file system");
        _services.Set(Files(calls, () => mounted, written));
        logger.Error("before the domain");
        calls.Should().BeEmpty();

        mounted = true;
        logger.Error("after");
        Stop(entryPoint);

        calls.Should().Equal("CreateDirectory logs", "Open logs/game.log");
        Encoding.UTF8.GetString(written.ToArray()).Should().MatchRegex(
            "(?s)Logging started.*before the file system.*before the domain.*after.*Logging stopped");
    }

    [Fact]
    public void Levels_ComeFromTheSettings()
    {
        string path = Path.Combine(_folder.Path, "game.log");
        LoggingSystemEntryPoint entryPoint = Make(("LogPath", path), ("LogFileLevel", "Error"));
        Run(entryPoint, _services);
        _services.Get<ILogger>().Warning("too quiet");
        _services.Get<ILogger>().Error("loud");
        Stop(entryPoint);

        File.ReadAllText(path).Should().Contain("loud").And.NotContain("too quiet").And.NotContain("started");
    }

    [Fact]
    public void ABadSetting_DoesNotStopTheEngine()
    {
        LoggingSystemEntryPoint entryPoint = Make(("LogPath", "://"), ("LogLevel", "loud"));

        Run(entryPoint, _services).Should().Be(0);
        _services.Get<ILoggerCatalog>().Keys.Should().Equal(LoggerNames.Console);
        Stop(entryPoint);
    }

    [Fact]
    public void AFileThatCannotBeOpened_DoesNotStopTheEngine()
    {
        // A folder where the file should be
        LoggingSystemEntryPoint entryPoint = Make(("LogPath", _folder.Path));

        Run(entryPoint, _services).Should().Be(0);
        _services.Get<ILogger>().Invoking(logger => logger.Error("m")).Should().NotThrow();
        Stop(entryPoint);
    }

    [Fact]
    public void ALoggerOfAnotherModule_HearsTheEngine_AndIsStopped()
    {
        LoggingSystemEntryPoint entryPoint = Make();
        Run(entryPoint, _services);
        var foreign = new RecordingLogger("Foreign");
        _services.Get<ILoggerCatalog>().TryAdd("Foreign", foreign).Should().BeTrue();

        _services.Get<ILogger>().Info("m");
        Stop(entryPoint);

        foreign.Lines.Should().Equal("Info:m", "Info:Logging stopped");
        foreign.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Stop_LeavesTheLoggerAnotherModulePublished()
    {
        LoggingSystemEntryPoint entryPoint = Make();
        Run(entryPoint, _services);
        var other = new RecordingLogger("Other");
        _services.Set<ILogger>(other);

        Stop(entryPoint);

        _services.Find<ILogger>().Should().BeSameAs(other);
    }

    [Fact]
    public void Dispose_WithoutAStop_TakesEverythingBack()
    {
        LoggingSystemEntryPoint entryPoint = Make();
        Run(entryPoint, _services);

        entryPoint.Dispose();
        entryPoint.Dispose();

        _services.Find<ILogger>().Should().BeNull();
    }

    [Fact]
    public void TwoEngines_HaveLoggersOfTheirOwn()
    {
        LoggingSystemEntryPoint first = Make();
        LoggingSystemEntryPoint second = Make();
        var other = new ServiceCatalog();
        Run(first, _services);
        Run(second, other);

        other.Get<ILogger>().Should().NotBeSameAs(_services.Get<ILogger>());
        Stop(first);
        other.Find<ILogger>().Should().NotBeNull();
        Stop(second);
    }
}
