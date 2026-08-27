namespace HouseManagement.Api.Models;

public class HouseHelpAvailability
{
    public int Id { get; set; }
    public int HouseHelpId { get; set; }
    public HouseHelp HouseHelp { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsActive { get; set; } = true;
}
