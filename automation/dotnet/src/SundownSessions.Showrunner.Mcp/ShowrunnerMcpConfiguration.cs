namespace SundownSessions.Showrunner.Mcp;

internal static class ShowrunnerMcpConfiguration
{
    public const string MusicRootEnvironmentVariable = "SUNDOWN_SHOWRUNNER_MUSIC_ROOT";
    public const string PreparationRootEnvironmentVariable = "SUNDOWN_SHOWRUNNER_PREPARATION_ROOT";
    public const string ShowDurationMinutesEnvironmentVariable = "SUNDOWN_SHOWRUNNER_SHOW_DURATION_MINUTES";

    public static ShowPreparationOptions Load()
    {
        var musicRoot = GetRequiredPath(MusicRootEnvironmentVariable);
        var preparationRoot = GetRequiredPath(PreparationRootEnvironmentVariable);
        var durationValue = Environment.GetEnvironmentVariable(ShowDurationMinutesEnvironmentVariable);
        TimeSpan? configuredDuration = null;
        if (!string.IsNullOrWhiteSpace(durationValue))
        {
            if (!double.TryParse(
                    durationValue,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var minutes) ||
                minutes <= 0)
            {
                throw new InvalidOperationException(
                    $"{ShowDurationMinutesEnvironmentVariable} must contain a positive number of minutes.");
            }

            configuredDuration = TimeSpan.FromMinutes(minutes);
        }

        return new ShowPreparationOptions(musicRoot, preparationRoot, configuredDuration);
    }

    private static string GetRequiredPath(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"The required {variable} environment variable is not configured.");
        }

        return value;
    }
}
