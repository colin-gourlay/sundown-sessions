namespace SundownMedia.Integration.Lidarr.Models;

public sealed record LidarrResult<T>(bool IsSuccess, T? Value, string? ErrorCode = null, string? ErrorMessage = null);
