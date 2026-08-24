using System.Collections.ObjectModel;

namespace SundownSessions.Showrunner;

public sealed record ApplicationError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Details)
{
    public static ApplicationError Validation(string field, params string[] messages)
    {
        return new ApplicationError(
            "validation_failed",
            "One or more validation errors occurred.",
            new ReadOnlyDictionary<string, IReadOnlyList<string>>(
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                {
                    [field] = messages,
                }));
    }

    public static ApplicationError NotFound(string resource, Guid id)
        => NotFound(resource, id.ToString());

    public static ApplicationError NotFound(string resource, string identifier)
    {
        return new ApplicationError(
            "not_found",
            $"{resource} '{identifier}' was not found.",
            new ReadOnlyDictionary<string, IReadOnlyList<string>>(
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                {
                    [resource] = new[] { identifier },
                }));
    }

    public static ApplicationError Conflict(string code, string message, string field, params string[] values)
    {
        return new ApplicationError(
            code,
            message,
            new ReadOnlyDictionary<string, IReadOnlyList<string>>(
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                {
                    [field] = values,
                }));
    }

    public static ApplicationError OperationFailed(string code, string message)
    {
        return new ApplicationError(
            code,
            message,
            new ReadOnlyDictionary<string, IReadOnlyList<string>>(
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)));
    }
}

public sealed class ApplicationResult
{
    private ApplicationResult(bool isSuccess, ApplicationError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public ApplicationError? Error { get; }

    public static ApplicationResult Success() => new(true, null);

    public static ApplicationResult Failure(ApplicationError error) => new(false, error);
}

public sealed class ApplicationResult<T>
{
    private ApplicationResult(bool isSuccess, T? value, ApplicationError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public T? Value { get; }

    public ApplicationError? Error { get; }

    public static ApplicationResult<T> Success(T value) => new(true, value, null);

    public static ApplicationResult<T> Failure(ApplicationError error) => new(false, default, error);
}
