using Stellar.Core.Quantization;
using Stellar.Kernel;
using Stellar.Kernel.LoggingSystem;

namespace Stellar.LoggingSystem;

/// <summary>
/// The meta of a logger: its label, its switch, its level and its formats. Safe to change from any thread.
/// </summary>
public sealed class LoggerMeta
    : MetaQuant
{
    private int _isActive;
    private object _format;
    private int _minimumLevel;

    /// <summary>
    /// Gets the label of the logger.
    /// </summary>
    public Label Label { get; }

    /// <summary>Makes the meta of a logger.</summary>
    /// <param name="name">Its name, unique in the catalog.</param>
    /// <param name="isActive">Its activity state.</param>
    /// <param name="format">The formats of its lines; <see cref="LoggerFormat.Default"/> if <c>null</c>.</param>
    /// <param name="minimumLevel">The least serious level it takes.</param>
    /// <exception cref="ArgumentException">Thrown if the name is empty.</exception>
    public LoggerMeta(
        string name,
        bool isActive = true,
        LoggerFormat? format = null,
        LogLevel minimumLevel = LogLevel.Debug
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A logger needs a name", nameof(name));

        Label = new Label(UID, name);
        _isActive = isActive ? 1 : 0;
        _format = format ?? LoggerFormat.Default;
        _minimumLevel = (int)minimumLevel;
    }

    /// <summary>
    /// Gets or sets whether the logger takes messages.
    /// </summary>
    public bool IsActive
    {
        get => Volatile.Read(ref _isActive) != 0;
        set => Volatile.Write(ref _isActive, value ? 1 : 0);
    }

    /// <summary>
    /// Gets or sets the least serious level the logger takes.
    /// </summary>
    public LogLevel MinimumLevel
    {
        get => (LogLevel)Volatile.Read(ref _minimumLevel);
        set => Volatile.Write(ref _minimumLevel, (int)value);
    }

    /// <summary>
    /// Gets or sets the formats of the lines.
    /// </summary>
    public LoggerFormat Format
    {
        get => (LoggerFormat)Volatile.Read(ref _format);
        set => Volatile.Write(ref _format, value);
    }
}

/// <summary>
/// What every logger of this module shares: a meta with its name, switch, level and formats, and the rules of
/// taking a message.
/// </summary>
/// <param name="name">Its name in the catalog.</param>
/// <param name="isActive">Its activity state</param>
/// <param name="format">Its formats; <see cref="LoggerFormat.Default"/> if <c>null</c>.</param>
/// <param name="minimumLevel">The least serious level it takes.</param>
public abstract class LoggerBase(
    string name,
    bool isActive = true,
    LoggerFormat? format = null,
    LogLevel minimumLevel = LogLevel.Debug
) : Quant<LoggerMeta>(new LoggerMeta(name, isActive, format, minimumLevel)), ILogger, ILabeled
{
    /// <summary>
    /// Gets the label of the logger.
    /// </summary>
    public Label Label => MetaQuant.Label;

    /// <summary>
    /// Gets whether the logger takes messages.
    /// </summary>
    public bool IsActive => MetaQuant.IsActive;

    /// <summary>
    /// Gets or sets the least serious level the logger takes.
    /// </summary>
    public LogLevel MinimumLevel
    {
        get => MetaQuant.MinimumLevel;
        set => MetaQuant.MinimumLevel = value;
    }

    /// <summary>
    /// Gets or sets the formats of the lines.
    /// </summary>
    public LoggerFormat Format
    {
        get => MetaQuant.Format;
        set => MetaQuant.Format = value;
    }

    private bool IsEnabled(LogLevel level)
    {
        return level >= MetaQuant.MinimumLevel;
    }

    /// <summary>
    /// Writes a message the logger took: it is active, and the level is serious enough.
    /// </summary>
    /// <param name="message">The message, never <c>null</c>.</param>
    /// <param name="level">The level; <c>null</c> for a message without a format.</param>
    protected abstract void Write(string message, LogLevel? level);

    /// <inheritdoc />
    public void Log(LogLevel level, string message)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (message is null || !MetaQuant.IsActive || !IsEnabled(level)) return;
        Write(message, level);
    }

    /// <inheritdoc />
    public void LogWithoutFormat(string message)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (message is null || !MetaQuant.IsActive) return;
        Write(message, null);
    }

    /// <inheritdoc/>
    public virtual void Start()
    {
        MetaQuant.IsActive = true;
    }

    /// <inheritdoc/>
    public virtual void Stop()
    {
        MetaQuant.IsActive = false;
    }
}
