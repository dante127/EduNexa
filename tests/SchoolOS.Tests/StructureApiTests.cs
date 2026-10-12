using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SchoolOS.Infrastructure.Identity;
using Xunit;

namespace SchoolOS.Tests;

public sealed class StructureApiTests(SchoolOsAppFactory factory) : IClassFixture<SchoolOsAppFactory>
{
    private const string DefaultPassword = "Password1X";

    [Fact]
    public async Task GetAcademicYears_Unauthenticated_Returns401()
    {
        using var client = CreateRawClient();

        var response = await client.GetAsync("/api/academic-years");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAcademicYear_AsTeacher_Returns403()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.Teacher);

        var response = await client.PostAsJsonAsync("/api/academic-years", new
        {
            name = NewName("Y"),
            startDate = "2031-01-01",
            endDate = "2031-06-30"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AcademicYearLifecycle_SingleActiveEnforced()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);

        var (yearA, _) = await CreateYearAsync(client);
        var (yearB, _) = await CreateYearAsync(client);

        var activateA = await client.PostAsync($"/api/academic-years/{yearA}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activateA.StatusCode);

        var activateB = await client.PostAsync($"/api/academic-years/{yearB}/activate", null);
        Assert.Equal(HttpStatusCode.Conflict, activateB.StatusCode);

        var closeA = await client.PostAsync($"/api/academic-years/{yearA}/close", null);
        Assert.Equal(HttpStatusCode.OK, closeA.StatusCode);

        var activateBAgain = await client.PostAsync($"/api/academic-years/{yearB}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activateBAgain.StatusCode);

        // Cleanup: leave no active year behind for other tests in this class.
        var cleanup = await client.PostAsync($"/api/academic-years/{yearB}/close", null);
        Assert.Equal(HttpStatusCode.OK, cleanup.StatusCode);
    }

    [Fact]
    public async Task CreateAcademicYear_DuplicateName_Returns409()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);

        var (_, name) = await CreateYearAsync(client);

        var duplicate = await client.PostAsJsonAsync("/api/academic-years", new
        {
            name,
            startDate = "2032-01-01",
            endDate = "2032-06-30"
        });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task CreateAcademicYear_InvalidDates_Returns400()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);

        var response = await client.PostAsJsonAsync("/api/academic-years", new
        {
            name = NewName("Y"),
            startDate = "2033-06-30",
            endDate = "2033-01-01"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_error", await ExtractErrorCodeAsync(response));
    }

    [Fact]
    public async Task AcademicTermLifecycle_BoundsValidated_AndSecondActiveConflict()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);

