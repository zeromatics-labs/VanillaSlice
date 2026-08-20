namespace {{ProjectName}}.Server.Data;

/// <summary>
/// Resolves the "%LOCALAPPDATA%" token in the SQLite "Data Source" connection string to a real,
/// cross-platform folder, and makes sure that folder exists before EF Core tries to open a file
/// inside it.
///
/// WebPortal and WebAPI both call this SAME method (it lives here, in the one project both hosts
/// already depend on transitively, specifically so the logic cannot be copy-pasted and drift). If
/// the two hosts ever resolve the connection string differently, they silently go back to opening
/// two separate database files - which is the exact bug this class exists to prevent.
///
/// OS-level environment-variable substitution is deliberately NOT used here: "LOCALAPPDATA" is a
/// Windows-only environment variable, so on Linux/macOS it is simply absent and the token would be
/// left in the string verbatim, which then fails at the SQLite layer with "unable to open database
/// file". <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/> is the cross-platform
/// equivalent: it resolves to "%AppData%\Local" on Windows, "~/.local/share" on Linux, and
/// "~/Library/Application Support" on macOS.
/// </summary>
public static class SqliteConnectionStringResolver
{
    private const string LocalAppDataToken = "%LOCALAPPDATA%";
    private const string DataSourcePrefix = "Data Source=";

    public static string Resolve(string connectionString)
    {
        if (!connectionString.Contains(LocalAppDataToken, StringComparison.Ordinal))
        {
            return connectionString;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var resolved = connectionString.Replace(LocalAppDataToken, localAppData, StringComparison.Ordinal);

        // GetFolderPath usually returns an existing directory, but SQLite will not create any
        // missing directory itself (it fails with "unable to open database file" instead), so
        // create the resolved parent folder ourselves before EF Core opens the file inside it.
        var dataSourceIndex = resolved.IndexOf(DataSourcePrefix, StringComparison.OrdinalIgnoreCase);
        if (dataSourceIndex >= 0)
        {
            var path = resolved[(dataSourceIndex + DataSourcePrefix.Length)..];
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        return resolved;
    }
}
