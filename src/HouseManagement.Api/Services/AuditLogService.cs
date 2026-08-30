using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class AuditLogService : IAuditLogService
{
    private readonly HouseContext _db;

    public AuditLogService(HouseContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string action, string entityType, int? entityId = null, int? userId = null, string? details = null, CancellationToken cancellationToken = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = action.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId,
            UserId = userId,
            Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim()
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> GetListAsync(
        string? action = null,
        string? entityType = null,
        int? userId = null,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim();
            query = query.Where(log => log.Action == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var normalizedEntityType = entityType.Trim();
            query = query.Where(log => log.EntityType == normalizedEntityType);
        }

        if (userId.HasValue)
        {
            query = query.Where(log => log.UserId == userId.Value);
        }

        query = query
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id);

        if (page.HasValue && pageSize.HasValue && page.Value > 0 && pageSize.Value > 0)
        {
            var boundedPageSize = Math.Min(pageSize.Value, 100);
            query = query
                .Skip((page.Value - 1) * boundedPageSize)
                .Take(boundedPageSize);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
