namespace SchoolOS.Application.Common;

public sealed class NotFoundException(string entityName, object key)
    : Exception($"{entityName} with key '{key}' was not found.")
{
}

public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
