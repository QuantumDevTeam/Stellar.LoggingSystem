using Stellar.Kernel.Lifecycle;
using Stellar.Kernel.LoggingSystem;

namespace Stellar.LoggingSystem.Sink;

/// <summary>
/// Writes messages to the console.
/// </summary>
public class ConsoleLogger
    : LoggerBase
{
    private readonly IEngineInstance _engine;
    private readonly TextWriter _output;
    private readonly bool _colorized;
    private readonly Lock _gate = new();

    /// <summary>Makes a console logger.</summary>
    /// <param name="engine">The engine it tells about.</param>
    /// <param name="output">Where it writes; the standard output if <c>null</c>.</param>
    /// <param name="colorized">
    /// Whether to color the lines; if <c>null</c>, only those of a standard output that is not redirected, unless
    /// NO_COLOR is set.
    /// </param>
    /// <param name="format">The formats of the lines; <see cref="LoggerFormat.Default"/> if <c>null</c>.</param>
    /// <param name="minimumLevel">The least serious level it takes.</param>
    public ConsoleLogger(
        IEngineInstance engine,
        TextWriter? output = null,
        bool? colorized = null,
        LoggerFormat? format = null,
        LogLevel minimumLevel = LogLevel.Debug
    ) : base(LoggerNames.Console, true, format, minimumLevel)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _output = output ?? Console.Out;
        _colorized = colorized ?? (output is null
                                   && !Console.IsOutputRedirected
                                   && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")));
    }

    /// <inheritdoc/>
    protected override void Write(string message, LogLevel? level)
    {
        string line = level is { } known
            ? LoggerTextFormatter.Format(Format.ForLevel(known), _colorized, known, message, Label.Name, _engine)
            : message;

        lock (_gate)
        {
            _output.WriteLine(line);
        }
    }

    /// <inheritdoc/>
    public override void Stop()
    {
        base.Stop();
        lock (_gate)
        {
            _output.Flush();
        }
    }
}
