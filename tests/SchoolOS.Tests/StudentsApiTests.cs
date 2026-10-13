using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SchoolOS.Infrastructure.Identity;
using Xunit;

namespace SchoolOS.Tests;

public sealed class StudentsApiTests(SchoolOsAppFactory factory) : IClassFixture<SchoolOsAppFactory>
{
    private const string DefaultPassword = "Password1X";
    private static int _gradeSequence = 100;

    [Fact]
    public async Task ListStudents_Unauthenticated_Returns401()
    {
        using var client = CreateRawClient();

        var response = await client.GetAsync("/api/students");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListStudents_AsTeacher_Returns200_ButManage_Returns403()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.Teacher);

        var list = await client.GetAsync("/api/students");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var create = await client.PostAsJsonAsync("/api/guardians", new
        {
            fullNameEn = "X",
            fullNameAr = "X",
            phone = (string?)null,
            altPhone = (string?)null,
            email = (string?)null,
            occupation = (string?)null,
            address = (string?)null
        });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task AdmissionFlow_CreatesStudentWithNumber_AndSequentialNumbers()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var setup = await CreateSetupAsync(client);

        var first = await EnrollStudentAsync(client, setup, "A");
        var second = await EnrollStudentAsync(client, setup, "B");

        Assert.Matches(@"^\d{4}-\d{4}$", first.StudentNumber);
        Assert.StartsWith("2030-", first.StudentNumber);

        var firstSuffix = int.Parse(first.StudentNumber.Split('-')[1]);
        var secondSuffix = int.Parse(second.StudentNumber.Split('-')[1]);
        Assert.Equal(firstSuffix + 1, secondSuffix);

