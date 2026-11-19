using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Simulation.API.Services;

public sealed class AccessKeyMiddleware
{
    private readonly RequestDelegate next;
    private readonly SimulationOptions options;

    public AccessKeyMiddleware(RequestDelegate next, IOptions<SimulationOptions> options)
    {
        this.next = next;
        this.options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var required = RequiredKey(context.Request.Path);
        if (required is null || HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }
        var supplied = Supplied(context.Request);
        if (supplied is null || !Matches(supplied, required))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        await next(context);
    }

    private string? RequiredKey(PathString path)
    {
        if (path.StartsWithSegments("/api/ground-truth")
            || path.StartsWithSegments("/hubs/ground-truth")
            || path.StartsWithSegments("/api/simulation"))
        {
            return options.GroundTruthKey;
        }
        if (path.StartsWithSegments("/api/observations") || path.StartsWithSegments("/hubs/observations"))
        {
            return options.AiKey;
        }
        return null;
    }

    private static string? Supplied(HttpRequest request)
    {
        var header = request.Headers["X-Access-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(header))
        {
            return header;
        }
        var authorization = request.Headers.Authorization.FirstOrDefault();
        if (authorization is not null && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..];
        }
        var query = request.Query["access_token"].FirstOrDefault();
        return string.IsNullOrEmpty(query) ? null : query;
    }

    private static bool Matches(string supplied, string required) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(required));
}
