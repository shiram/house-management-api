using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using HouseManagement.Api.Data;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly HouseContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;

    public AuthController(HouseContext db, IPasswordHasher hasher, ITokenService tokens)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (!ModelState.IsValid) return ValidationResponseFactory.Create(this, ModelState);

        if (await _db.Users.AnyAsync(u => u.Email == req.Email || u.UserName == req.UserName))
        {
            return BadRequest(new { error = "User with that email or username already exists" });
        }

        var user = new User
        {
            UserName = req.UserName,
            Email = req.Email,
            PasswordHash = _hasher.Hash(req.Password),
            // Force default role for self-registered users. Elevated roles must be assigned by an admin.
            Role = "househelp"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _tokens.CreateToken(user);

        var response = ApiResponseFactory.Create(this, new AuthResponse
        {
            Token = token,
            UserName = user.UserName,
            Email = user.Email,
            Role = user.Role
        }, "User registered successfully", StatusCodes.Status200OK);

        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (!ModelState.IsValid) return ValidationResponseFactory.Create(this, ModelState);

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == req.Email);
        if (user == null) return Unauthorized(new { error = "Invalid credentials" });

        if (!_hasher.Verify(user.PasswordHash, req.Password))
            return Unauthorized(new { error = "Invalid credentials" });

        if (!user.IsActive)
            return Unauthorized(new { error = "Account is deactivated" });

        user.LastLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = _tokens.CreateToken(user);
        var response = ApiResponseFactory.Create(this, new AuthResponse
        {
            Token = token,
            UserName = user.UserName,
            Email = user.Email,
            Role = user.Role
        }, "Login successful", StatusCodes.Status200OK);

        return Ok(response);
    }
}