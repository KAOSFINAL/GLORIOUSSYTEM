using Microsoft.Data.Sqlite;

namespace GLORIOUSSYSTEM.Data.Models;

public static class HydroponicDatabasePath
{
    const string DefaultRelativePath = "database/hydroponic.db";

    public static string Resolve(string? connectionString, params string[] roots)
    {
        var configuredPath = GetDataSource(connectionString);
        if (Path.IsPathRooted(configuredPath) && File.Exists(configuredPath))
            return Path.GetFullPath(configuredPath);

        var repositoryRoot = FindRepositoryRoot(roots);
        if (repositoryRoot != null)
        {
            var relativePath = Path.IsPathRooted(configuredPath)
                || string.IsNullOrWhiteSpace(configuredPath)
                ? DefaultRelativePath
                : configuredPath;
            return Path.GetFullPath(Path.Combine(repositoryRoot, relativePath));
        }

        var fileName = string.IsNullOrWhiteSpace(configuredPath) || Path.IsPathRooted(configuredPath)
            ? "hydroponic.db"
            : Path.GetFileName(configuredPath);
        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GLORIOUSSYSTEM");
        Directory.CreateDirectory(appDataRoot);
        return Path.Combine(appDataRoot, fileName);
    }

    static string? GetDataSource(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        try
        {
            return new SqliteConnectionStringBuilder(connectionString).DataSource;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    static string? FindRepositoryRoot(IEnumerable<string> roots)
    {
        foreach (var root in roots.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var directory = new DirectoryInfo(root);
            while (directory != null)
            {
                var hasSolution = File.Exists(Path.Combine(directory.FullName, "GLORIOUSSYSTEM.slnx"));
                var hasDatabaseDirectory = Directory.Exists(Path.Combine(directory.FullName, "database"));
                if (hasSolution || hasDatabaseDirectory)
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        return null;
    }
}
