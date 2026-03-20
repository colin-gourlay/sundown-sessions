// <copyright file="WorkflowState.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Domain.Workflows;

public enum WorkflowState
{
    Draft,
    IntakeRunning,
    IntakeSucceeded,
    IntakeFailed,
    PrepareRunning,
    PrepareSucceeded,
    PrepareFailed,
    AwaitingReview,
    ReviewCompleted,
    PublishRunning,
    PublishPartiallyFailed,
    PublishSucceeded,
    Cancelled,
}
