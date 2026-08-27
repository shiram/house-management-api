using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Common.Api;

public static class ApiResponseFactory
{
    public static ApiResponse<T> Create<T>(ControllerBase controller, T? data, string message, int statusCode)
    {
        var requestId = controller.HttpContext?.Items.TryGetValue("RequestId", out var requestIdValue) == true
            ? requestIdValue?.ToString()
            : controller.HttpContext?.TraceIdentifier;

        return new ApiResponse<T>
        {
            StatusCode = statusCode,
            Message = message,
            Data = data,
            RequestId = requestId
        };
    }
}
