using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JaCrm.Api.Data;
using JaCrm.Api.Features.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace JaCrm.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real API in memory against a real SQLite file in its own temporary folder (no mocks).
/// The clock is a <see cref="FakeTimeProvider"/> so date rules (staleness, deadlines) are deterministic.
/// </summary>
public class AppFactory : WebApplicationFactory<Program>
{
    public const string Password = "correct horse battery staple";

    private static readonly string PasswordHash = OwnerPassword.Hash(Password);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>10 Nov 2026, 09:00 UTC (10:00 in Belgrade).</summary>
    public static readonly DateTimeOffset Start = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    public string DataDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "jacrm-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Keep the data folder on dispose (to start another instance on it, like a restart).</summary>
    public bool KeepData { get; init; }

    public FakeTimeProvider Time { get; } = new(Start);

    public int LoginAttemptsPerMinute { get; init; } = 1000;

    public string DatabasePath => Path.Combine(DataDirectory, "jacrm.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("App:DataDirectory", DataDirectory);
        builder.UseSetting("Auth:PasswordHash", PasswordHash);
        builder.UseSetting("Auth:SecureCookie", "false");
        builder.UseSetting("Auth:LoginAttemptsPerMinute", LoginAttemptsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    /// <summary>A client without a session that still sends the CSRF header.</summary>
    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(AuthSetup.CsrfHeaderName, AuthSetup.CsrfHeaderValue);
        return client;
    }

    /// <summary>A signed-in client (session cookie + CSRF header).</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { password = Password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Direct database access for assertions the API does not expose.</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (KeepData)
        {
            GC.SuppressFinalize(this);
            return;
        }

        try
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is harmless.
        }

        GC.SuppressFinalize(this);
    }
}