        var detail = await client.GetAsync($"/api/students/{first.StudentId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var student = await detail.Content.ReadFromJsonAsync<StudentDetailPayload>();
        Assert.NotNull(student);
        Assert.Equal("Active", student!.Student.Status);
        Assert.Single(student.Guardians);
        Assert.True(student.Guardians[0].IsPrimary);
        Assert.Single(student.Enrollments);
        Assert.Equal("Enrolled", student.Enrollments[0].Status);

        // Enrolling the same application twice must conflict.
        var duplicate = await client.PostAsJsonAsync(
            $"/api/admission-applications/{first.ApplicationId}/enroll",
            new { classSectionId = setup.SectionId, startDate = "2030-09-05", notes = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Students_SearchAndPagingShape()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var setup = await CreateSetupAsync(client);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var enrolled = await EnrollStudentAsync(client, setup, suffix);

        var search = await client.GetAsync($"/api/students?search={suffix}&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var page = await search.Content.ReadFromJsonAsync<PagedStudentsPayload>();
        Assert.NotNull(page);
        Assert.Equal(1, page!.Page);
        Assert.Equal(10, page.PageSize);
        Assert.True(page.Total >= 1);
        Assert.Contains(page.Items, item => item.Id == enrolled.StudentId);

        var noMatch = await client.GetAsync("/api/students?search=zzz-no-such-student");
        var emptyPage = await noMatch.Content.ReadFromJsonAsync<PagedStudentsPayload>();
        Assert.Equal(0, emptyPage!.Total);
    }

    [Fact]
    public async Task Guardians_LinkAndPrimaryDemotion()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var setup = await CreateSetupAsync(client);
        var student = await EnrollStudentAsync(client, setup, "G");

        var guardian1 = await CreateGuardianAsync(client, "Guardian One");
        var guardian2 = await CreateGuardianAsync(client, "Guardian Two");

        var link1 = await client.PostAsJsonAsync($"/api/students/{student.StudentId}/guardians", new
        {
            guardianId = guardian1,
            relationship = "Mother",
            isPrimary = true,
            isEmergencyContact = false,
            isAuthorizedPickup = true,
            custodyNotes = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, link1.StatusCode);

        var link2 = await client.PostAsJsonAsync($"/api/students/{student.StudentId}/guardians", new
        {
            guardianId = guardian2,
            relationship = "Father",
            isPrimary = true,
            isEmergencyContact = true,
            isAuthorizedPickup = true,
            custodyNotes = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, link2.StatusCode);

        var list = await client.GetAsync($"/api/students/{student.StudentId}/guardians");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var guardians = await list.Content.ReadFromJsonAsync<List<StudentGuardianPayload>>();
        Assert.NotNull(guardians);

        var primaries = guardians!.Where(link => link.IsPrimary).ToList();
        Assert.Single(primaries);
        Assert.Equal(guardian2, primaries[0].GuardianId);
    }

    [Fact]
    public async Task SectionChange_UpdatesOpenEnrollment()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var setup = await CreateSetupAsync(client);
        var student = await EnrollStudentAsync(client, setup, "S");

        var sectionB = await client.PostAsJsonAsync("/api/sections", new
        {
            academicYearId = setup.YearId,
            gradeLevelId = setup.GradeLevelId,
            name = "B",
            capacity = 25
        });
        Assert.Equal(HttpStatusCode.OK, sectionB.StatusCode);
        var sectionBId = (await sectionB.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var change = await client.PostAsJsonAsync(
            $"/api/enrollments/{student.EnrollmentId}/change-section",
            new { classSectionId = sectionBId });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        var enrollments = await client.GetAsync($"/api/students/{student.StudentId}/enrollments");
        var list = await enrollments.Content.ReadFromJsonAsync<List<EnrollmentPayload>>();
        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.Equal(sectionBId, list![0].ClassSectionId);
        Assert.Equal("Enrolled", list[0].Status);
    }

    [Fact]
    public async Task Transfer_ClosesEnrollment_AndSetsStudentStatus()
    {
        using var client = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var setup = await CreateSetupAsync(client);
        var student = await EnrollStudentAsync(client, setup, "T");

        var transfer = await client.PostAsJsonAsync($"/api/enrollments/{student.EnrollmentId}/transfer", new
        {
            endDate = "2031-01-15",
            notes = "Moved to another city"
        });
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        var enrollments = await client.GetAsync($"/api/students/{student.StudentId}/enrollments");
        var list = await enrollments.Content.ReadFromJsonAsync<List<EnrollmentPayload>>();
        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.Equal("Transferred", list![0].Status);
        Assert.Equal("2031-01-15", list[0].EndDate);

        var detail = await client.GetAsync($"/api/students/{student.StudentId}");
        var studentDetail = await detail.Content.ReadFromJsonAsync<StudentDetailPayload>();
        Assert.Equal("Transferred", studentDetail!.Student.Status);
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

        var email = $"students-{Guid.NewGuid():N}@test.local";
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

    private sealed record Setup(Guid YearId, Guid GradeLevelId, Guid SectionId);

    private static async Task<Setup> CreateSetupAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _gradeSequence);

        var yearResponse = await client.PostAsJsonAsync("/api/academic-years", new
        {
            name = NewName("Y"),
            startDate = "2030-09-01",
            endDate = "2031-06-30"
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

    private sealed record EnrolledStudent(Guid StudentId, string StudentNumber, Guid EnrollmentId, Guid ApplicationId);

    private static async Task<EnrolledStudent> EnrollStudentAsync(HttpClient client, Setup setup, string suffix)
    {
        var applicationResponse = await client.PostAsJsonAsync("/api/admission-applications", new
        {
            applicantFirstNameEn = "Test",
            applicantLastNameEn = $"Student{suffix}",
            applicantFirstNameAr = "طالب",
            applicantLastNameAr = $"اختبار{suffix}",
            dateOfBirth = "2015-04-01",
            gender = "Male",
            nationality = (string?)null,
            academicYearId = setup.YearId,
            desiredGradeLevelId = setup.GradeLevelId,
            guardianName = $"Guardian {suffix}",
            guardianPhone = "+963000000",
            guardianEmail = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, applicationResponse.StatusCode);
        var applicationId = (await applicationResponse.Content.ReadFromJsonAsync<IdPayload>())!.Id;

        var approve = await client.PostAsJsonAsync(
            $"/api/admission-applications/{applicationId}/approve",
            new { notes = "Approved in test" });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var enroll = await client.PostAsJsonAsync($"/api/admission-applications/{applicationId}/enroll", new
        {
            classSectionId = setup.SectionId,
            startDate = "2030-09-05",
            notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, enroll.StatusCode);

        var result = await enroll.Content.ReadFromJsonAsync<EnrollmentResultPayload>();
        Assert.NotNull(result);
        return new EnrolledStudent(result!.Student.Id, result.Student.StudentNumber, result.Enrollment.Id, applicationId);
    }

    private static async Task<Guid> CreateGuardianAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/guardians", new
        {
            fullNameEn = name,
            fullNameAr = name,
            phone = "+963000001",
            altPhone = (string?)null,
            email = (string?)null,
            occupation = (string?)null,
            address = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdPayload>())!.Id;
    }

    private sealed record LoginPayload(string AccessToken, int ExpiresInSeconds);

    private sealed record IdPayload(Guid Id);

    private sealed record StudentSummaryPayload(Guid Id, string StudentNumber, string Status);

    private sealed record EnrollmentPayload(Guid Id, string Status, Guid ClassSectionId, string? EndDate);

    private sealed record StudentGuardianPayload(Guid GuardianId, bool IsPrimary);

    private sealed record StudentDetailPayload(StudentSummaryPayload Student, List<StudentGuardianPayload> Guardians, List<EnrollmentPayload> Enrollments);

    private sealed record EnrollmentResultPayload(StudentSummaryPayload Student, EnrollmentPayload Enrollment);

    private sealed record PagedStudentsPayload(List<StudentSummaryPayload> Items, int Page, int PageSize, int Total);
}
