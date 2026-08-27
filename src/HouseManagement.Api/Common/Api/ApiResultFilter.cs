using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HouseManagement.Api.Common.Api
{
    public class ApiResultFilter : IAsyncResultFilter
    {
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            // Don't wrap ProblemDetails or already-wrapped responses
            if (context.Result is ObjectResult obj)
            {
                if (obj.Value is ProblemDetails || IsApiResponse(obj.Value))
                {
                    await next();
                    return;
                }

                var wrapped = new ApiResponse<object>
                {
                    StatusCode = obj.StatusCode ?? 200,
                    Message = obj.StatusCode >= 400 ? "Error" : "OK",
                    Data = obj.Value
                };

                context.Result = new ObjectResult(wrapped) { StatusCode = obj.StatusCode };
            }

            await next();
        }

        private static bool IsApiResponse(object? value)
        {
            var type = value?.GetType();
            return type is not null
                && type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(ApiResponse<>);
        }
    }
}