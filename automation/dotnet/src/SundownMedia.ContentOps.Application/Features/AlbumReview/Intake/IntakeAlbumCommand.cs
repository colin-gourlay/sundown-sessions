using ErrorOr;
using Mediator;

namespace SundownMedia.ContentOps.Application.Features.AlbumReview.Intake;

public sealed record IntakeAlbumCommand(string SourcePath, string WorkingRoot, string MasterRoot, string CorrelationId) : IRequest<ErrorOr<IntakeAlbumResult>>;
