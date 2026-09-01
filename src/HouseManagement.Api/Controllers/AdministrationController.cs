using HouseManagement.Api.Common;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdministrationController : ControllerBase
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        Roles.Admin,
        Roles.Manager,
        Roles.HouseHelp
    };

    private readonly HouseContext _db;
    private readonly IAuditLogService _auditLogs;

    public AdministrationController(HouseContext db, IAuditLogService auditLogs)
    {
        _db = db;
        _auditLogs = auditLogs;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var query = _db.Users
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .AsQueryable();

        if (page.HasValue && pageSize.HasValue && page.Value > 0 && pageSize.Value > 0)
        {
            var boundedPageSize = Math.Min(pageSize.Value, 100);
            query = query
                .Skip((page.Value - 1) * boundedPageSize)
                .Take(boundedPageSize);
        }

        var users = await query
            .Select(user => new UserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLogin = user.LastLogin
            })
            .ToListAsync();

        var response = ApiResponseFactory.Create(this, users, "Users retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new UserDto
            {
                Id = item.Id,
                UserName = item.UserName,
                Email = item.Email,
                Role = item.Role,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
                LastLogin = item.LastLogin
            })
            .SingleOrDefaultAsync();

        if (user == null)
        {
            return NotFound();
        }

        var response = ApiResponseFactory.Create(this, user, "User retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateUserRoleRequest request)
    {
        if (!ModelState.IsValid) return ValidationResponseFactory.Create(this, ModelState);

        var normalizedRole = request.Role.Trim().ToLowerInvariant();
        if (!AllowedRoles.Contains(normalizedRole))
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                $"Role must be one of: {string.Join(", ", AllowedRoles)}.",
                StatusCodes.Status400BadRequest));
        }

        var user = await _db.Users.SingleOrDefaultAsync(item => item.Id == id);
        if (user == null)
        {
            return NotFound();
        }

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (int.TryParse(subject, out var currentUserId) && currentUserId == id && !string.Equals(normalizedRole, Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                "Admins cannot change their own role away from admin.",
                StatusCodes.Status400BadRequest));
        }

        var previousRole = user.Role;
        user.Role = normalizedRole;
        await _db.SaveChangesAsync();
        await _auditLogs.LogAsync(
            AuditEventTypes.UserRoleChanged,
            nameof(User),
            entityId: user.Id,
            userId: GetAuthenticatedUserId(),
            details: $"{previousRole} -> {normalizedRole}");

        var dto = new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLogin = user.LastLogin
        };

        var response2 = ApiResponseFactory.Create(this, dto, "User role updated", StatusCodes.Status200OK);
        return Ok(response2);
    }

    [HttpPut("{id:int}/activate")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool active = true)
    {
        var user = await _db.Users.SingleOrDefaultAsync(item => item.Id == id);
        if (user == null)
        {
            return NotFound();
        }

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!active && int.TryParse(subject, out var currentUserId) && currentUserId == id)
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                "Admins cannot deactivate their own account.",
                StatusCodes.Status400BadRequest));
        }

        var previousActive = user.IsActive;
        user.IsActive = active;
        await _db.SaveChangesAsync();
        await _auditLogs.LogAsync(
            AuditEventTypes.UserActivated,
            nameof(User),
            entityId: user.Id,
            userId: GetAuthenticatedUserId(),
            details: $"{previousActive} -> {active}");

        var dto = new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLogin = user.LastLogin
        };

        var response = ApiResponseFactory.Create(this, dto, "User status updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    private int? GetAuthenticatedUserId()
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(subject, out var userId) ? userId : null;
    }
}
