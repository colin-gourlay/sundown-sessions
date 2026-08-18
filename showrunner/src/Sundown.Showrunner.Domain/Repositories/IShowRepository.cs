using Sundown.Showrunner.Domain.Entities;

namespace Sundown.Showrunner.Domain.Repositories;

public interface IShowRepository
{
    Task<Show?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Show?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<Show> SaveAsync(Show show, CancellationToken cancellationToken = default);
}
