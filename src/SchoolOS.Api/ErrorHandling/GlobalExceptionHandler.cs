using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Common;

namespace SchoolOS.Api.ErrorHandling;

/// <summary>
/// Converts exceptions into RFC 9457 ProblemDetails responses carrying a stable,
/// machine-readable <c>code</c> extension. Stack traces are never sent to clients.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, code, title) = exception switch
        {
            NotFoundException => (
                StatusCodes.Status404NotFound,
                "not_found",
                "The requested resource was not found."),
            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                conflict.Code,
                exception.Message),
            ValidationException => (
                StatusCodes.Status400BadRequest,
                "validation_error",
                "One or more validation errors occurred."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "An unexpected error occurred.")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation(
                "Request rejected ({Code}) for {Method} {Path}",
                code,
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://schoolos.local/errors/{code}"
        };
        problemDetails.Extensions["code"] = code;

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(failure => failure.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).ToArray());
        }

        if (status >= StatusCodes.Status500InternalServerError && environment.IsDevelopment())
        {
            problemDetails.Detail = exception.Message;
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
        return true;
    }
}
