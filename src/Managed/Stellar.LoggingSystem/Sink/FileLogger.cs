using System.Text;

using Stellar.Kernel.Lifecycle;
using Stellar.Kernel.LoggingSystem;

namespace Stellar.LoggingSystem.Sink;

/// <summary>
/// Writes messages to a stream, opened at the first of them.
/// </summary>
/// <remarks>
/// While there is nowhere to write yet (the file system of the engine comes later), the lines are kept and written
/// once there is. A stream that fails is told once and given up until <see cref="Start"/>.
/// </remarks>
public class FileLogger
    : LoggerBase, IDisposable
{
    /// <summary>
    /// The most lines kept while there is nowhere to write; the oldest are dropped first.
    /// </summary>
    public const int MaxPending = 1024;

    private readonly IEngineInstance _engine;
    private readonly Func<Stream?> _open;
    private readonly bool _autoFlush;
    private readonly TextWriter? _fallback;
    private readonly Lock _gate = new();
    private readonly Queue<string> _pending = new();

    private StreamWriter? _writer;
    private long _dropped;
    private bool _failed;

    /// <summary>Makes a file logger.</summary>
    /// <param name="engine">The engine it tells about.</param>
    /// <param name="open">Opens the stream; <c>null</c> from it means there is nowhere to write yet.</param>
    /// <param name="autoFlush">Whether every line is flushed at once; errors always are.</param>
    /// <param name="format">The formats of the lines; <see cref="LoggerFormat.Default"/> if <c>null</c>.</param>
    /// <param name="minimumLevel">The least serious level it takes.</param>
    /// <param name="fallback">Where its own failure is told; the error output if <c>null</c>.</param>
    public FileLogger(
        IEngineInstance engine,
        Func<Stream?> open,
        bool autoFlush = true,
        LoggerFormat? format = null,
        LogLevel minimumLevel = LogLevel.Info,
        TextWriter? fallback = null
    ) : base(LoggerNames.File, true, format, minimumLevel)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _open = open ?? throw new ArgumentNullException(nameof(open));
        _autoFlush = autoFlush;
        _fallback = fallback;
    }

    private void Tell(string message)
    {
        try
        {
            (_fallback ?? Console.Error).WriteLine($"[Stellar.LoggingSystem] {message}");
        }
        catch
        {
            // There is nobody left to tell
        }
    }

    private bool TryOpen()
    {
        if (_writer is not null) return true;
        if (_open() is not { } stream) return false;

        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = _autoFlush };

        if (_dropped > 0) _writer.WriteLine($"[{_dropped} message(s) were dropped before the log could be opened]");
        _dropped = 0;
        while (_pending.TryDequeue(out string? line)) _writer.WriteLine(line);
        return true;
    }

    private void Close()
    {
        StreamWriter? writer = _writer;
        _writer = null;
        writer?.Dispose();
    }

    private void Fail(Exception exception)
    {
        _failed = true;
        _pending.Clear();
        _dropped = 0;

        try
        {
            Close();
        }
        catch
        {
            // Closing a broken stream may fail as writing did
        }

        Tell($"The file logger failed: {exception}");
    }

    private void Keep(string line)
    {
        if (_pending.Count == MaxPending)
        {
            _pending.Dequeue();
            _dropped++;
        }

        _pending.Enqueue(line);
    }

    /// <inheritdoc/>
    protected override void Write(string message, LogLevel? level)
    {
        string line = level is { } known
            ? LoggerTextFormatter.Format(Format.ForLevel(known), false, known, message, Label.Name, _engine)
            : message;

        lock (_gate)
        {
            if (_failed) return;

            try
            {
                if (!TryOpen())
                {
                    Keep(line);
                    return;
                }

                _writer!.WriteLine(line);
                if (!_autoFlush && level is LogLevel.Error or LogLevel.Exception) _writer.Flush();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }
    }

    /// <summary>
    /// Takes messages again, also after a failure.
    /// </summary>
    public override void Start()
    {
        lock (_gate)
        {
            _failed = false;
        }

        base.Start();
    }

    /// <summary>
    /// Closes the stream; lines that had nowhere to go till now are told about.
    /// </summary>
    public override void Stop()
    {
        base.Stop();

        lock (_gate)
        {
            try
            {
                if (_pending.Count > 0 && !TryOpen())
                {
                    Tell($"{_pending.Count + _dropped} message(s) were not written: the log could not be opened");
                    _pending.Clear();
                    _dropped = 0;
                }

                Close();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
    }
}
