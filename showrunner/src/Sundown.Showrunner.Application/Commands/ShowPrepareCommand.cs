using Sundown.Showrunner.Application.Exceptions;
using Sundown.Showrunner.Application.Results;
using Sundown.Showrunner.Domain.Entities;
using Sundown.Showrunner.Domain.Repositories;

namespace Sundown.Showrunner.Application.Commands;

public sealed class ShowPrepareCommand
{
    private readonly IShowRepository _shows;
    private readonly IRecordingRepository _recordings;
    private readonly IRepeatExceptionRepository _repeatExceptions;

    public ShowPrepareCommand(
        IShowRepository shows,
        IRecordingRepository recordings,
        IRepeatExceptionRepository repeatExceptions)
    {
        _shows = shows;
        _recordings = recordings;
        _repeatExceptions = repeatExceptions;
    }

    public async Task<ShowPrepareResult> ExecuteAsync(int showId, CancellationToken cancellationToken = default)
    {
        var show = await _shows.GetByIdAsync(showId, cancellationToken)
            ?? throw new ShowNotFoundException(showId);

        if (show.Status == ShowStatus.Broadcast || show.Status == ShowStatus.Finalised)
            throw new DomainRuleException($"Show {showId} has already been broadcast and cannot be prepared again.");

        var conflicts = new List<RepeatConflict>();

        foreach (var slot in show.Slots.Where(s => s.RecordingId.HasValue))
        {
            var recordingId = slot.RecordingId!.Value;
            var history = await _recordings.GetHistoryAsync(recordingId, cancellationToken);

            if (history.Count == 0) continue;

            var exception = await _repeatExceptions.GetAsync(recordingId, showId, cancellationToken);
            var recording = await _recordings.GetByIdAsync(recordingId, cancellationToken);

            conflicts.Add(new RepeatConflict
            {
                SlotPosition = slot.Position,
                RecordingId = recordingId,
                ArtistName = recording?.ArtistName ?? slot.ArtistName ?? string.Empty,
                TrackTitle = recording?.TrackTitle ?? slot.TrackTitle ?? string.Empty,
                PreviousPlays = history.Select(h => new PlayHistoryResult
                {
                    ShowId = h.ShowId,
                    BroadcastDate = h.BroadcastDate.ToString("yyyy-MM-dd"),
                    ArtistName = h.ArtistName ?? string.Empty,
                    TrackTitle = h.TrackTitle ?? string.Empty,
                }).ToList(),
                HasException = exception is not null,
            });
        }

        var updated = show with { Status = ShowStatus.Prepared };
        await _shows.SaveAsync(updated, cancellationToken);

        return new ShowPrepareResult
        {
            Show = new ShowResult
            {
                Id = updated.Id,
                BroadcastDate = updated.BroadcastDate.ToString("yyyy-MM-dd"),
                Title = updated.Title,
                Status = updated.Status.ToString(),
                Slots = updated.Slots.Select(s => new ShowSlotResult
                {
                    Position = s.Position,
                    RecordingId = s.RecordingId,
                    ArtistName = s.ArtistName,
                    TrackTitle = s.TrackTitle,
                    AlbumTitle = s.AlbumTitle,
                }).ToList(),
            },
            RepeatConflicts = conflicts,
        };
    }
}
