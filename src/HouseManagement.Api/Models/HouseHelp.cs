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
    public string? Bio { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? Languages { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? NationalIdLast4 { get; set; }
    public HouseHelpVerificationStatus VerificationStatus { get; set; } = HouseHelpVerificationStatus.Unverified;
    public string? ProfileImageStorageKey { get; set; }
    public string? ProfileImageContentType { get; set; }
    public long? ProfileImageSizeBytes { get; set; }
    public DateTimeOffset? ProfileImageUpdatedAt { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<HouseHelpSkill> Skills { get; set; } = new();
    public List<HouseHelpAvailability> Availabilities { get; set; } = new();
    public List<HouseHelpAvailabilityException> AvailabilityExceptions { get; set; } = new();
}