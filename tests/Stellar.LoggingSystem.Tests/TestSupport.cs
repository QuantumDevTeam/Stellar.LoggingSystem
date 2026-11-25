using System.Reflection;

using Stellar.Kernel.Lifecycle;
using Stellar.Kernel.LoggingSystem;

namespace Stellar.LoggingSystem.Tests;

/// <summary>
/// Remembers what it was given, or fails.
/// </summary>
internal sealed class RecordingLogger(string name, LogLevel minimumLevel = LogLevel.Debug, bool fails = false)
    : LoggerBase(name, minimumLevel: minimumLevel)
{
    private readonly List<string> _lines = [];

    public Action? OnWrite { get; init; }

    public string[] Lines
    {
        get
        {
            lock (_lines)
            {
                return _lines.ToArray();
            }
        }
    }

    protected override void Write(string message, LogLevel? level)
    {
        if (fails) throw new InvalidOperationException("broken");
        lock (_lines)
        {
            _lines.Add($"{level}:{message}");
        }

        OnWrite?.Invoke();
    }
}

/// <summary>
/// An implementation of a contract that answers with a delegate: the engine is made of contracts only.
/// </summary>
public class Stub
    : DispatchProxy
{
    private Func<string, object?[], object?> _answer = (_, _) => null;

    public static T Of<T>(Func<string, object?[], object?> answer)
        where T : class
    {
        T proxy = Create<T, Stub>();
        ((Stub)(object)proxy)._answer = answer;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        return _answer(method!.Name, args ?? []);
    }

    /// <summary>An engine with a name and a clock.</summary>
    public static IEngineInstance Engine(double seconds = 0)
    {
        var clock = Of<Kernel.Timing.IClock>((member, _) => member == "get_Name" ? "Engine" : seconds);
        var time = Of<Kernel.Timing.ITimeSystem>((_, _) => clock);
        return Of<IEngineInstance>((member, _) => member == "get_Time" ? time : "Main");
    }
}

/// <summary>
/// A folder of its own, gone with the test.
/// </summary>
internal sealed class TempFolder
    : IDisposable
{
    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Stellar.LoggingSystem.Tests",
            Guid.NewGuid().ToString("N"));

    public TempFolder()
    {
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        Directory.Delete(Path, true);
    }
}
