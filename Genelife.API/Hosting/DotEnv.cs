using Microsoft.Extensions.Configuration;

namespace Genelife.Api.Hosting;

public static class DotEnv
{
    public static void Load(ConfigurationManager configuration, string startDirectory)
    {
        var path = FindFile(startDirectory);
        if (path is null)
            return;

        var parsed = Parse(path);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in parsed)
        {
            if (configuration[key] is null)
                values[key] = value;
        }

        if (values.Count > 0)
            configuration.AddInMemoryCollection(values);
    }

    private static string? FindFile(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        for (var depth = 0; depth < 6 && directory is not null; depth++, directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static Dictionary<string, string?> Parse(string path)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line[7..].TrimStart();

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            if (key.Length == 0)
                continue;

            key = key.Replace("__", ":", StringComparison.Ordinal);

            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            values[key] = value;
        }

        return values;
    }
}
