using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HouseManagement.Api.Common.Api
{
    public class ValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var modelState = context.ModelState;
            if (modelState.IsValid) return;

            var errors = modelState
                .Where(kv => kv.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    kv => kv.Key ?? string.Empty,
                    kv => kv.Value!.Errors
                        .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? (e.Exception?.Message ?? string.Empty) : e.ErrorMessage)
                        .ToArray()
                );

            var response = new ApiResponse<object>
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Validation failed",
                Data = new { Errors = errors }
            };

            context.Result = new BadRequestObjectResult(response);
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}