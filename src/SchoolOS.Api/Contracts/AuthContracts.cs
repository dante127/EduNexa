using System.ComponentModel.DataAnnotations;

namespace SchoolOS.Api.Contracts;

public sealed record LoginRequest(
    [Required] string EmailOrUserName,
    [Required] string Password);

public sealed record LoginResponse(string AccessToken, int ExpiresInSeconds);

public sealed record PasswordResetRequest([Required, EmailAddress] string Email);

public sealed record PasswordResetConfirm(
    [Required, EmailAddress] string Email,
    [Required] string Token,
    [Required] string NewPassword);
