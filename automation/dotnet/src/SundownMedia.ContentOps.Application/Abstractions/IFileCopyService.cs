namespace SundownMedia.ContentOps.Application.Abstractions;

public interface IFileCopyService
{
    Task CopyAlbumAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken);
}
