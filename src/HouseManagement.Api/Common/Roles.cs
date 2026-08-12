namespace HouseManagement.Api.Common;

public static class Roles
{
    // Role values are stored in users.Role and emitted into JWT role claim.
    // Keep these exactly matching existing persisted values to avoid breaking checks.
    public const string Admin = "admin";
    public const string Manager = "manager";
    public const string HouseHelp = "househelp";
}
