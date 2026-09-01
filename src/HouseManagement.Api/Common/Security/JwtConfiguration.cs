namespace HouseManagement.Api.Common.Security;

public static class JwtConfiguration
{
    private const string FallbackSigningKey = "PleaseSetASecretKeyInEnv";
    private const string DevelopmentPlaceholderSigningKey = "PleaseChangeThisSecretOrSetEnvVar";

    public static string GetSigningKey(IConfiguration configuration)
    {
        return GetSigningKey(configuration, Environment.GetEnvironmentVariable("JWT_KEY"));
    }

    public static string GetSigningKey(IConfiguration configuration, string? environmentKey)
    {
        if (!string.IsNullOrWhiteSpace(environmentKey))
        {
            return environmentKey;
        }

        return configuration["Jwt:Key"] ?? FallbackSigningKey;
    }

    public static bool IsProductionSafeSigningKey(string signingKey)
    {
        return !string.IsNullOrWhiteSpace(signingKey) &&
               signingKey.Length >= 32 &&
               !string.Equals(signingKey, FallbackSigningKey, StringComparison.Ordinal) &&
               !string.Equals(signingKey, DevelopmentPlaceholderSigningKey, StringComparison.Ordinal);
    }
}
