using Sundown.Showrunner.Application.Exceptions;
using Sundown.Showrunner.Application.Results;
using Sundown.Showrunner.Domain.Repositories;

namespace Sundown.Showrunner.Application.Queries;

public sealed class RecordingHistoryQuery
{
    private readonly IRecordingRepository _recordings;

    public RecordingHistoryQuery(IRecordingRepository recordings)
    {
        _recordings = recordings;
    }

    public async Task<IReadOnlyList<PlayHistoryResult>> ExecuteAsync(int recordingId, CancellationToken cancellationToken = default)
    {
        var recording = await _recordings.GetByIdAsync(recordingId, cancellationToken)
            ?? throw new RecordingNotFoundException(recordingId);

        var history = await _recordings.GetHistoryAsync(recordingId, cancellationToken);

        return history.Select(h => new PlayHistoryResult
        {
            ShowId = h.ShowId,
            BroadcastDate = h.BroadcastDate.ToString("yyyy-MM-dd"),
            ArtistName = h.ArtistName ?? recording.ArtistName,
            TrackTitle = h.TrackTitle ?? recording.TrackTitle,
        }).ToList();
    }
}
