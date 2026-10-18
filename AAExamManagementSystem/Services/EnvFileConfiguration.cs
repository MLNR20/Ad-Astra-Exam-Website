namespace AAExamManagementSystem.Services;

/// <summary>
/// Loads KEY=value pairs from a .env file into configuration. Values already set (for example by a real
/// environment variable) win over the file, so deployments can override it.
/// </summary>
public static class EnvFileConfiguration
{
    private const int MaxParentsToSearch = 4;

    public static ConfigurationManager AddEnvFile(this ConfigurationManager configuration, string startDirectory)
    {
        var path = FindEnvFile(startDirectory);
        if (path is null)
        {
            return configuration;
        }

        var values = Parse(File.ReadAllLines(path))
            .Where(pair => string.IsNullOrEmpty(configuration[pair.Key]))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        configuration.AddInMemoryCollection(values);
        return configuration;
    }

    public static IDictionary<string, string?> Parse(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line["export ".Length..].TrimStart();
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (value.Length >= 2
                && (value[0] == '"' || value[0] == '\'')
                && value[^1] == value[0])
            {
                value = value[1..^1];
            }

            values[key] = value;
        }

        return values;
    }

    // The .env sits at the repository root, one level above the web project's content root.
    private static string? FindEnvFile(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        for (var i = 0; i <= MaxParentsToSearch && directory is not null; i++, directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
