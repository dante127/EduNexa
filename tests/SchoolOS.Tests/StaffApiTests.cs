using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SchoolOS.Infrastructure.Identity;
using Xunit;

namespace SchoolOS.Tests;

public sealed class StaffApiTests(SchoolOsAppFactory factory) : IClassFixture<SchoolOsAppFactory>
{
    private const string DefaultPassword = "Password1X";
    private static int _gradeSequence = 400;

    [Fact]
    public async Task ListStaff_Unauthenticated_Returns401()
    {
        using var client = CreateRawClient();

        var response = await client.GetAsync("/api/staff");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_CannotViewOrManageStaff()
    {
        using var teacher = await CreateAuthenticatedClientAsync(SchoolOsRoles.Teacher);

        var list = await teacher.GetAsync("/api/staff");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);

        var create = await teacher.PostAsJsonAsync("/api/staff", new
        {
            firstNameEn = "X",
            lastNameEn = "Y",
            firstNameAr = "س",
            lastNameAr = "ص",
            position = "Teacher",
            hireDate = "2025-09-01",
            contractType = "FullTime"
        });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task CreateStaff_AllocatesSequentialEmployeeNumbers()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);

        var first = await CreateStaffDtoAsync(admin, "Alpha");
        var second = await CreateStaffDtoAsync(admin, "Beta");

        Assert.Matches(@"^EMP-\d{4}$", first.EmployeeNumber);
        Assert.Equal("Active", first.Status);

        var firstNumber = int.Parse(first.EmployeeNumber.Split('-')[1]);
        var secondNumber = int.Parse(second.EmployeeNumber.Split('-')[1]);
        Assert.Equal(firstNumber + 1, secondNumber);

        var detail = await admin.GetAsync($"/api/staff/{first.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var staff = await detail.Content.ReadFromJsonAsync<StaffPayload>();
        Assert.Equal(first.EmployeeNumber, staff!.EmployeeNumber);
    }

    [Fact]
    public async Task CreateStaff_ExplicitDuplicateNumber_Returns409()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var number = $"EMP-X{Guid.NewGuid():N}"[..10];

        var first = await CreateStaffDtoAsync(admin, "First", number);
        Assert.Equal(number, first.EmployeeNumber);

        var duplicate = await CreateStaffDtoRawAsync(admin, "Second", number);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task ListStaff_SearchAndPagingShape()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var staffId = await CreateStaffDtoAsync(admin, $"Zed{suffix}");

        var search = await admin.GetAsync($"/api/staff?search={suffix}&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var page = await search.Content.ReadFromJsonAsync<PagedStaffPayload>();
        Assert.NotNull(page);
        Assert.Equal(1, page!.Page);
        Assert.Equal(10, page.PageSize);
        Assert.True(page.Total >= 1);
        Assert.Contains(page.Items, item => item.Id == staffId.Id);

        var noMatch = await admin.GetAsync("/api/staff?search=zzz-no-such-staff");
        var emptyPage = await noMatch.Content.ReadFromJsonAsync<PagedStaffPayload>();
        Assert.Equal(0, emptyPage!.Total);

        var byStatus = await admin.GetAsync("/api/staff?status=Active");
        Assert.Equal(HttpStatusCode.OK, byStatus.StatusCode);
    }

    [Fact]
    public async Task UpdateStaff_ChangesStatusAndFields()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var created = await CreateStaffDtoAsync(admin, "Updatable");

        var update = await admin.PutAsJsonAsync($"/api/staff/{created.Id}", new
        {
            id = created.Id,
            firstNameEn = "Updated",
            lastNameEn = "Updatable",
            firstNameAr = "محدث",
            lastNameAr = "اختبار",
            position = "Senior Teacher",
            department = "Science",
            hireDate = "2024-09-01",
            contractType = "PartTime",
            status = "Terminated",
            phone = (string?)null,
            email = (string?)null,
            emergencyContact = (string?)null,
            notes = "Left the school."
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<StaffPayload>();
        Assert.Equal("Terminated", updated!.Status);
        Assert.Equal("Science", updated.Department);
        Assert.Equal("Senior Teacher", updated.Position);

        var detail = await admin.GetAsync($"/api/staff/{created.Id}");
        var persisted = await detail.Content.ReadFromJsonAsync<StaffPayload>();
        Assert.Equal("Terminated", persisted!.Status);
    }

    [Fact]
    public async Task LinkUser_OncePerStaff_ConflictSecond_Unlink()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var staff1 = await CreateStaffDtoAsync(admin, "LinkOne");
        var staff2 = await CreateStaffDtoAsync(admin, "LinkTwo");
        var (userId, _) = await CreateUserWithIdAsync(SchoolOsRoles.Teacher);

        var link = await admin.PutAsJsonAsync($"/api/staff/{staff1.Id}/user", new { userId });
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        var linked = await link.Content.ReadFromJsonAsync<StaffPayload>();
        Assert.Equal(userId, linked!.UserId);

        var conflict = await admin.PutAsJsonAsync($"/api/staff/{staff2.Id}/user", new { userId });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var unlink = await admin.DeleteAsync($"/api/staff/{staff1.Id}/user");
        Assert.Equal(HttpStatusCode.OK, unlink.StatusCode);
        var unlinked = await unlink.Content.ReadFromJsonAsync<StaffPayload>();
        Assert.Null(unlinked!.UserId);

        var relink = await admin.PutAsJsonAsync($"/api/staff/{staff1.Id}/user", new { userId });
        Assert.Equal(HttpStatusCode.OK, relink.StatusCode);
    }

