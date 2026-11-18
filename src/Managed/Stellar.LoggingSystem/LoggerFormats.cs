using Stellar.Kernel.LoggingSystem;

namespace Stellar.LoggingSystem;

/// <summary>
/// TODO: summary
/// </summary>
public sealed class LoggerFormats
{
    /// <summary>
    /// TODO: summary
    /// </summary>
    public LogFormat Info { get; init; } = new()
    {
        Format = "{0}:{1} | {2}      | {3}:{4}:{5} - {6}",
        ColorizedFormat = "\e[32m{0}\e[0m" +
                          "\e[31m:\e[0m" +
                          "\e[2;32m{1}\e[0m " +

                          "\e[31m|\e[0m " +

                          "\e[1;38m{2}\e[0m      " +

                          "\e[31m|\e[0m " +

                          "\e[36m{3}\e[0m" +
                          "\e[31m:\e[0m" +
                          "\e[36m{4}\e[0m " +
                          // "\e[31m:\e[0m" +
                          // "\e[36m{5}\e[0m " +

                          "\e[31m-\e[0m " +

                          "\e[1;38m{6}\e[0m"
    };

    /// <summary>
    /// TODO: summary
    /// </summary>
    public LogFormat Debug { get; init; } = new()
    {
        Format = "{0}:{1} | {2}     | {3}:{4}:{5} - {6}",
        ColorizedFormat = "\e[32m{0}\e[0m" +
                          "\e[31m:\e[0m" +
                          "\e[2;32m{1}\e[0m " +

                          "\e[31m|\e[0m " +

                          "\e[1;33m{2}\e[0m     " +

                          "\e[31m|\e[0m " +

                          "\e[36m{3}\e[0m" +
                          "\e[31m:\e[0m" +
                          "\e[36m{4}\e[0m " +
                          // "\e[31m:\e[0m" +
                          // "\e[36m{5}\e[0m " +

                          "\e[31m-\e[0m " +

                          "\e[1;33m{6}\e[0m"
    };

    /// <summary>
    /// TODO: summary
    /// </summary>
    public LogFormat Success { get; init; } = new()
    {
        Format = "{0}:{1} | {2}   | {3}:{4}:{5} - {6}",
        ColorizedFormat = "\e[32m{0}\e[0m" +
                          "\e[31m:\e[0m" +
                          "\e[2;32m{1}\e[0m " +

                          "\e[31m|\e[0m " +

                          "\e[1;32m{2}\e[0m   " +

                          "\e[31m|\e[0m " +

                          "\e[36m{3}\e[0m" +
                          "\e[31m:\e[0m" +
                          "\e[36m{4}\e[0m " +
                          // "\e[31m:\e[0m" +
                          // "\e[36m{5}\e[0m " +

                          "\e[31m-\e[0m " +

                          "\e[1;32m{6}\e[0m"
    };

    /// <summary>
    /// TODO: summary
    /// </summary>
    public LogFormat Warning { get; init; } = new()
    {
        Format = "{0}:{1} | {2}   | {3}:{4}:{5} - {6}",
        ColorizedFormat = "\e[32m{0}\e[0m" +
                          "\e[31m:\e[0m" +
                          "\e[2;32m{1}\e[0m " +

                          "\e[31m|\e[0m " +

                          "\e[1;34m{2}\e[0m   " +

                          "\e[31m|\e[0m " +

                          "\e[36m{3}\e[0m" +
                          "\e[31m:\e[0m" +
                          "\e[36m{4}\e[0m " +
                          // "\e[31m:\e[0m" +
                          // "\e[36m{5}\e[0m " +

                          "\e[31m-\e[0m " +

                          "\e[1;34m{6}\e[0m"
    };

    /// <summary>
    /// TODO: summary
    /// </summary>
    public LogFormat Error { get; init; } = new()
    {
        Format = "{0}:{1} | {2}     | {3}:{4}:{5} - {6}",
        ColorizedFormat = "\e[32m{0}\e[0m" +
                          "\e[31m:\e[0m" +
                          "\e[2;32m{1}\e[0m " +

                          "\e[31m|\e[0m " +

                          "\e[1;31m{2}\e[0m     " +

                          "\e[31m|\e[0m " +

                          "\e[36m{3}\e[0m" +
                          "\e[31m:\e[0m" +
                          "\e[36m{4}\e[0m"  +
                          // "\e[31m:\e[0m" +
                          // "\e[36m{5}\e[0m " +

                          "\e[31m-\e[0m " +

                          "\e[1;38m{6}\e[0m"
    };

    /// <summary>
    /// TODO: summary
    /// </summary>
    public LogFormat Exception { get; init; } = new()
    {
        Format = "{0}:{1} | {2} | {3}:{4}:{5} - {6}",
        ColorizedFormat = "\e[32m{0}\e[0m" +
                          "\e[31m:\e[0m" +
                          "\e[2;32m{1}\e[0m " +

                          "\e[31m|\e[0m " +

                          "\e[1;31m{2}\e[0m " +

                          "\e[31m|\e[0m " +

                          "\e[36m{3}\e[0m" +
                          "\e[31m:\e[0m" +
                          "\e[36m{4}\e[0m " +
                          // "\e[31m:\e[0m" +
                          // "\e[36m{5}\e[0m " +

                          "\e[31m-\e[0m " +

                          "\e[1;31m{6}\e[0m"
    };

    /// <summary>
    /// TODO: summary
    /// </summary>
    public LogFormat GetFormat(LogLevel level) => level switch
    {
        LogLevel.Info => Info,
        LogLevel.Debug => Debug,
        LogLevel.Success => Success,
        LogLevel.Warning => Warning,
        LogLevel.Error => Error,
        LogLevel.Exception => Exception,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
    };
}
