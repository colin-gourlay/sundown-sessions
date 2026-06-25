// <copyright file="EnrichContentResult.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.ContentEnrichment
{
    public sealed record EnrichContentResult(
        string ReportPath,
        int ShowsScanned,
        int PlaylistEntriesParsed,
        int TrackInfoEntriesParsed,
        int ArtistPagesCreated,
        int ReleasePagesCreated,
        int TrackPagesCreated,
        int ExistingArtistsSkipped,
        int ExistingReleasesSkipped,
        int ExistingTracksSkipped,
        int AmbiguousItems,
        string CorrelationId)
    {
        public int PagesCreated => this.ArtistPagesCreated + this.ReleasePagesCreated + this.TrackPagesCreated;
    }
}
