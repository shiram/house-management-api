using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HouseManagement.Api.Tests;

public sealed class SecurityHeadersIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityHeadersIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ApiResponses_IncludeSecurityHeaders()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeader(response, "X-Frame-Options", "DENY");
        AssertHeader(response, "Referrer-Policy", "no-referrer");
        AssertHeader(response, "Permissions-Policy", "camera=(), geolocation=(), microphone=()");
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task CorsPolicy_AllowsConfiguredDevelopmentOriginOnly()
    {
        var client = _factory.CreateClient();
        var allowedRequest = new HttpRequestMessage(HttpMethod.Options, "/api/services");
        allowedRequest.Headers.Add("Origin", "http://localhost:4200");
        allowedRequest.Headers.Add("Access-Control-Request-Method", "GET");

        var allowedResponse = await client.SendAsync(allowedRequest);

        Assert.Equal(HttpStatusCode.NoContent, allowedResponse.StatusCode);
        AssertHeader(allowedResponse, "Access-Control-Allow-Origin", "http://localhost:4200");

        var rejectedRequest = new HttpRequestMessage(HttpMethod.Options, "/api/services");
        rejectedRequest.Headers.Add("Origin", "https://untrusted.example");
        rejectedRequest.Headers.Add("Access-Control-Request-Method", "GET");

        var rejectedResponse = await client.SendAsync(rejectedRequest);

        Assert.False(rejectedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static void AssertHeader(HttpResponseMessage response, string name, string expectedValue)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values));
        Assert.Contains(expectedValue, values);
    }
}
