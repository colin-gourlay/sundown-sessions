using Sundown.Showrunner.Domain.Entities;

namespace Sundown.Showrunner.Domain.Repositories;

public interface IRecordingRepository
{
    Task<Recording?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Recording>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayHistory>> GetHistoryAsync(int recordingId, CancellationToken cancellationToken = default);
    Task<Recording> SaveAsync(Recording recording, CancellationToken cancellationToken = default);
}
