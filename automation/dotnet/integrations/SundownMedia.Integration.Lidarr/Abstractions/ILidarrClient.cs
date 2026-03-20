using SundownMedia.Integration.Lidarr.Models;

namespace SundownMedia.Integration.Lidarr.Abstractions;

public interface ILidarrClient
{
    Task<LidarrResult<bool>> RefreshLibraryAsync(LidarrRelease release, CancellationToken cancellationToken);
}
