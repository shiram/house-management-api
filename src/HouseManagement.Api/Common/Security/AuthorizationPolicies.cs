namespace HouseManagement.Api.Common.Security;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string ManagerOrAdmin = "ManagerOrAdmin";
    public const string HouseHelpOnly = "HouseHelpOnly";

    // Role values remain lowercase for compatibility with existing users and JWT claims.
    public const string AdminRole = "admin";
    public const string ManagerRole = "manager";
    public const string HouseHelpRole = "househelp";
}
