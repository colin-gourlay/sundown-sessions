using Sundown.Showrunner.Application.Exceptions;
using Sundown.Showrunner.Application.Results;
using Sundown.Showrunner.Domain.Repositories;

namespace Sundown.Showrunner.Application.Queries;

public sealed class RecordingSearchQuery
{
    private readonly IRecordingRepository _recordings;

    public RecordingSearchQuery(IRecordingRepository recordings)
    {
        _recordings = recordings;
    }

    public async Task<RecordingSearchResult> ExecuteAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new DomainRuleException("Search query must not be empty.");

        var matches = await _recordings.SearchAsync(query, cancellationToken);

        return new RecordingSearchResult
        {
            Matches = matches.Select(r => new RecordingResult
            {
                Id = r.Id,
                ArtistName = r.ArtistName,
                TrackTitle = r.TrackTitle,
                AlbumTitle = r.AlbumTitle,
                Isrc = r.Isrc,
                HasLocalFile = r.LocalFilePath is not null,
            }).ToList(),
            IsAmbiguous = matches.Count > 1,
        };
    }
}
