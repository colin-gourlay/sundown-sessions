namespace SundownSessions.Showrunner;

public sealed record RepeatExceptionReason
{
    private RepeatExceptionReason(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ApplicationResult<RepeatExceptionReason> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ApplicationResult<RepeatExceptionReason>.Failure(
                ApplicationError.Validation("reason", "A repeat exception requires an explicit reason."));
        }

        return ApplicationResult<RepeatExceptionReason>.Success(new RepeatExceptionReason(value.Trim()));
    }
}
