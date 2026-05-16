namespace KnowledgeSearch.Core.Common;

public enum ErrorKind
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
}

public sealed record ResultError(
    string Code,
    string Message,
    ErrorKind Kind);