        var (yearId, _) = await CreateYearAsync(client);
        var activateYear = await client.PostAsync($"/api/academic-years/{yearId}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activateYear.StatusCode);

        var outOfBounds = await client.PostAsJsonAsync($"/api/academic-years/{yearId}/terms", new
        {
            name = "T-out",
            sequence = 1,
            startDate = "2029-01-01",
            endDate = "2029-02-01"
        });
        Assert.Equal(HttpStatusCode.Conflict, outOfBounds.StatusCode);

        var term1 = await client.PostAsJsonAsync($"/api/academic-years/{yearId}/terms", new
        {
            name = "Semester 1",
            sequence = 1,
            startDate = "2030-01-05",
            endDate = "2030-03-01"
        });
        Assert.Equal(HttpStatusCode.OK, term1.StatusCode);
        var term1Id = (await term1.Content.ReadFromJsonAsync<TermPayload>())!.Id;

        var term2 = await client.PostAsJsonAsync($"/api/academic-years/{yearId}/terms", new
        {
            name = "Semester 2",
            sequence = 2,
            startDate = "2030-03-05",
            endDate = "2030-06-20"
        });
        Assert.Equal(HttpStatusCode.OK, term2.StatusCode);
        var term2Id = (await term2.Content.ReadFromJsonAsync<TermPayload>())!.Id;

        var activate1 = await client.PostAsync($"/api/terms/{term1Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activate1.StatusCode);

        var activate2 = await client.PostAsync($"/api/terms/{term2Id}/activate", null);
        Assert.Equal(HttpStatusCode.Conflict, activate2.StatusCode);

        var close1 = await client.PostAsync($"/api/terms/{term1Id}/close", null);
        Assert.Equal(HttpStatusCode.OK, close1.StatusCode);

        var activate2Again = await client.PostAsync($"/api/terms/{term2Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activate2Again.StatusCode);

        // Cleanup.
        await client.PostAsync($"/api/terms/{term2Id}/close", null);
        await client.PostAsync($"/api/academic-years/{yearId}/close", null);
    }

    [Fact]
    public async Task CreateSection_ValidatesCapacity_AndDuplicates()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);

        var (yearId, _) = await CreateYearAsync(client);

        var gradeLevel = await client.PostAsJsonAsync("/api/grade-levels", new
        {
            sequence = 42,
            nameEn = "Grade X",
            nameAr = "الصف العاشر"
        });
        Assert.Equal(HttpStatusCode.OK, gradeLevel.StatusCode);
        var gradeLevelId = (await gradeLevel.Content.ReadFromJsonAsync<GradeLevelPayload>())!.Id;

        var invalid = await client.PostAsJsonAsync("/api/sections", new
        {
            academicYearId = yearId,
            gradeLevelId,
            name = "A",
            capacity = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var valid = await client.PostAsJsonAsync("/api/sections", new
        {
            academicYearId = yearId,
            gradeLevelId,
            name = "A",
            capacity = 25
        });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);

        var duplicate = await client.PostAsJsonAsync("/api/sections", new
        {
            academicYearId = yearId,
            gradeLevelId,
            name = "A",
            capacity = 25
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task SchoolSettings_GetSeeded_AndUpdateRoundTrips()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);

        var get = await client.GetAsync("/api/school-settings");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var settings = await get.Content.ReadFromJsonAsync<SchoolSettingsPayload>();
        Assert.NotNull(settings);
        Assert.Equal("SchoolOS", settings!.NameEn);

        var update = await client.PutAsJsonAsync("/api/school-settings", new
        {
            nameEn = "SchoolOS",
            nameAr = "SchoolOS",
            timezone = "Europe/Berlin",
            defaultLocale = "en",
            currencyCode = "USD",
            address = (string?)null,
            logoPath = (string?)null,
            phone = (string?)null,
            email = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var getAgain = await client.GetAsync("/api/school-settings");
        var updated = await getAgain.Content.ReadFromJsonAsync<SchoolSettingsPayload>();
        Assert.Equal("Europe/Berlin", updated!.Timezone);
    }

    [Fact]
    public async Task CreateSubject_DuplicateCode_Returns409()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);

        var code = $"SUB-{Guid.NewGuid():N}"[..12];

        var first = await client.PostAsJsonAsync("/api/subjects", new
        {
            code,
            nameEn = "Mathematics",
            nameAr = "الرياضيات"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var duplicate = await client.PostAsJsonAsync("/api/subjects", new
        {
            code,
            nameEn = "Mathematics 2",
            nameAr = "الرياضيات 2"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    private HttpClient CreateRawClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string role)
    {
        var email = await CreateUserAsync(role);
        var client = CreateRawClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { emailOrUserName = email, password = DefaultPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var payload = await login.Content.ReadFromJsonAsync<LoginPayload>();
        Assert.NotNull(payload);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload!.AccessToken);
        return client;
    }

    private async Task<string> CreateUserAsync(string role)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var email = $"struct-{Guid.NewGuid():N}@test.local";
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Status = UserStatuses.Active
        };

        var createResult = await userManager.CreateAsync(user, DefaultPassword);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));

        var roleResult = await userManager.AddToRoleAsync(user, role);
        Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));

        return email;
    }

    private static string NewName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..16];

    private static async Task<(Guid Id, string Name)> CreateYearAsync(HttpClient client)
    {
        var name = NewName("Y");
        var response = await client.PostAsJsonAsync("/api/academic-years", new
        {
            name,
            startDate = "2030-01-01",
            endDate = "2030-06-30"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AcademicYearPayload>();
        Assert.NotNull(payload);
        return (payload!.Id, name);
    }

    private static async Task<string?> ExtractErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private sealed record LoginPayload(string AccessToken, int ExpiresInSeconds);

    private sealed record AcademicYearPayload(Guid Id, string Name, string Status);

    private sealed record TermPayload(Guid Id, string Status);

    private sealed record GradeLevelPayload(Guid Id, int Sequence);

    private sealed record SchoolSettingsPayload(Guid Id, string NameEn, string Timezone);
}
