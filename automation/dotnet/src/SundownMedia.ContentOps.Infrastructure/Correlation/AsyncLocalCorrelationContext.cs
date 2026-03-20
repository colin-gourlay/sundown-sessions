// <copyright file="AsyncLocalCorrelationContext.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Infrastructure.Correlation
{
    using SundownMedia.ContentOps.Contracts.Correlation;

    public sealed class AsyncLocalCorrelationContext : ICorrelationContext
    {
        private static readonly AsyncLocal<string?> CurrentCorrelationId = new();

        public string CorrelationId => CurrentCorrelationId.Value ?? string.Empty;

        public void SetCorrelation(string correlationId)
        {
            CurrentCorrelationId.Value = correlationId;
        }
    }
}
