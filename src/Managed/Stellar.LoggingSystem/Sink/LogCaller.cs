using System.Diagnostics;

using Stellar.Kernel.LoggingSystem;

namespace Stellar.LoggingSystem.Sink;

/// <summary>
/// Finds who logs: the first method on the stack that does not belong to logging. Nothing is counted in frames, so
/// it does not depend on how the code was compiled or on how many loggers a message goes through.
/// </summary>
internal static class LogCaller
{
    private const string UnknownType = "UnknownType";
    private const string UnknownMethod = "UnknownMethod";

    public static (string Type, string Method) Find()
    {
        foreach (StackFrame frame in new StackTrace(false).GetFrames())
        {
            if (frame.GetMethod() is not { DeclaringType: { } type } method) continue;
            if (type.Assembly == typeof(LogCaller).Assembly || type == typeof(LoggerExtensions)) continue;

            // A loop of an async method or a lambda: <Name>d__0.MoveNext, <>c.<Name>b__0
            string name = Original(type.Name) ?? Original(method.Name) ?? method.Name;
            while (type.Name.StartsWith('<') && type.DeclaringType is { } outer) type = outer;

            return (type.FullName ?? type.Name, name);
        }

        return (UnknownType, UnknownMethod);
    }

    private static string? Original(string name)
    {
        int end = name.IndexOf('>');
        return name.StartsWith('<') && end > 1 ? name[1..end] : null;
    }
}
