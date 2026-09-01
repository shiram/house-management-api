using Microsoft.AspNetCore.Http;

namespace HouseManagement.Api.Common;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var response = (HttpResponse)state;
            response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            response.Headers.TryAdd("X-Frame-Options", "DENY");
            response.Headers.TryAdd("Referrer-Policy", "no-referrer");
            response.Headers.TryAdd("Permissions-Policy", "camera=(), geolocation=(), microphone=()");

            if (response.ContentType == null ||
                !response.ContentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                response.Headers.TryAdd(
                    "Content-Security-Policy",
                    "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'none'");
            }

            return Task.CompletedTask;
        }, context.Response);

        await _next(context);
    }
}
