using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace JaCrm.Api.Features.Auth;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>Output of the <c>hash-password</c> command. Required; the app refuses to start without it.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Mark the session cookie Secure. Turn off only for plain-HTTP use on a trusted network (D-36).</summary>
    public bool SecureCookie { get; set; } = true;

    public int SessionDays { get; set; } = 30;

    public int LoginAttemptsPerMinute { get; set; } = 5;
}

public sealed record LoginRequest(string? Password);

public sealed record SessionResponse(bool Authenticated);

/// <summary>Single-owner password hashing (ASP.NET Core Identity PBKDF2 v3 format, decision D-34).</summary>
public static class OwnerPassword
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object Owner = new();

    public static string Hash(string password) => Hasher.HashPassword(Owner, password);

    public static bool Verify(string hash, string password)
    {
        try
        {
            return Hasher.VerifyHashedPassword(Owner, hash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public static class AuthSetup
{
    public const string LoginRateLimitPolicy = "login";
    public const string CsrfHeaderName = "X-Requested-With";
    public const string CsrfHeaderValue = "JaCrm";
    private const string CookieName = "jacrm_session";

    public static IServiceCollection AddOwnerAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.Section));

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();

        // Options are read when first needed, not at registration, so every configuration source
        // (environment, test host settings) is already applied.
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((cookie, auth) =>
            {
                var options = auth.Value;
                cookie.Cookie.Name = CookieName;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Strict;
                cookie.Cookie.SecurePolicy = options.SecureCookie ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
                cookie.ExpireTimeSpan = TimeSpan.FromDays(options.SessionDays);
                cookie.SlidingExpiration = true;

                // An API answers 401/403; it never redirects to a login page.
                cookie.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                cookie.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(LoginRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = context.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.LoginAttemptsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return services;
    }

    /// <summary>Fails fast with instructions when no password hash is configured.</summary>
    public static void EnsurePasswordConfigured(IServiceProvider services)
    {
        var hash = services.GetRequiredService<IOptions<AuthOptions>>().Value.PasswordHash;
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new InvalidOperationException(
                "Auth:PasswordHash is not configured. Generate one with `dotnet run --project src/JaCrm.Api -- hash-password` " +
                "(or `docker compose run --rm app hash-password`) and set it as the Auth__PasswordHash environment variable.");
        }
    }

    /// <summary>CSRF defence (D-35): unsafe /api requests must carry a custom header. Browsers only let
    /// same-origin scripts set it, because CORS is disabled.</summary>
    public static IApplicationBuilder UseCsrfHeaderCheck(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var request = context.Request;
        var unsafeMethod = !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method));
        if (unsafeMethod && request.Path.StartsWithSegments("/api")
            && !string.Equals(request.Headers[CsrfHeaderName], CsrfHeaderValue, StringComparison.Ordinal))
        {
            await Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: $"Missing {CsrfHeaderName} header.").ExecuteAsync(context);
            return;
        }

        await next();
    });

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest request, IOptions<AuthOptions> options, HttpContext context) =>
        {
            var hash = options.Value.PasswordHash;
            if (string.IsNullOrEmpty(request.Password) || hash is null || !OwnerPassword.Verify(hash, request.Password))
            {
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Incorrect password.");
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "owner")], CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });
            return Results.NoContent();
        })
        .AllowAnonymous()
        .RequireRateLimiting(LoginRateLimitPolicy);

        group.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).AllowAnonymous();

        group.MapGet("/session", (HttpContext context) =>
            new SessionResponse(context.User.Identity?.IsAuthenticated == true)).AllowAnonymous();

        return api;
    }
}
