using HouseManagement.Api.Common.Security;
using Microsoft.Extensions.Configuration;

namespace HouseManagement.Api.Tests;

public sealed class JwtConfigurationTests
{
    [Fact]
    public void GetSigningKey_UsesConfiguredKeyWhenEnvironmentKeyIsNotSet()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "01234567890123456789012345678901"
            })
            .Build();

        Assert.Equal("01234567890123456789012345678901", JwtConfiguration.GetSigningKey(configuration, null));
    }

    [Fact]
    public void GetSigningKey_PrefersNonEmptyEnvironmentKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "01234567890123456789012345678901"
            })
            .Build();

        Assert.Equal("abcdefghijklmnopqrstuvwxyz012345", JwtConfiguration.GetSigningKey(configuration, "abcdefghijklmnopqrstuvwxyz012345"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("PleaseSetASecretKeyInEnv")]
    [InlineData("PleaseChangeThisSecretOrSetEnvVar")]
    [InlineData("too-short")]
    public void IsProductionSafeSigningKey_RejectsUnsafeKeys(string signingKey)
    {
        Assert.False(JwtConfiguration.IsProductionSafeSigningKey(signingKey));
    }

    [Fact]
    public void IsProductionSafeSigningKey_AcceptsLongNonPlaceholderKey()
    {
        Assert.True(JwtConfiguration.IsProductionSafeSigningKey("01234567890123456789012345678901"));
    }
}
