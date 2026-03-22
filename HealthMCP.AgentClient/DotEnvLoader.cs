namespace HealthMCP.AgentClient;

public static class DotEnvLoader
{
    public static void Load()
    {
        var path = FindEnvFilePath();
        if (path is null)
            return;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (key.Length == 0)
                continue;

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string? FindEnvFilePath()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
                return candidate;
        }

        var baseCandidate = Path.Combine(AppContext.BaseDirectory, ".env");
        return File.Exists(baseCandidate) ? baseCandidate : null;
    }
}
