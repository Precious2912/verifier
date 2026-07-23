namespace FaultInjector;

public static class Args
{
    // Returns the value following a flag, or null. e.g. --mode inject 
    public static string? GetValue(string[] args, string flag)
    {
        var i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--")
            ? args[i + 1]
            : null;
    }

    public static string GetValue(string[] args, string flag, string fallback)
        => GetValue(args, flag) ?? fallback;
}