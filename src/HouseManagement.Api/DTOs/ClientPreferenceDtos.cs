namespace HouseManagement.Api.DTOs;

public sealed class PreferredHouseHelpDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string City { get; set; } = null!;
    public IEnumerable<string> Skills { get; set; } = [];
    public DateTimeOffset PreferredAt { get; set; }
}
