using System.Globalization;

using Stellar.Kernel.Lifecycle;
using Stellar.Kernel.LoggingSystem;
using Stellar.Kernel.Timing;

namespace Stellar.LoggingSystem.Sink;

/// <summary>
/// Makes the line of a message.
/// </summary>
public static class LoggerTextFormatter
{
    /// <summary>
    /// Makes the line of a message; the placeholders are those of <see cref="LogFormat"/>.
    /// </summary>
    /// <param name="format">The format of the level.</param>
    /// <param name="colorized">Whether to use the colorized format, if there is one.</param>
    /// <param name="level">The level of the message.</param>
    /// <param name="message">The message.</param>
    /// <param name="loggerName">The name of the logger.</param>
    /// <param name="engine">The engine whose name and clock go into the line.</param>
    /// <returns>The line, without a line end.</returns>
    public static string Format(
        LogFormat format, bool colorized, LogLevel level, string message,
        string loggerName, IEngineInstance engine
    )
    {
        string text = colorized ? format.ColorizedFormat ?? format.Format : format.Format;
        DateTime now = DateTime.Now;
        IClock clock = engine.Time.Engine;

        // Walking the stack is most of the price of a line: only for the formats that show it
        (string type, string method) = text.Contains("{8") || text.Contains("{9")
            ? LogCaller.Find()
            : (string.Empty, string.Empty);

        return string.Format(
            CultureInfo.InvariantCulture, // cultures
            text, // format
            now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), // 0
            now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), // 1
            clock.Name, // 2
            TimeSpan.FromSeconds(clock.Time).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture), // 3
            level, // 4
            engine.Name, // 5
            loggerName, // 6
            Environment.CurrentManagedThreadId, // 7
            type, // 8
            method, // 9
            message // 10
        );
    }
}
