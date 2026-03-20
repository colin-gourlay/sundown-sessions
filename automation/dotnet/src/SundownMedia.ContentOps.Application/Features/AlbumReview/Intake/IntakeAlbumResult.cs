// <copyright file="IntakeAlbumResult.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.AlbumReview.Intake;

public sealed record IntakeAlbumResult(Guid WorkflowId, string CorrelationId);
