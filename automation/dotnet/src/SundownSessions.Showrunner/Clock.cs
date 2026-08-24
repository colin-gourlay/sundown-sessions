namespace SundownSessions.Showrunner;

public interface IShowrunnerClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemShowrunnerClock : IShowrunnerClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
