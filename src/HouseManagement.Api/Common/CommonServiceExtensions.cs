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
            // Placeholder for future common registrations: ProblemDetails, API conventions, request ID, etc.
            return services;
        }
    }
}