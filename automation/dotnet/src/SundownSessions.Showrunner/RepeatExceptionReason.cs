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

        if (value.Trim().Length > FieldLimits.RepeatExceptionReason)
        {
            return ApplicationResult<RepeatExceptionReason>.Failure(
                ApplicationError.Validation(
                    "reason",
                    $"A repeat exception reason cannot exceed {FieldLimits.RepeatExceptionReason} characters."));
        }

        return ApplicationResult<RepeatExceptionReason>.Success(new RepeatExceptionReason(value.Trim()));
    }
}
