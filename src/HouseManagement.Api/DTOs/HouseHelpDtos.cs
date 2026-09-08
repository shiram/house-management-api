using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using HouseManagement.Api.Models;
using Microsoft.AspNetCore.Http;

namespace HouseManagement.Api.DTOs;

public class HouseHelpDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string City { get; set; } = null!;
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public IEnumerable<string> Skills { get; set; } = new List<string>();
}

public class HouseHelpProfileDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
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
    public string VerificationStatus { get; set; } = null!;
    public string? ProfileImageContentType { get; set; }
    public long? ProfileImageSizeBytes { get; set; }
    public DateTimeOffset? ProfileImageUpdatedAt { get; set; }
    public bool IsActive { get; set; }
    public IEnumerable<string> Skills { get; set; } = new List<string>();
}

public class OwnHouseHelpProfileDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string City { get; set; } = null!;
    public string? Address { get; set; }
    public string? Bio { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? Languages { get; set; }
    public string VerificationStatus { get; set; } = null!;
    public string? ProfileImageContentType { get; set; }
    public long? ProfileImageSizeBytes { get; set; }
    public DateTimeOffset? ProfileImageUpdatedAt { get; set; }
    public bool IsActive { get; set; }
    public IEnumerable<string> Skills { get; set; } = new List<string>();
}

public class PublicHouseHelpDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string City { get; set; } = null!;
    public string? Bio { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? Languages { get; set; }
    public string VerificationStatus { get; set; } = null!;
    public string? ProfileImageUrl { get; set; }
    public IEnumerable<string> Skills { get; set; } = new List<string>();
}

public class UploadHouseHelpProfileImageRequest
{
    [Required]
    public IFormFile? File { get; set; }
}

public class CreateHouseHelpRequest
{
    public int? UserId { get; set; }

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = null!;

    [Required]
    [Phone]
    public string Phone { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string City { get; set; } = null!;

    [StringLength(250)]
    public string? Address { get; set; }

    // List of services/skills this househelp is eligible for (service names)
    public IEnumerable<string>? Skills { get; set; }
}

public class UpdateHouseHelpRequest
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = null!;

    [Required]
    [Phone]
    public string Phone { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string City { get; set; } = null!;

    [StringLength(250)]
    public string? Address { get; set; }

    public IEnumerable<string>? Skills { get; set; }
}

public class UpdateOwnHouseHelpProfileRequest
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = null!;

    [Required]
    [Phone]
    public string Phone { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string City { get; set; } = null!;

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(1000)]
    public string? Bio { get; set; }

    [Range(0, 80)]
    public int? YearsOfExperience { get; set; }

    [StringLength(500)]
    public string? Languages { get; set; }
}

public class UpdateHouseHelpProfileRequest : UpdateOwnHouseHelpProfileRequest
{
    [StringLength(200)]
    public string? EmergencyContactName { get; set; }

    [StringLength(32)]
    [Phone]
    public string? EmergencyContactPhone { get; set; }

    [StringLength(4, MinimumLength = 4)]
    public string? NationalIdLast4 { get; set; }

    [Required]
    public HouseHelpVerificationStatus VerificationStatus { get; set; }
}