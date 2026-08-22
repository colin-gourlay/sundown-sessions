using Sundown.Showrunner.Domain.Entities;
using Sundown.Showrunner.Domain.Repositories;

namespace Sundown.Showrunner.TestHelpers.Fakes;

public sealed class FakeShowRepository : IShowRepository
{
    private readonly Dictionary<int, Show> _shows = new();
    private int _nextId = 1;

    public void Seed(Show show) => _shows[show.Id] = show;

    public Task<Show?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(_shows.GetValueOrDefault(id));

    public Task<Show?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
        => Task.FromResult(_shows.Values.FirstOrDefault(s => s.BroadcastDate == date));

    public Task<Show> SaveAsync(Show show, CancellationToken cancellationToken = default)
    {
        if (show.Id == 0) show = show with { Id = _nextId++ };
        _shows[show.Id] = show;
        return Task.FromResult(show);
    }
}

public sealed class FakeRecordingRepository : IRecordingRepository
{
    private readonly Dictionary<int, Recording> _recordings = new();
    private readonly List<PlayHistory> _history = [];
    private int _nextId = 1;

    public void Seed(Recording recording) => _recordings[recording.Id] = recording;
    public void SeedHistory(PlayHistory history) => _history.Add(history);

    public Task<Recording?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(_recordings.GetValueOrDefault(id));

    public Task<IReadOnlyList<Recording>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Recording> matches = _recordings.Values
            .Where(r => r.ArtistName.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || r.TrackTitle.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || (r.AlbumTitle?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<PlayHistory>> GetHistoryAsync(int recordingId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PlayHistory> results = _history.Where(h => h.RecordingId == recordingId).ToList();
        return Task.FromResult(results);
    }

    public Task<Recording> SaveAsync(Recording recording, CancellationToken cancellationToken = default)
    {
        if (recording.Id == 0) recording = recording with { Id = _nextId++ };
        _recordings[recording.Id] = recording;
        return Task.FromResult(recording);
    }
}

public sealed class FakeRepeatExceptionRepository : IRepeatExceptionRepository
{
    private readonly List<RepeatException> _exceptions = [];
    private int _nextId = 1;

    public void Seed(RepeatException ex) => _exceptions.Add(ex);

    public Task<RepeatException?> GetAsync(int recordingId, int showId, CancellationToken cancellationToken = default)
        => Task.FromResult(_exceptions.FirstOrDefault(e => e.RecordingId == recordingId && e.ShowId == showId));

    public Task<RepeatException> CreateAsync(RepeatException exception, CancellationToken cancellationToken = default)
    {
        var saved = exception with { Id = _nextId++ };
        _exceptions.Add(saved);
        return Task.FromResult(saved);
    }
}
