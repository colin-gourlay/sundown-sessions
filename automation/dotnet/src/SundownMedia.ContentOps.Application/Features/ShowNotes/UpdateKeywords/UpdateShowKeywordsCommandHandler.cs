// <copyright file="UpdateShowKeywordsCommandHandler.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.ShowNotes.UpdateKeywords;

using ErrorOr;
using Mediator;
using SundownMedia.ContentOps.Application.Abstractions;
using SundownMedia.ContentOps.Domain.ShowNotes;

public sealed class UpdateShowKeywordsCommandHandler : IRequestHandler<UpdateShowKeywordsCommand, ErrorOr<UpdateShowKeywordsResult>>
{
    private readonly IShowNotesService showNotesService;

    public UpdateShowKeywordsCommandHandler(IShowNotesService showNotesService)
    {
        this.showNotesService = showNotesService;
    }

    public async ValueTask<ErrorOr<UpdateShowKeywordsResult>> Handle(UpdateShowKeywordsCommand command, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(command.ShowDirectoryPath))
        {
            return Error.Validation("UpdateShowKeywords.ShowDirectoryPath", "Show directory path does not exist.");
        }

        string trackInfoContent;

        try
        {
            trackInfoContent = await this.showNotesService.ReadTrackInfoAsync(command.ShowDirectoryPath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return Error.NotFound("UpdateShowKeywords.TrackInfo", "Track info file was not found.");
        }

        var keywords = TrackInfoParser.ParseArtistNames(trackInfoContent);

        try
        {
            await this.showNotesService.UpdateKeywordsAsync(command.ShowDirectoryPath, keywords, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return Error.NotFound("UpdateShowKeywords.ShowNotes", "Show notes index file was not found.");
        }

        return new UpdateShowKeywordsResult(keywords);
    }
}
