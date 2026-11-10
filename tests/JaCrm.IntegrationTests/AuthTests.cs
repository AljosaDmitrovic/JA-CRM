using System.Net;
using System.Net.Http.Json;
using JaCrm.IntegrationTests.Infrastructure;

namespace JaCrm.IntegrationTests;

public sealed class AuthTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Theory]
    [InlineData("/api/settings")]
    [InlineData("/api/meta")]
    public async Task Protected_endpoints_require_a_session(string url)
    {
        var response = await factory.CreateAnonymousClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_and_session_check_are_anonymous()
    {
        var client = factory.CreateAnonymousClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/healthz")).StatusCode);
        var session = await client.GetJsonAsync<SessionDto>("/api/auth/session");
        Assert.False(session.Authenticated);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var response = await factory.CreateAnonymousClient().PostJsonAsync("/api/auth/login", new { password = "nope-nope-nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_issues_an_http_only_same_site_strict_cookie()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.PostJsonAsync("/api/auth/login", new { password = AppFactory.Password });

        await response.EnsureStatusAsync(HttpStatusCode.NoContent);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("jacrm_session=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.True((await client.GetJsonAsync<SessionDto>("/api/auth/session")).Authenticated);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings")).StatusCode);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        await (await client.PostJsonAsync("/api/auth/logout")).EnsureStatusAsync(HttpStatusCode.NoContent);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/settings")).StatusCode);
    }

    [Fact]
    public async Task Unsafe_requests_without_the_csrf_header_are_forbidden()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        client.DefaultRequestHeaders.Remove("X-Requested-With");

        var response = await client.PutAsJsonAsync("/api/settings/exchange-rates", new { rates = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_routes_return_json_404()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private sealed record SessionDto(bool Authenticated);
}

public sealed class LoginRateLimitTests : IAsyncLifetime
{
    private readonly AppFactory _factory = new() { LoginAttemptsPerMinute = 3 };

    [Fact]
    public async Task Login_is_rate_limited()
    {
        var client = _factory.CreateAnonymousClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await client.PostJsonAsync("/api/auth/login", new { password = "wrong-password" })).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
