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
                if (obj.Value is ProblemDetails || obj.Value is ApiResponse<object>)
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
    }
}