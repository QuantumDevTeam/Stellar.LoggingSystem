using Stellar.Kernel.Configuration;
using Stellar.Kernel.EntryPoint;
using Stellar.Kernel.FileSystem;
using Stellar.Kernel.Lifecycle;
using Stellar.Kernel.LoggingSystem;
using Stellar.Kernel.Services;
using Stellar.LoggingSystem.Sink;

namespace Stellar.LoggingSystem;

/// <summary>
/// Publishes the logger of the engine and its catalog, with a console and, if the settings name one, a file.
/// </summary>
/// <param name="configuration">The configuration of the application (<see cref="LoggingSettings"/>).</param>
[StellarEntryPoint]
public sealed class LoggingSystemEntryPoint(RuntimeConfiguration configuration)
    : StellarEntryPoint(LoggingSystemModule.Id, configuration)
{
    private LoggerCatalog? _catalog;
    private Logger? _logger;
    private IServiceCatalog? _services;
    private ConsoleLogger? _console;
    private FileLogger? _file;

    private static Stream? OpenEngine(IServiceCatalog services, FilePath path)
    {
        // Nobody published a file system, or mounted the domain, yet: the logger keeps the lines and asks again
        if (services.Find<IFileSystem>() is not { } files || !files.Domains.Contains(path.Domain)) return null;

        FilePath directory = path.Parent;
        if (!directory.IsRoot) files.CreateDirectory(directory);
        return files.Open(path, FileOpenOptions.Append);
    }

    private static Stream OpenDisk(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Can be read while the engine runs
        return new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
    }

    private static FileLogger? MakeFile(LoggingSettings settings, IServiceCatalog services, IEngineInstance engine)
    {
        if (settings.EnginePath is { } path)
            return new FileLogger(engine, () => OpenEngine(services, path), minimumLevel: settings.FileLevel);
        if (settings.DiskPath is { } disk)
            return new FileLogger(engine, () => OpenDisk(disk), minimumLevel: settings.FileLevel);
        return null;
    }

    /// <inheritdoc />
    public override int Run<TContext>(ref readonly TContext context)
    {
        if (context.Services is not { } services || context.Engine is not { } engine) return 1;

        var settings = new LoggingSettings(RuntimeConfiguration);
        _services = services;
        _catalog = new LoggerCatalog();
        _logger = new Logger(_catalog);

        _console = new ConsoleLogger(engine, minimumLevel: settings.ConsoleLevel);
        _catalog.Add(_console);

        _file = MakeFile(settings, services, engine);
        if (_file is not null) _catalog.Add(_file);

        services.Set<ILoggerCatalog>(_catalog);
        services.Set<ILogger>(_logger);

        _logger.Info($"Logging started: {_catalog.Count} logger(s)");
        foreach (string problem in settings.Problems) _logger.Warning(problem);
        return 0;
    }

    /// <inheritdoc />
    public override void RequestStop<TContext>(ref readonly TContext context)
    {
        _logger?.Info("Logging stopped");
        Withdraw();
        _logger?.Stop();

        foreach (ILogger sink in _catalog?.Values ?? [])
            sink.Stop();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        Withdraw();
        _logger?.Stop();

        // Its own only: the loggers other modules added are theirs
        _console?.Stop();
        _file?.Dispose();
    }

    private void Withdraw()
    {
        if (_services is not { } services) return;
        _services = null;

        // Another module may have put its own there since
        if (ReferenceEquals(services.Find<ILogger>(), _logger)) services.Remove<ILogger>();
        if (ReferenceEquals(services.Find<ILoggerCatalog>(), _catalog)) services.Remove<ILoggerCatalog>();
    }
}
