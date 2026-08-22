using Sundown.Showrunner.Application.Exceptions;
using Sundown.Showrunner.Application.Results;
using Sundown.Showrunner.Domain.Entities;
using Sundown.Showrunner.Domain.Repositories;

namespace Sundown.Showrunner.Application.Commands;

public sealed class RepeatExceptionCreateCommand
{
    private readonly IShowRepository _shows;
    private readonly IRecordingRepository _recordings;
    private readonly IRepeatExceptionRepository _repeatExceptions;

    public RepeatExceptionCreateCommand(
        IShowRepository shows,
        IRecordingRepository recordings,
        IRepeatExceptionRepository repeatExceptions)
    {
        _shows = shows;
        _recordings = recordings;
        _repeatExceptions = repeatExceptions;
    }

    public async Task<RepeatExceptionResult> ExecuteAsync(
        int showId,
        int recordingId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("A reason must be provided for the repeat exception.");

        var show = await _shows.GetByIdAsync(showId, cancellationToken)
            ?? throw new ShowNotFoundException(showId);

        var recording = await _recordings.GetByIdAsync(recordingId, cancellationToken)
            ?? throw new RecordingNotFoundException(recordingId);

        var existing = await _repeatExceptions.GetAsync(recordingId, showId, cancellationToken);
        if (existing is not null)
            throw new DomainRuleException($"A repeat exception already exists for recording {recordingId} in show {showId}.");

        var history = await _recordings.GetHistoryAsync(recordingId, cancellationToken);
        if (history.Count == 0)
            throw new DomainRuleException($"Recording {recordingId} has no play history; a repeat exception is not required.");

        var exception = await _repeatExceptions.CreateAsync(new RepeatException
        {
            RecordingId = recordingId,
            ShowId = showId,
            Reason = reason,
            CreatedAt = DateTimeOffset.UtcNow,
        }, cancellationToken);

        return new RepeatExceptionResult
        {
            Id = exception.Id,
            RecordingId = exception.RecordingId,
            ShowId = exception.ShowId,
            Reason = exception.Reason,
        };
    }
}
