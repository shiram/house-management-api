using HouseManagement.Api.Common.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace HouseManagement.Api.Common
{
    public static class CommonServiceExtensions
    {
        /// <summary>
        /// Register common API services and conventions here.
        /// Kept minimal for T010 to avoid changing existing behavior.
        /// </summary>
        public static IServiceCollection AddCommonServices(this IServiceCollection services)
        {
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var envelope = ValidationResponseFactory.CreateEnvelope(context.HttpContext, context.ModelState);
                    return new BadRequestObjectResult(envelope);
                };
            });

            return services;
        }
    }
}