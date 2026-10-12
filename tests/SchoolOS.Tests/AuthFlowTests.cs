using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SchoolOS.Infrastructure.Identity;
using Xunit;

namespace SchoolOS.Tests;

public sealed class AuthFlowTests(SchoolOsAppFactory factory) : IClassFixture<SchoolOsAppFactory>
{
    private const string RefreshCookieName = "schoolos_rt";
    private const string DefaultPassword = "Password1X";

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAccessTokenWithRoleAndPermissionClaims()
    {
        var email = await CreateUserAsync(SchoolOsRoles.Teacher);
        using var client = CreateClient();

        var response = await LoginAsync(client, email, DefaultPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponsePayload>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.True(payload.ExpiresInSeconds > 0);
        Assert.NotNull(GetRefreshCookie(response));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(payload.AccessToken);
        Assert.Contains(jwt.Claims, claim => claim.Value == SchoolOsRoles.Teacher);
        Assert.Contains(jwt.Claims, claim => claim.Type == "permission" && claim.Value == "attendance.take");
        Assert.Contains(jwt.Claims, claim => claim.Type == "permission" && claim.Value == "grades.enter");
    }

    [Fact]
    public async Task Login_WithWrongPassword_AndUnknownUser_ReturnSameGenericError()
    {
        var email = await CreateUserAsync();
        using var client = CreateClient();

        var wrongPassword = await LoginAsync(client, email, "WrongPassword9");
        var unknownUser = await LoginAsync(client, "nobody@test.local", DefaultPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);

        var wrongPasswordCode = await ExtractErrorCodeAsync(wrongPassword);
        var unknownUserCode = await ExtractErrorCodeAsync(unknownUser);
        Assert.Equal("invalid_credentials", wrongPasswordCode);
        Assert.Equal(wrongPasswordCode, unknownUserCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndReuseRevokesAllSessions()
    {
        var email = await CreateUserAsync();
        using var client = CreateClient();

        var login = await LoginAsync(client, email, DefaultPassword);
        var firstToken = GetRefreshCookie(login);
        Assert.NotNull(firstToken);

        var firstRefresh = await RefreshAsync(client, firstToken!);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        var secondToken = GetRefreshCookie(firstRefresh);
        Assert.NotNull(secondToken);
        Assert.NotEqual(firstToken, secondToken);

        // Replaying the already-rotated token must fail (reuse detection).
        var reuse = await RefreshAsync(client, firstToken!);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // Reuse revokes every active session: the newer token is dead too.
        var afterReuse = await RefreshAsync(client, secondToken!);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var email = await CreateUserAsync();
        using var client = CreateClient();

        var login = await LoginAsync(client, email, DefaultPassword);
        var refreshToken = GetRefreshCookie(login);
        Assert.NotNull(refreshToken);

        var logoutResponse = await client.SendAsync(WithRefreshCookie(HttpMethod.Post, "/api/auth/logout", refreshToken!));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refresh = await RefreshAsync(client, refreshToken!);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_RequiresAuth_AndRevokesAllSessions()
    {
        var email = await CreateUserAsync();
        using var client = CreateClient();

        var login = await LoginAsync(client, email, DefaultPassword);
        var payload = await login.Content.ReadFromJsonAsync<LoginResponsePayload>();
        var refreshToken = GetRefreshCookie(login);
        Assert.NotNull(payload);
        Assert.NotNull(refreshToken);

        var unauthenticated = await client.PostAsync("/api/auth/logout-all", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout-all");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", payload!.AccessToken);
        var logoutAll = await client.SendAsync(request);
        await AssertStatusAsync(HttpStatusCode.NoContent, logoutAll);

        var refresh = await RefreshAsync(client, refreshToken!);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task PasswordReset_RequestIsGeneric_AndConfirmChangesPassword()
    {
        var email = await CreateUserAsync();
        using var client = CreateClient();

        var requestResponse = await client.PostAsJsonAsync(
            "/api/auth/password-reset/request", new { email });
        Assert.Equal(HttpStatusCode.Accepted, requestResponse.StatusCode);

        var unknownResponse = await client.PostAsJsonAsync(
            "/api/auth/password-reset/request", new { email = "nobody@test.local" });
        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);

        string token;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            token = await userManager.GeneratePasswordResetTokenAsync(user!);
        }

        const string newPassword = "NewPassword2";
        var confirmResponse = await client.PostAsJsonAsync(
            "/api/auth/password-reset/confirm", new { email, token, newPassword });
        Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);

        var oldPasswordLogin = await LoginAsync(client, email, DefaultPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await LoginAsync(client, email, newPassword);
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task Me_RequiresAuth_AndReturnsCurrentUser()
    {
        var email = await CreateUserAsync(SchoolOsRoles.Teacher);
        using var client = CreateClient();

        var unauthenticated = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        var login = await LoginAsync(client, email, DefaultPassword);
        var payload = await login.Content.ReadFromJsonAsync<LoginResponsePayload>();
        Assert.NotNull(payload);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", payload!.AccessToken);
        var me = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meBody = await me.Content.ReadFromJsonAsync<MePayload>();
        Assert.NotNull(meBody);
        Assert.Equal(email, meBody!.Email);
        Assert.Contains(SchoolOsRoles.Teacher, meBody.Roles);
        Assert.Contains("attendance.take", meBody.Permissions);
    }

    private async Task<string> CreateUserAsync(string? role = null)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var email = $"user-{Guid.NewGuid():N}@test.local";
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

        if (role is not null)
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        return email;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string emailOrUserName, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { emailOrUserName, password });

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken) =>
        client.SendAsync(WithRefreshCookie(HttpMethod.Post, "/api/auth/refresh", refreshToken));

    private static HttpRequestMessage WithRefreshCookie(HttpMethod method, string url, string refreshToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshToken}");
        return request;
    }

    private static string? GetRefreshCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        foreach (var value in values)
        {
            if (!value.StartsWith(RefreshCookieName + "=", StringComparison.Ordinal))
            {
                continue;
            }

            var start = RefreshCookieName.Length + 1;
            var end = value.IndexOf(';', start);
            return end < 0 ? value[start..] : value[start..end];
        }

        return null;
    }

    private static async Task<string?> ExtractErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        var challenge = response.Headers.WwwAuthenticate.ToString();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Fail($"Expected {(int)expected} but got {(int)response.StatusCode}. WWW-Authenticate: '{challenge}'. Body: {body}");
    }

    private sealed record MePayload(string Id, string Email, string Name, string[] Roles, string[] Permissions);

    private sealed record LoginResponsePayload(string AccessToken, int ExpiresInSeconds);
}
