using System.Text;
using JaCrm.Api.Data;
using JaCrm.Api.Features.Auth;

namespace JaCrm.Api.Cli;

/// <summary>Explicit command-line tasks: <c>hash-password</c>, <c>seed</c>, <c>backup</c>, <c>healthcheck</c>.
/// None of them ever runs as part of normal startup.</summary>
public static class Commands
{
    public static bool IsHashPassword(string[] args) => args is ["hash-password", ..];

    public static bool IsHostCommand(string[] args) => args is ["seed" or "backup", ..];

    public static bool IsHealthCheck(string[] args) => args is ["healthcheck", ..];

    /// <summary>Used by the Docker HEALTHCHECK (the runtime image has no curl).</summary>
    public static async Task<int> HealthCheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await http.GetAsync("http://localhost:8080/healthz");
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (HttpRequestException)
        {
            return 1;
        }
        catch (TaskCanceledException)
        {
            return 1;
        }
    }

    /// <summary>Reads the password from stdin (hidden when interactive) and prints its hash.</summary>
    public static int HashPassword()
    {
        var password = ReadPassword("Password: ");
        if (password.Length < 8)
        {
            Console.Error.WriteLine("Use at least 8 characters.");
            return 1;
        }

        if (!Console.IsInputRedirected && ReadPassword("Repeat password: ") != password)
        {
            Console.Error.WriteLine("The passwords do not match.");
            return 1;
        }

        Console.Error.WriteLine("Set this value as Auth__PasswordHash (for example in .env):");
        Console.WriteLine(OwnerPassword.Hash(password));
        return 0;
    }

    public static async Task<int> RunHostCommandAsync(IServiceProvider services, string[] args)
    {
        await DatabaseMaintenance.MigrateAsync(services);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        switch (args[0])
        {
            case "seed":
                var seeded = await DevSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<Common.Clock>());
                Console.WriteLine(seeded
                    ? "Seeded demonstration data."
                    : "The database already contains data. Seeding only runs on an empty database.");
                return seeded ? 0 : 1;
            case "backup":
                var path = await DatabaseMaintenance.BackupAsync(db, scope.ServiceProvider.GetRequiredService<DatabaseFiles>(), "manual");
                Console.WriteLine($"Backup written to {path}");
                return 0;
            default:
                return 1;
        }
    }

    private static string ReadPassword(string prompt)
    {
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? "";
        }

        Console.Error.Write(prompt);
        var builder = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                return builder.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (builder.Length > 0)
                {
                    builder.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                builder.Append(key.KeyChar);
            }
        }
    }
}
