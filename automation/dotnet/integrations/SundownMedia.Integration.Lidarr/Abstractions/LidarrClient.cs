using SundownMedia.Integration.Lidarr.Models;

namespace SundownMedia.Integration.Lidarr.Abstractions;

public sealed class LidarrClient : ILidarrClient
{
    public Task<LidarrResult<bool>> RefreshLibraryAsync(LidarrRelease release, CancellationToken cancellationToken)
    {
        var result = new LidarrResult<bool>(false, false, "NotImplemented", "Lidarr client is a reusable integration contract stub.");
        return Task.FromResult(result);
    }
}
