using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogs;

    public AuditLogsController(IAuditLogService auditLogs)
    {
        _auditLogs = auditLogs;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] int? userId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var logs = await _auditLogs.GetListAsync(action, entityType, userId, page, pageSize, cancellationToken);
        var dtos = logs.Select(log => new DTOs.AuditLogDto
        {
            Id = log.Id,
            Action = log.Action,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            UserId = log.UserId,
            Details = log.Details,
            CreatedAt = log.CreatedAt
        });

        var response = ApiResponseFactory.Create(this, dtos, "Audit logs retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }
}
