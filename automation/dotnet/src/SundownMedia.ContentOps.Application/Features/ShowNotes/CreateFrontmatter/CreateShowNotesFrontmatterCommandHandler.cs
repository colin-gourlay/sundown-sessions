// <copyright file="CreateShowNotesFrontmatterCommandHandler.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.ShowNotes.CreateFrontmatter
{
    using ErrorOr;
    using Mediator;
    using SundownMedia.ContentOps.Application.Abstractions;

    public sealed class CreateShowNotesFrontmatterCommandHandler : IRequestHandler<CreateShowNotesFrontmatterCommand, ErrorOr<CreateShowNotesFrontmatterResult>>
    {
        private readonly IShowNotesWriter showNotesWriter;
        private readonly IShowLogoService showLogoService;

        public CreateShowNotesFrontmatterCommandHandler(IShowNotesWriter showNotesWriter, IShowLogoService showLogoService)
        {
            this.showNotesWriter = showNotesWriter;
            this.showLogoService = showLogoService;
        }

        public async ValueTask<ErrorOr<CreateShowNotesFrontmatterResult>> Handle(
            CreateShowNotesFrontmatterCommand command, CancellationToken cancellationToken)
        {
            var outputDirectory = Path.GetDirectoryName(command.OutputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                return Error.Validation("ShowNotes.OutputPath", "Output directory does not exist.");
            }

            if (!string.IsNullOrWhiteSpace(command.SpotifyEpisodeId))
            {
                var logoPath = Path.Combine(
                    outputDirectory ?? string.Empty,
                    $"{command.ShowNumber}-show-logo.jpeg");

                var logoResult = await this.showLogoService.DownloadLogoAsync(
                    command.SpotifyEpisodeId,
                    logoPath,
                    cancellationToken);

                if (logoResult.IsError)
                {
                    return logoResult.Errors;
                }
            }

            var content = ShowNotesFrontmatterBuilder.Build(command);
            await this.showNotesWriter.WriteAsync(command.OutputPath, content, cancellationToken);

            return new CreateShowNotesFrontmatterResult(command.OutputPath, command.CorrelationId);
        }
    }
}
