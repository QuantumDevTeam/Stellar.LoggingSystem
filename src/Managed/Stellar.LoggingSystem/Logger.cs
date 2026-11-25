using Stellar.Kernel.LoggingSystem;

namespace Stellar.LoggingSystem;

/// <summary>
/// The default logger of an engine: the front door that gives every message to every logger of the catalog.
/// </summary>
/// <remarks>
/// <para>It is a router, not a sink: it formats nothing and writes nowhere. The loggers of the catalog are the
/// sinks (console, file, a module's own), each with its own formats in its meta.</para>
/// <para>It is never in the catalog: <see cref="LoggerCatalog"/> refuses it, and a sink that is this logger is
/// skipped. A sink that logs while it fails cannot come back here: routing is remembered per thread.</para>
/// <para>Every member is safe to call from any thread.</para>
/// </remarks>
public sealed class Logger
    : LoggerBase
{
    [ThreadStatic] private static Logger? _routing;

    private readonly ILoggerCatalog _catalog;
    private readonly TextWriter? _fallback;

    /// <summary>Makes the default logger.</summary>
    /// <param name="catalog">The loggers this one gives messages to.</param>
    /// <param name="fallback">Where a failure of a sink is told; the error output if <c>null</c>.</param>
    public Logger(ILoggerCatalog catalog, TextWriter? fallback = null)
        : base(LoggerNames.Default, minimumLevel: LogLevel.Debug)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _fallback = fallback;
    }

    private void WriteFallback(string message)
    {
        try
        {
            (_fallback ?? Console.Error).WriteLine($"[Stellar.LoggingSystem] {message}");
        }
        catch
        {
            // ignore
        }
    }

    /// <inheritdoc/>
    protected override void Write(string message, LogLevel? level)
    {
        if (ReferenceEquals(_routing, this))
        {
            WriteFallback($"[reentrant {level}] {message}");
            return;
        }

        var previous = _routing;
        _routing = this;
        try
        {
            if (_catalog.Count == 0)
            {
                if (level is LogLevel.Warning or LogLevel.Error or LogLevel.Exception)
                    WriteFallback($"[{level}] {message}");
                return;
            }

            foreach (var sink in _catalog.Values)
            {
                if (ReferenceEquals(sink, this)) continue;

                try
                {
                    if (level is not null) sink.Log((LogLevel)level, message);
                    else sink.LogWithoutFormat(message);
                }
                catch (Exception exception)
                {
                    WriteFallback($"The logger '{sink.UID}' failed: {exception}");
                }
            }
        }
        finally
        {
            _routing = previous;
        }
    }
}
