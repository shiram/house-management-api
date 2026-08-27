using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HouseManagement.Api.Common.Api;

public static class ValidationResponseFactory
{
    public static BadRequestObjectResult Create(ControllerBase controller, ModelStateDictionary modelState)
    {
        return new BadRequestObjectResult(CreateEnvelope(controller.HttpContext, modelState));
    }

    public static ApiResponse<Dictionary<string, string[]>> CreateEnvelope(HttpContext? httpContext, ModelStateDictionary modelState)
    {
        var requestId = GetRequestId(httpContext);

        var errors = modelState
            .Where(entry => entry.Value is not null && entry.Value.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The value is invalid." : error.ErrorMessage)
                    .ToArray());

        return new ApiResponse<Dictionary<string, string[]>>
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Message = "Validation failed",
            Data = errors,
            RequestId = requestId
        };
    }

    private static string? GetRequestId(HttpContext? httpContext)
    {
        if (httpContext == null)
        {
            return null;
        }

        return httpContext.Items.TryGetValue("RequestId", out var requestIdValue)
            ? requestIdValue?.ToString()
            : httpContext.TraceIdentifier;
    }
}
