using System;
using System.Collections.Generic;

namespace HouseManagement.Api.Models;

public class HouseHelp
{
    public int Id { get; set; }

    // link to authentication user when this househelp has an account
    public int? UserId { get; set; }
    public User? User { get; set; }

    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string City { get; set; } = null!;
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<HouseHelpSkill> Skills { get; set; } = new();
    public List<HouseHelpAvailability> Availabilities { get; set; } = new();
    public List<HouseHelpAvailabilityException> AvailabilityExceptions { get; set; } = new();
}