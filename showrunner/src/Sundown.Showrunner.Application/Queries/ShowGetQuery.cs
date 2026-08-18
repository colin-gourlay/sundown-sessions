using Sundown.Showrunner.Application.Exceptions;
using Sundown.Showrunner.Application.Results;
using Sundown.Showrunner.Domain.Repositories;

namespace Sundown.Showrunner.Application.Queries;

public sealed class ShowGetQuery
{
    private readonly IShowRepository _shows;

    public ShowGetQuery(IShowRepository shows)
    {
        _shows = shows;
    }

    public async Task<ShowResult> ByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var show = await _shows.GetByIdAsync(id, cancellationToken)
            ?? throw new ShowNotFoundException(id);

        return MapShow(show);
    }

    public async Task<ShowResult> ByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var show = await _shows.GetByDateAsync(date, cancellationToken)
            ?? throw new ShowNotFoundException(date);

        return MapShow(show);
    }

    private static ShowResult MapShow(Domain.Entities.Show show) => new()
    {
        Id = show.Id,
        BroadcastDate = show.BroadcastDate.ToString("yyyy-MM-dd"),
        Title = show.Title,
        Status = show.Status.ToString(),
        Slots = show.Slots.Select(s => new ShowSlotResult
        {
            Position = s.Position,
            RecordingId = s.RecordingId,
            ArtistName = s.ArtistName,
            TrackTitle = s.TrackTitle,
            AlbumTitle = s.AlbumTitle,
        }).ToList(),
    };
}
