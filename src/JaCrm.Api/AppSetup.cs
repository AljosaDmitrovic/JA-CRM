using System.Text.RegularExpressions;
using JaCrm.Api.Common;
using JaCrm.Api.Data;
using JaCrm.Api.Features;
using JaCrm.Api.Features.Auth;
using JaCrm.Api.Features.Meta;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JaCrm.Api;

/// <summary>Service registration, middleware pipeline and endpoint map in one place.</summary>
public static partial class AppSetup
{
    public static IServiceCollection AddJaCrm(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.Section));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Clock>();
        services.AddSingleton<DatabaseFiles>();

        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<DatabaseFiles>().ConnectionString));

        // Cookie encryption keys must survive container rebuilds, or every restart logs the owner out.
        services.AddDataProtection().SetApplicationName("JaCrm");
        services.AddOptions<KeyManagementOptions>()
            .Configure<DatabaseFiles>((keys, files) =>
                keys.XmlRepository = new Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository(
                    new DirectoryInfo(files.KeysDirectory), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance));

        services.AddProblemDetails();
        services.AddExceptionHandler<AppExceptionHandler>();
        services.AddOwnerAuthentication(configuration);
        services.AddFeatureServices();

        return services;
    }

    public static WebApplication UseJaCrmPipeline(this WebApplication app)
    {
        if (app.Services.GetRequiredService<IOptions<AppOptions>>().Value.TrustForwardedHeaders)
        {
            var forwarded = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            };

            // The app port is only reachable from the reverse proxy in this mode (see docs/deployment.md).
            forwarded.KnownIPNetworks.Clear();
            forwarded.KnownProxies.Clear();
            app.UseForwardedHeaders(forwarded);
        }

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseSecurityHeaders();

        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                // Hashed bundles never change; index.html and other files must always be revalidated.
                context.Context.Response.Headers.CacheControl = HashedAsset().IsMatch(context.File.Name)
                    ? "public, max-age=31536000, immutable"
                    : "no-cache";
            },
        });

        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseCsrfHeaderCheck();
        return app;
    }

    public static WebApplication MapJaCrmEndpoints(this WebApplication app)
    {
        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).AllowAnonymous().ExcludeFromDescription();

        var api = app.MapGroup("/api");
        api.MapAuthEndpoints();
        api.MapMetaEndpoints();
        api.MapFeatureEndpoints();

        // Unknown API routes are JSON 404s, not the SPA's index.html.
        api.Map("/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found."))
            .AllowAnonymous();

        // Client-side routes fall back to the Angular app.
        app.MapFallbackToFile("index.html").AllowAnonymous();
        return app;
    }

    private static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy =
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
            "font-src 'self'; connect-src 'self'; manifest-src 'self'; object-src 'none'; base-uri 'self'; " +
            "form-action 'self'; frame-ancestors 'none'";
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        await next();
    });

    [GeneratedRegex(@"-[A-Z0-9]{8}\.(js|css|woff2?)$")]
    private static partial Regex HashedAsset();
}
