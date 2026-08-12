using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

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