    [Fact]
    public async Task LinkUser_UnknownUser_Returns404()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var staff = await CreateStaffDtoAsync(admin, "NoUser");

        var link = await admin.PutAsJsonAsync($"/api/staff/{staff.Id}/user", new { userId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, link.StatusCode);
    }

    [Fact]
    public async Task Assignments_CreateDuplicateListDelete()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var setup = await CreateSetupAsync(admin);
        var subjectId = await CreateSubjectAsync(admin);
        var staffId = await CreateStaffDtoAsync(admin, "Assignable");

        var create = await admin.PostAsJsonAsync("/api/teacher-assignments", new
        {
            academicYearId = setup.YearId,
            staffMemberId = staffId.Id,
            subjectId,
            classSectionId = setup.SectionId
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var assignment = await create.Content.ReadFromJsonAsync<AssignmentPayload>();

        var duplicate = await admin.PostAsJsonAsync("/api/teacher-assignments", new
        {
            academicYearId = setup.YearId,
            staffMemberId = staffId.Id,
            subjectId,
            classSectionId = setup.SectionId
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var list = await admin.GetAsync($"/api/teacher-assignments?staffMemberId={staffId.Id}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var assignments = await list.Content.ReadFromJsonAsync<List<AssignmentPayload>>();
        Assert.NotNull(assignments);
        Assert.Contains(assignments!, item => item.Id == assignment!.Id);

        var delete = await admin.DeleteAsync($"/api/teacher-assignments/{assignment!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var listAfter = await admin.GetAsync($"/api/teacher-assignments?staffMemberId={staffId.Id}");
        var remaining = await listAfter.Content.ReadFromJsonAsync<List<AssignmentPayload>>();
        Assert.Empty(remaining!);
    }

    [Fact]
    public async Task Assignment_SectionYearMismatch_Returns409()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var setupOne = await CreateSetupAsync(admin);
        var setupTwo = await CreateSetupAsync(admin);
        var subjectId = await CreateSubjectAsync(admin);
        var staffId = await CreateStaffDtoAsync(admin, "Mismatched");

        var mismatch = await admin.PostAsJsonAsync("/api/teacher-assignments", new
        {
            academicYearId = setupTwo.YearId,
            staffMemberId = staffId.Id,
            subjectId,
            classSectionId = setupOne.SectionId
        });

        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
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
        var (_, email) = await CreateUserWithIdAsync(role);
        return email;
    }

    private async Task<(Guid Id, string Email)> CreateUserWithIdAsync(string role)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var email = $"staff-{Guid.NewGuid():N}@test.local";
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

        return (user.Id, email);
    }

    private static string NewName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..16];

    private sealed record Setup(Guid YearId, Guid GradeLevelId, Guid SectionId);

    private static async Task<Setup> CreateSetupAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _gradeSequence);

        var yearResponse = await client.PostAsJsonAsync("/api/academic-years", new
        {
            name = NewName("Y"),
            startDate = "2031-09-01",
            endDate = "2032-06-30"
        });
        Assert.Equal(HttpStatusCode.OK, yearResponse.StatusCode);
        var yearId = (await yearResponse.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var gradeResponse = await client.PostAsJsonAsync("/api/grade-levels", new
        {
            sequence,
            nameEn = $"Grade {sequence}",
            nameAr = $"صف {sequence}"
        });
        Assert.Equal(HttpStatusCode.OK, gradeResponse.StatusCode);
        var gradeLevelId = (await gradeResponse.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var sectionResponse = await client.PostAsJsonAsync("/api/sections", new
        {
            academicYearId = yearId,
            gradeLevelId,
            name = "A",
            capacity = 30
        });
        Assert.Equal(HttpStatusCode.OK, sectionResponse.StatusCode);
        var sectionId = (await sectionResponse.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        return new Setup(yearId, gradeLevelId, sectionId);
    }

    private static async Task<Guid> CreateSubjectAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/subjects", new
        {
            code = $"SUB-{Guid.NewGuid():N}"[..12],
            nameEn = "Mathematics",
            nameAr = "رياضيات"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdPayload>())!.Id;
    }

    private static async Task<StaffPayload> CreateStaffDtoAsync(HttpClient client, string lastName, string? employeeNumber = null)
    {
        var response = await CreateStaffDtoRawAsync(client, lastName, employeeNumber);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StaffPayload>())!;
    }

    private static Task<HttpResponseMessage> CreateStaffDtoRawAsync(HttpClient client, string lastName, string? employeeNumber)
    {
        return client.PostAsJsonAsync("/api/staff", new
        {
            employeeNumber,
            firstNameEn = "Test",
            lastNameEn = $"Staff{lastName}"[..Math.Min(110, lastName.Length + 5)],
            firstNameAr = "موظف",
            lastNameAr = "اختبار",
            position = "Teacher",
            department = (string?)null,
            hireDate = "2025-09-01",
            contractType = "FullTime",
            phone = (string?)null,
            email = (string?)null,
            emergencyContact = (string?)null,
            notes = (string?)null
        });
    }

    private sealed record LoginPayload(string AccessToken, int ExpiresInSeconds);

    private sealed record IdPayload(Guid Id);

    private sealed record StaffPayload(
        Guid Id,
        Guid? UserId,
        string EmployeeNumber,
        string Position,
        string? Department,
        string Status);

    private sealed record AssignmentPayload(
        Guid Id,
        Guid AcademicYearId,
        Guid StaffMemberId,
        Guid SubjectId,
        Guid ClassSectionId);

    private sealed record PagedStaffPayload(List<StaffPayload> Items, int Page, int PageSize, int Total);
}
