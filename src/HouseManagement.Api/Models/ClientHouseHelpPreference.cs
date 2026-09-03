namespace HouseManagement.Api.Models;

public class ClientHouseHelpPreference
{
    public int Id { get; set; }

    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public int HouseHelpId { get; set; }
    public HouseHelp HouseHelp { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
