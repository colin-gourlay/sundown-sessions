using Sundown.Showrunner.Domain.Entities;

namespace Sundown.Showrunner.Domain.Repositories;

public interface IRepeatExceptionRepository
{
    Task<RepeatException?> GetAsync(int recordingId, int showId, CancellationToken cancellationToken = default);
    Task<RepeatException> CreateAsync(RepeatException exception, CancellationToken cancellationToken = default);
}
