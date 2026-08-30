using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IAuditLogService
{
    Task LogAsync(string action, string entityType, int? entityId = null, int? userId = null, string? details = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLog>> GetListAsync(
        string? action = null,
        string? entityType = null,
        int? userId = null,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default);
}
