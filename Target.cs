using System.IO;

internal static class Target
{
    public static string? FromArgument(string arg)
    {
        const string prefix = "BrowSelURL:";
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            arg = arg[prefix.Length..];

        // Never let a target look like a browser option.
        if (arg.StartsWith("-", StringComparison.Ordinal) ||
            arg.StartsWith("/", StringComparison.Ordinal))
            return null;

        if (arg.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            arg.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            arg.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
            Path.IsPathFullyQualified(arg) ||
            File.Exists(arg))
            return arg;

        return null;
    }
}
