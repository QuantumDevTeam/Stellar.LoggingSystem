using Stellar.Kernel.Configuration;
using Stellar.Kernel.FileSystem;
using Stellar.Kernel.LoggingSystem;

namespace Stellar.LoggingSystem;

/// <summary>
/// What the module reads from the settings of the application: the <c>Runtime</c> section of <c>.stellar.project</c>,
/// or the command line (<c>--stellar:LogPath=logs/game.log</c>). Every setting may be missing.
/// </summary>
/// <remarks>A value that cannot be read is told in <see cref="Problems"/> and left at its default: a typing error in
/// the settings of logging never stops an engine.</remarks>
public sealed class LoggingSettings
{
    /// <summary>The setting of the file of the log.</summary>
    public const string LogPathName = "LogPath";

    /// <summary>The setting of the least serious level of the console.</summary>
    public const string LogLevelName = "LogLevel";

    /// <summary>The setting of the least serious level of the file.</summary>
    public const string LogFileLevelName = "LogFileLevel";

    private readonly List<string> _problems = [];

    /// <summary>
    /// Gets the path of the log in the file system of the engine, if <c>LogPath</c> is <c>domain://path</c>.
    /// </summary>
    public FilePath? EnginePath { get; }

    /// <summary>
    /// Gets the full path of the log on the disk, if <c>LogPath</c> is a path of the machine. A relative one starts at
    /// the folder of the application.
    /// </summary>
    public string? DiskPath { get; }

    /// <summary>
    /// Gets the least serious level of the console.
    /// </summary>
    public LogLevel ConsoleLevel { get; }

    /// <summary>
    /// Gets the least serious level of the file.
    /// </summary>
    public LogLevel FileLevel { get; }

    /// <summary>
    /// Gets what could not be read.
    /// </summary>
    public IReadOnlyList<string> Problems => _problems;

    /// <summary>
    /// Reads the settings.
    /// </summary>
    /// <param name="configuration">The configuration of the application.</param>
    /// <param name="baseDirectory">Where a relative disk path starts; the folder of the application if <c>null</c>.</param>
    public LoggingSettings(RuntimeConfiguration configuration, string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        ConsoleLevel = Level(configuration, LogLevelName, LogLevel.Debug);
        FileLevel = Level(configuration, LogFileLevelName, LogLevel.Info);

        string? text = Find(configuration, LogPathName);
        if (text is null) return;

        if (text.Contains(FilePath.DomainSeparator, StringComparison.Ordinal))
        {
            if (FilePath.TryParse(text, out FilePath path) && !path.IsRoot) EnginePath = path;
            else _problems.Add($"{LogPathName}: '{text}' is not the path of a file, expected domain://path");
            return;
        }

        try
        {
            string full = Path.GetFullPath(text, baseDirectory ?? AppContext.BaseDirectory);
            if (Path.GetFileName(full).Length > 0) DiskPath = full;
            else _problems.Add($"{LogPathName}: '{text}' is a folder, expected a file");
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                              or PathTooLongException)
        {
            _problems.Add($"{LogPathName}: '{text}' is not a path: {exception.Message}");
        }
    }

    private static string? Find(RuntimeConfiguration configuration, string name)
    {
        // The settings of a configuration made by hand may tell case apart
        foreach ((string key, string value) in configuration.Settings)
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        return null;
    }

    private LogLevel Level(RuntimeConfiguration configuration, string name, LogLevel fallback)
    {
        string? text = Find(configuration, name);
        if (text is null) return fallback;

        // Names only: Enum.TryParse alone takes any number, and lists of names
        if (Enum.GetNames<LogLevel>().Contains(text, StringComparer.OrdinalIgnoreCase))
            return Enum.Parse<LogLevel>(text, true);

        _problems.Add(
            $"{name}: '{text}' is not a level, expected one of {string.Join(", ", Enum.GetNames<LogLevel>())}");
        return fallback;
    }
}
