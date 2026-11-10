using JaCrm.Api.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JaCrm.Api.Data;

/// <summary>Locates the database file and creates consistent online backups with <c>VACUUM INTO</c>.</summary>
public sealed class DatabaseFiles(IOptions<AppOptions> options, IHostEnvironment environment)
{
    public string DataDirectory { get; } = Path.GetFullPath(options.Value.DataDirectory, environment.ContentRootPath);

    public string DatabasePath => Path.Combine(DataDirectory, "jacrm.db");

    public string KeysDirectory => Path.Combine(DataDirectory, "keys");

    public string BackupsDirectory => Path.Combine(DataDirectory, "backups");

    public string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = DatabasePath,
        ForeignKeys = true,
        DefaultTimeout = 10,
    }.ToString();
}

public static class DatabaseMaintenance
{
    /// <summary>Applies pending migrations. If the database already has data, a backup is taken first (D-40).</summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<DatabaseFiles>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMaintenance));

        Directory.CreateDirectory(files.DataDirectory);

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        if (File.Exists(files.DatabasePath) && (await db.Database.GetAppliedMigrationsAsync(ct)).Any())
        {
            var backup = await BackupAsync(db, files, "pre-migration", ct);
            logger.LogInformation("Backed up the database to {Backup} before applying {Count} migration(s)", backup, pending.Count);
        }

        await db.Database.MigrateAsync(ct);
        logger.LogInformation("Applied migrations: {Migrations}", string.Join(", ", pending));
    }

    /// <summary>Writes a consistent copy of the live database to the backups folder and returns its path.</summary>
    public static async Task<string> BackupAsync(AppDbContext db, DatabaseFiles files, string label, CancellationToken ct = default)
    {
        Directory.CreateDirectory(files.BackupsDirectory);
        var target = Path.Combine(files.BackupsDirectory, $"jacrm-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{label}.db");
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "VACUUM INTO $target";
            command.Parameters.AddWithValue("$target", target);
            await command.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            await connection.CloseAsync();
        }

        return target;
    }
}
