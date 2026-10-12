using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Structure;

namespace SchoolOS.Application.Structure;

public sealed record SchoolSettingsDto(
    Guid Id,
    string NameEn,
    string NameAr,
    string Timezone,
    string DefaultLocale,
    string CurrencyCode,
    string? Address,
    string? LogoPath,
    string? Phone,
    string? Email);

public sealed record GetSchoolSettingsQuery : IRequest<SchoolSettingsDto>;

public sealed class GetSchoolSettingsHandler(IAppDbContext db)
    : IRequestHandler<GetSchoolSettingsQuery, SchoolSettingsDto>
{
    public async Task<SchoolSettingsDto> Handle(GetSchoolSettingsQuery request, CancellationToken cancellationToken)
    {
        var school = await db.Schools.AsNoTracking().SingleAsync(cancellationToken);
        return Map(school);
    }

    internal static SchoolSettingsDto Map(School school) =>
        new(
            school.Id,
            school.NameEn,
            school.NameAr,
            school.Timezone,
            school.DefaultLocale,
            school.CurrencyCode,
            school.Address,
            school.LogoPath,
            school.Phone,
            school.Email);
}

public sealed record UpdateSchoolSettingsCommand(
    string NameEn,
    string NameAr,
    string Timezone,
    string DefaultLocale,
    string CurrencyCode,
    string? Address,
    string? LogoPath,
    string? Phone,
    string? Email) : IRequest<SchoolSettingsDto>;

public sealed class UpdateSchoolSettingsValidator : AbstractValidator<UpdateSchoolSettingsCommand>
{
    public UpdateSchoolSettingsValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Timezone).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DefaultLocale).Must(locale => locale is "en" or "ar")
            .WithMessage("DefaultLocale must be 'en' or 'ar'.");
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.LogoPath).MaximumLength(300);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class UpdateSchoolSettingsHandler(IAppDbContext db)
    : IRequestHandler<UpdateSchoolSettingsCommand, SchoolSettingsDto>
{
    public async Task<SchoolSettingsDto> Handle(UpdateSchoolSettingsCommand request, CancellationToken cancellationToken)
    {
        var school = await db.Schools.SingleAsync(cancellationToken);

        school.NameEn = request.NameEn;
        school.NameAr = request.NameAr;
        school.Timezone = request.Timezone;
        school.DefaultLocale = request.DefaultLocale;
        school.CurrencyCode = request.CurrencyCode;
        school.Address = request.Address;
        school.LogoPath = request.LogoPath;
        school.Phone = request.Phone;
        school.Email = request.Email;

        await db.SaveChangesAsync(cancellationToken);
        return GetSchoolSettingsHandler.Map(school);
    }
}
