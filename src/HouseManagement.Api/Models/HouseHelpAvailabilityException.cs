namespace HouseManagement.Api.Models;

public class HouseHelpAvailabilityException
{
    public int Id { get; set; }
    public int HouseHelpId { get; set; }
    public HouseHelp HouseHelp { get; set; } = null!;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string? Reason { get; set; }
    public bool IsActive { get; set; } = true;
}
