namespace HouseManagement.Api.Models;

public class HouseHelpSkill
{
    public int Id { get; set; }
    public int HouseHelpId { get; set; }
    public HouseHelp HouseHelp { get; set; } = null!;

    // For now, reference service by name to avoid coupling to Service module
    public string ServiceName { get; set; } = null!;
}