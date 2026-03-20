using FluentValidation;

namespace SundownMedia.ContentOps.Application.Features.AlbumReview.Intake;

public sealed class IntakeAlbumCommandValidator : AbstractValidator<IntakeAlbumCommand>
{
    public IntakeAlbumCommandValidator()
    {
        RuleFor(command => command.SourcePath).NotEmpty();
        RuleFor(command => command.WorkingRoot).NotEmpty();
        RuleFor(command => command.MasterRoot).NotEmpty();
        RuleFor(command => command.CorrelationId).NotEmpty();
    }
}
