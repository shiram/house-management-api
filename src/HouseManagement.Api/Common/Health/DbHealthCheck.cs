using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HouseManagement.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Common.Health
{
    public class DbHealthCheck : IHealthCheck
    {
        private readonly HouseContext _context;

        public DbHealthCheck(HouseContext context)
        {
            _context = context;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await _context.Database.CanConnectAsync(cancellationToken))
                {
                    return HealthCheckResult.Healthy("Database reachable");
                }

                return HealthCheckResult.Unhealthy("Database not reachable");
            }
            catch (System.Exception ex)
            {
                return HealthCheckResult.Unhealthy("Database check failed", ex);
            }
        }
    }
}