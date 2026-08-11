using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace HouseManagement.Api.Common.Middleware
{
    /// <summary>
    /// Adds or propagates a request ID for correlation and pushes it into the Serilog LogContext.
    /// Sets the X-Request-Id response header and stores the ID in HttpContext.Items["RequestId"].
    /// Non-breaking: if client supplies X-Request-Id it will be respected.
    /// </summary>
    public class RequestIdMiddleware
    {
        private readonly RequestDelegate _next;

        public RequestIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            string requestId = context.Request.Headers.ContainsKey("X-Request-Id") && !string.IsNullOrWhiteSpace(context.Request.Headers["X-Request-Id"]) 
                ? context.Request.Headers["X-Request-Id"].ToString() 
                : Guid.NewGuid().ToString("D");

            // Make it available via TraceIdentifier and Items
            try { context.TraceIdentifier = requestId; } catch { /* ignore if not settable */ }
            context.Items["RequestId"] = requestId;

            // Ensure response header is set
            context.Response.OnStarting(state => {
                var http = (HttpContext)state!;
                if (!http.Response.Headers.ContainsKey("X-Request-Id"))
                {
                                    http.Response.Headers.Append("X-Request-Id", requestId);
                }
                return Task.CompletedTask;
            }, context);

            // Push into Serilog context for structured logging
            using (LogContext.PushProperty("RequestId", requestId))
            {
                await _next(context);
            }
        }
    }
}