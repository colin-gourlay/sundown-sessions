// <copyright file="ICorrelationContext.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Contracts.Correlation
{
    public interface ICorrelationContext
    {
        string CorrelationId { get; }

        void SetCorrelation(string correlationId);
    }
}
