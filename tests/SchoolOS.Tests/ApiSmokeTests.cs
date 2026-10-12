using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SchoolOS.Tests;

/// <summary>
/// Smoke tests: the API host starts in-memory and its operational endpoints respond.
/// These tests must pass without a database (no DB access on these paths).
/// </summary>
public sealed class ApiSmokeTests
{
    private static readonly WebApplicationFactory<Program> Factory =
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        using var client = Factory.CreateClient();
        using var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_ReturnsOk()
    {
        using var client = Factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
