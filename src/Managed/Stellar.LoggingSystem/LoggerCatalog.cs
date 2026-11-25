using Stellar.Kernel;
using Stellar.Kernel.LoggingSystem;
using Stellar.Core.Data.Catalog;

namespace Stellar.LoggingSystem;

/// <summary>
/// The loggers of one engine, by name: the sinks the default logger writes into.
/// </summary>
public sealed class LoggerCatalog()
    : Catalog<string, ILogger>(StringComparer.OrdinalIgnoreCase), ILoggerCatalog
{
    /// <summary>
    /// Gets the key of a logger: its name, or the text of its identifier.
    /// </summary>
    /// <param name="logger">A logger; one with a label (<see cref="ILabeled"/>) is named by it.</param>
    /// <returns>The key of <paramref name="logger"/>.</returns>
    public static string NameOf(ILogger logger)
    {
        return logger is ILabeled labeled ? labeled.Label.Name : logger.UID.ToString();
    }

    /// <summary>
    /// Adds a logger under its name.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <returns><c>true</c> if it was added; <c>false</c> if the name is taken.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown for a <see cref="Logger"/>: it routes to the catalog.</exception>
    public bool Add(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return TryAdd(NameOf(logger), logger);
    }

    /// <summary>
    /// Takes out the logger under the name of this one.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <returns><c>true</c> if there was one.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger"/> is <c>null</c>.</exception>
    public bool Remove(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return Remove(NameOf(logger));
    }

    /// <inheritdoc />
    protected override string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("A logger needs a name", nameof(key));
        return key;
    }

    /// <inheritdoc />
    protected override void Validate(string key, ILogger value)
    {
        base.Validate(key, value);

        if (value is Logger)
            throw new ArgumentException("The default logger routes to the catalog: it cannot be in it", nameof(value));
    }
}
