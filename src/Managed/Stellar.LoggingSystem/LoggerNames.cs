namespace Stellar.LoggingSystem;

/// <summary>
/// The names of the loggers of this module.
/// </summary>
public static class LoggerNames
{
    /// <summary>
    /// The default logger, which routes to the others.
    /// </summary>
    public const string Default = "Default";

    /// <summary>
    /// The logger of the console.
    /// </summary>
    public const string Console = "Console";

    /// <summary>
    /// The logger of the file.
    /// </summary>
    public const string File = "File";
}
