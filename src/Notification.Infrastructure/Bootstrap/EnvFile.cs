namespace Notification.Infrastructure.Bootstrap;

public static class EnvFile
{
    public static void Load()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var envPath = Path.Combine(current.FullName, ".env");
            if (File.Exists(envPath))
            {
                LoadFile(envPath);
                return;
            }

            var envExamplePath = Path.Combine(current.FullName, ".env.example");
            if (File.Exists(envExamplePath) && !File.Exists(envPath))
            {
                LoadFile(envExamplePath);
                return;
            }

            current = current.Parent;
        }

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDir))
        {
            var dir = new DirectoryInfo(baseDir);
            while (dir is not null)
            {
                var envPath = Path.Combine(dir.FullName, ".env");
                if (File.Exists(envPath))
                {
                    LoadFile(envPath);
                    return;
                }
                dir = dir.Parent;
            }
        }
    }

    private static void LoadFile(string path)
    {
        try
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    continue;

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0)
                    continue;

                var key = trimmed[..separatorIndex].Trim();
                var value = trimmed[(separatorIndex + 1)..].Trim();

                if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
                {
                    value = value[1..^1];
                }

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
        catch { }
    }
}
