using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SchoolOS.Api.Contracts;
using SchoolOS.Infrastructure.Identity;

namespace SchoolOS.Api.Controllers;

/// <summary>
/// Authentication endpoints (docs/architecture.md §5, docs/security-plan.md §2):
/// JWT access token + rotating refresh token cookie ("schoolos_rt").
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<AppUser> userManager,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IOptions<JwtOptions> jwtOptions,
    IWebHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string RefreshCookieName = "schoolos_rt";
    private const string RefreshCookiePath = "/api/auth";

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.EmailOrUserName)
            ?? await userManager.FindByNameAsync(request.EmailOrUserName);

        if (user is null || user.Status != UserStatuses.Active)
        {
            return InvalidCredentials();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return InvalidCredentials();
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return InvalidCredentials();
        }

        await userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        return await CreateSessionAsync(user, cancellationToken);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var rawToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(rawToken))
        {
            return InvalidRefresh();
        }

        var result = await refreshTokenService.ValidateAndRotateAsync(
            rawToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        if (result.Status != RefreshTokenValidationStatus.Success || result.NewToken is null || result.UserId is null)
        {
            DeleteRefreshCookie();
            return InvalidRefresh(result.Status == RefreshTokenValidationStatus.ReuseDetected);
        }

        var user = await userManager.FindByIdAsync(result.UserId.Value.ToString());
        if (user is null || user.Status != UserStatuses.Active)
        {
            DeleteRefreshCookie();
            return InvalidRefresh();
        }

        var accessToken = await jwtTokenService.CreateAccessTokenAsync(user, cancellationToken);
        WriteRefreshCookie(result.NewToken);
        return Ok(CreateLoginResponse(accessToken));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var rawToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(rawToken))
        {
            await refreshTokenService.RevokeAsync(rawToken, cancellationToken);
        }

        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        await refreshTokenService.RevokeAllForUserAsync(userId, cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpPost("password-reset/request")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RequestPasswordReset(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null && user.Status == UserStatuses.Active)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            if (environment.IsDevelopment())
            {
                // Dev-only convenience: there is no email provider yet (arrives with the
                // communication module, Phase 6). Never log reset tokens outside Development.
                logger.LogInformation("DEV ONLY - password reset token for {Email}: {Token}", user.Email, token);
            }
        }

        // Always the same response regardless of account existence (no user enumeration).
        return Accepted(new { message = "If the account exists, a password reset email has been sent." });
    }

    [HttpPost("password-reset/confirm")]
    public async Task<IActionResult> ConfirmPasswordReset(PasswordResetConfirm request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || user.Status != UserStatuses.Active)
        {
            return InvalidPasswordReset();
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return InvalidPasswordReset();
        }

        // Password changed: all existing sessions are no longer trusted.
        await refreshTokenService.RevokeAllForUserAsync(user.Id, cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult> CreateSessionAsync(AppUser user, CancellationToken cancellationToken)
    {
        var accessToken = await jwtTokenService.CreateAccessTokenAsync(user, cancellationToken);
        var rawRefreshToken = await refreshTokenService.CreateAsync(
            user.Id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        WriteRefreshCookie(rawRefreshToken);
        return Ok(CreateLoginResponse(accessToken));
    }

    private LoginResponse CreateLoginResponse(AccessTokenResult accessToken) =>
        new(accessToken.Token, Math.Max(1, (int)(accessToken.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds));

    private void WriteRefreshCookie(string token) =>
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = RefreshCookiePath,
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays)
        });

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });

    private ObjectResult InvalidCredentials() =>
        ProblemWithCode(StatusCodes.Status401Unauthorized, "invalid_credentials", "Invalid credentials.");

    private ObjectResult InvalidRefresh(bool reuseDetected = false)
    {
        if (reuseDetected)
        {
            logger.LogWarning("Refresh token reuse detected; all sessions for the affected user were revoked.");
        }

        return ProblemWithCode(StatusCodes.Status401Unauthorized, "invalid_refresh_token", "The session is no longer valid.");
    }

    private ObjectResult InvalidPasswordReset() =>
        ProblemWithCode(
            StatusCodes.Status400BadRequest,
            "invalid_password_reset",
            "The password reset request is invalid or has expired.");

    private static ObjectResult ProblemWithCode(int status, string code, string title)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://schoolos.local/errors/{code}"
        };
        problem.Extensions["code"] = code;

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}
