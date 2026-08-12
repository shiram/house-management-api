using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace HouseManagement.Api.Common.Middleware
{
    /// <summary>
    /// Global exception handler middleware that returns ProblemDetails for unexpected errors.
    /// Non-breaking: returns HTTP 500 for unhandled exceptions and includes the RequestId for correlation.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var requestId = context.Items.ContainsKey("RequestId") ? context.Items["RequestId"]?.ToString() : context.TraceIdentifier;

                // Log the exception with RequestId
                Log.Error(ex, "Unhandled exception occurred while processing request {RequestId}", requestId);

                // Prepare ProblemDetails
                var problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.com/500",
                    Title = "An unexpected error occurred.",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = context.RequestServices.GetService(typeof(Microsoft.Extensions.Hosting.IHostEnvironment)) is Microsoft.Extensions.Hosting.IHostEnvironment env && env.IsDevelopment()
                        ? ex.ToString()
                        : "An unexpected error occurred. Please contact support with the provided request id.",
                    Extensions = { ["requestId"] = requestId ?? string.Empty }
                };

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";

                // Serialize ProblemDetails using System.Text.Json
                var json = System.Text.Json.JsonSerializer.Serialize(problem, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                await context.Response.WriteAsync(json);
            }
        }
    }
}