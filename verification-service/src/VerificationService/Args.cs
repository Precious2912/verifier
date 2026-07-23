namespace VerificationService;

public static class Args
{
    public static string? GetValue(string[] args, string flag)
    {
        var i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--")
            ? args[i + 1]
            : null;
    }

    public static int GetInt(string[] args, string flag, int fallback)
        => int.TryParse(GetValue(args, flag), out var v) ? v : fallback;

    public static bool Has(string[] args, string flag) => args.Contains(flag);
}