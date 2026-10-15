using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolOS.Infrastructure.Identity;
using SchoolOS.Infrastructure.Persistence;
using Xunit;

namespace SchoolOS.Tests;

public sealed class StudentDocumentsApiTests(SchoolOsAppFactory factory) : IClassFixture<SchoolOsAppFactory>
{
    private const string DefaultPassword = "Password1X";
    private static int _gradeSequence = 300;

    [Fact]
    public async Task ListDocuments_Unauthenticated_Returns401()
    {
        using var client = CreateRawClient();

        var response = await client.GetAsync($"/api/students/{Guid.NewGuid()}/documents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_CanListAndDownload_ButCannotUpload()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var studentId = await CreateEnrolledStudentAsync(admin);
        var upload = await UploadDocumentAsync(admin, studentId, PdfBytes());
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var document = await upload.Content.ReadFromJsonAsync<DocumentPayload>();

        using var teacher = await CreateAuthenticatedClientAsync(SchoolOsRoles.Teacher);

        var list = await teacher.GetAsync($"/api/students/{studentId}/documents");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var download = await teacher.GetAsync($"/api/students/{studentId}/documents/{document!.Id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);

        var forbidden = await UploadDocumentAsync(teacher, studentId, PdfBytes());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Upload_List_Download_Roundtrip()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var studentId = await CreateEnrolledStudentAsync(admin);
        var bytes = PdfBytes();

        var upload = await UploadDocumentAsync(admin, studentId, bytes);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var document = await upload.Content.ReadFromJsonAsync<DocumentPayload>();

        Assert.NotNull(document);
        Assert.Equal("BirthCertificate", document!.DocumentType);
        Assert.Equal("birth-certificate.pdf", document.FileName);
        Assert.Equal("application/pdf", document.MimeType);
        Assert.Equal(bytes.Length, document.SizeBytes);
        Assert.Equal("Pending", document.VerificationStatus);
        Assert.Null(document.VerifiedAt);

        var list = await admin.GetAsync($"/api/students/{studentId}/documents");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var documents = await list.Content.ReadFromJsonAsync<List<DocumentPayload>>();
        Assert.NotNull(documents);
        Assert.Single(documents!);
        Assert.Equal(document.Id, documents![0].Id);

        var download = await admin.GetAsync($"/api/students/{studentId}/documents/{document.Id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);
        var downloaded = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal(bytes, downloaded);
    }

    [Fact]
    public async Task Upload_RejectsDisallowedType()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var studentId = await CreateEnrolledStudentAsync(admin);

        var upload = await UploadDocumentAsync(
            admin,
            studentId,
            System.Text.Encoding.ASCII.GetBytes("plain text is not a document"),
            fileName: "notes.txt",
            contentType: "text/plain");

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    [Fact]
    public async Task Upload_RejectsOversizedFile()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var studentId = await CreateEnrolledStudentAsync(admin);

        var oversized = new byte[(10 * 1024 * 1024) + 1];
        var upload = await UploadDocumentAsync(admin, studentId, oversized);

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    [Fact]
    public async Task Verify_TransitionsOnce_ThenConflicts()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var studentId = await CreateEnrolledStudentAsync(admin);

        var upload = await UploadDocumentAsync(admin, studentId, PdfBytes());
        var document = await upload.Content.ReadFromJsonAsync<DocumentPayload>();

        var verify = await admin.PostAsJsonAsync(
            $"/api/students/{studentId}/documents/{document!.Id}/verify",
            new { status = "Verified", notes = "Checked against original." });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var verified = await verify.Content.ReadFromJsonAsync<DocumentPayload>();
        Assert.Equal("Verified", verified!.VerificationStatus);
        Assert.NotNull(verified.VerifiedAt);
        Assert.NotNull(verified.VerifiedByUserId);

        var reVerify = await admin.PostAsJsonAsync(
            $"/api/students/{studentId}/documents/{document.Id}/verify",
            new { status = "Rejected", notes = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, reVerify.StatusCode);

        var upload2 = await UploadDocumentAsync(admin, studentId, PdfBytes(), fileName: "second.pdf");
        var document2 = await upload2.Content.ReadFromJsonAsync<DocumentPayload>();
        var reject = await admin.PostAsJsonAsync(
            $"/api/students/{studentId}/documents/{document2!.Id}/verify",
            new { status = "Rejected", notes = "Unreadable scan." });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var rejected = await reject.Content.ReadFromJsonAsync<DocumentPayload>();
        Assert.Equal("Rejected", rejected!.VerificationStatus);
    }

    [Fact]
    public async Task Delete_SoftDeletesDocument()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var studentId = await CreateEnrolledStudentAsync(admin);

        var upload = await UploadDocumentAsync(admin, studentId, PdfBytes());
        var document = await upload.Content.ReadFromJsonAsync<DocumentPayload>();

        var delete = await admin.DeleteAsync($"/api/students/{studentId}/documents/{document!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var list = await admin.GetAsync($"/api/students/{studentId}/documents");
        var documents = await list.Content.ReadFromJsonAsync<List<DocumentPayload>>();
        Assert.NotNull(documents);
        Assert.Empty(documents!);

        var download = await admin.GetAsync($"/api/students/{studentId}/documents/{document.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);

        // The row survives as a soft-deleted record.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.StudentDocuments.AsNoTracking().SingleAsync(candidate => candidate.Id == document.Id);
        Assert.NotNull(row.DeletedAt);
    }

    [Fact]
    public async Task Documents_AreScopedToStudent()
    {
        using var admin = await CreateAuthenticatedClientAsync(SchoolOsRoles.SchoolAdmin);
        var studentA = await CreateEnrolledStudentAsync(admin);
        var studentB = await CreateEnrolledStudentAsync(admin);

        var upload = await UploadDocumentAsync(admin, studentA, PdfBytes());
        var document = await upload.Content.ReadFromJsonAsync<DocumentPayload>();

        var listB = await admin.GetAsync($"/api/students/{studentB}/documents");
        var documentsB = await listB.Content.ReadFromJsonAsync<List<DocumentPayload>>();
        Assert.Empty(documentsB!);

        var downloadViaB = await admin.GetAsync($"/api/students/{studentB}/documents/{document!.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, downloadViaB.StatusCode);

        var verifyViaB = await admin.PostAsJsonAsync(
            $"/api/students/{studentB}/documents/{document.Id}/verify",
            new { status = "Verified", notes = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, verifyViaB.StatusCode);
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

        var email = $"documents-{Guid.NewGuid():N}@test.local";
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

    private static byte[] PdfBytes()
    {
        var header = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n");
        var body = new byte[512];
        Random.Shared.NextBytes(body);

        var bytes = new byte[header.Length + body.Length];
        header.CopyTo(bytes, 0);
        body.CopyTo(bytes, header.Length);
        return bytes;
    }

    private static async Task<HttpResponseMessage> UploadDocumentAsync(
        HttpClient client,
        Guid studentId,
        byte[] bytes,
        string fileName = "birth-certificate.pdf",
        string contentType = "application/pdf",
        string documentType = "BirthCertificate")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent(documentType), "documentType");
        return await client.PostAsync($"/api/students/{studentId}/documents", form);
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

    private static async Task<Guid> CreateEnrolledStudentAsync(HttpClient client)
    {
        var setup = await CreateSetupAsync(client);

        var applicationResponse = await client.PostAsJsonAsync("/api/admission-applications", new
        {
            applicantFirstNameEn = "Doc",
            applicantLastNameEn = $"Student{Guid.NewGuid():N}"[..20],
            applicantFirstNameAr = "طالب",
            applicantLastNameAr = "اختبار",
            dateOfBirth = "2015-04-01",
            gender = "Male",
            nationality = (string?)null,
            academicYearId = setup.YearId,
            desiredGradeLevelId = setup.GradeLevelId,
            guardianName = "Guardian Docs",
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
        return result!.Student.Id;
    }

    private sealed record LoginPayload(string AccessToken, int ExpiresInSeconds);

    private sealed record IdPayload(Guid Id);

    private sealed record StudentSummaryPayload(Guid Id, string StudentNumber, string Status);

    private sealed record EnrollmentShortPayload(Guid Id, string Status);

    private sealed record EnrollmentResultPayload(StudentSummaryPayload Student, EnrollmentShortPayload Enrollment);

    private sealed record DocumentPayload(
        Guid Id,
        string DocumentType,
        string FileName,
        string MimeType,
        long SizeBytes,
        string VerificationStatus,
        Guid? VerifiedByUserId,
        DateTimeOffset? VerifiedAt);
}
