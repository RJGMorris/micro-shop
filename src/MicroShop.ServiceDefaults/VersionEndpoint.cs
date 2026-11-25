using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace MicroShop.ServiceDefaults;

/// <summary>
/// Reports which build is running. The CD pipeline calls this after a deployment
/// to prove that the NEW version is live, not just that something answered.
/// </summary>
public static class VersionEndpoint
{
    public const string Path = "/version";

    /// <summary>The git commit. The Dockerfile bakes it into the image as APP_VERSION.</summary>
    public static string GetVersion(IConfiguration configuration) =>
        configuration["APP_VERSION"] ?? "local";

    public static void MapVersionEndpoint(this WebApplication app)
    {
        var info = new
        {
            service = app.Environment.ApplicationName,
            version = GetVersion(app.Configuration),
            environment = app.Environment.EnvironmentName,
        };

        app.MapGet(Path, () => Results.Ok(info)).ExcludeFromDescription();
    }
}
