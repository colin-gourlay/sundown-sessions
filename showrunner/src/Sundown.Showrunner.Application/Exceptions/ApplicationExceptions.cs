namespace Sundown.Showrunner.Application.Exceptions;

public sealed class ShowNotFoundException : Exception
{
    public ShowNotFoundException(int id)
        : base($"Show with ID {id} was not found.") { }

    public ShowNotFoundException(DateOnly date)
        : base($"No show found for {date:yyyy-MM-dd}.") { }
}

public sealed class RecordingNotFoundException : Exception
{
    public RecordingNotFoundException(int id)
        : base($"Recording with ID {id} was not found.") { }
}

public sealed class DomainRuleException : Exception
{
    public DomainRuleException(string message) : base(message) { }
}
