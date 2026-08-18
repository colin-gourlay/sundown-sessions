using Sundown.Showrunner.Application.Exceptions;
using Sundown.Showrunner.Application.Results;
using Sundown.Showrunner.Domain.Repositories;

namespace Sundown.Showrunner.Application.Commands;

public sealed class RecordingResolveCommand
{
    private readonly IShowRepository _shows;
    private readonly IRecordingRepository _recordings;

    public RecordingResolveCommand(IShowRepository shows, IRecordingRepository recordings)
    {
        _shows = shows;
        _recordings = recordings;
    }

    public async Task<ShowResult> ExecuteAsync(
        int showId,
        int slotPosition,
        int recordingId,
        CancellationToken cancellationToken = default)
    {
        var show = await _shows.GetByIdAsync(showId, cancellationToken)
            ?? throw new ShowNotFoundException(showId);

        var recording = await _recordings.GetByIdAsync(recordingId, cancellationToken)
            ?? throw new RecordingNotFoundException(recordingId);

        var slot = show.Slots.FirstOrDefault(s => s.Position == slotPosition)
            ?? throw new DomainRuleException($"Slot {slotPosition} does not exist in show {showId}.");

        var updatedSlots = show.Slots
            .Select(s => s.Position == slotPosition
                ? s with
                {
                    RecordingId = recording.Id,
                    ArtistName = recording.ArtistName,
                    TrackTitle = recording.TrackTitle,
                    AlbumTitle = recording.AlbumTitle,
                }
                : s)
            .ToList();

        var updated = show with { Slots = updatedSlots };
        await _shows.SaveAsync(updated, cancellationToken);

        return new ShowResult
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
        };
    }
}
