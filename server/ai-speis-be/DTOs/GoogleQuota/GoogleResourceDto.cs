namespace ai_speis_be.DTOs.GoogleQuota
{
    /// <summary>
    /// Unified model representing a single Google Cloud service's quota state.
    /// Returned by GET /api/google/quota.
    /// </summary>
    public sealed class GoogleResourceDto
    {
        /// <summary>Display name of the service (e.g. "Cloud Speech-to-Text API").</summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>Whether the service is currently enabled in the project.</summary>
        public bool Enabled { get; set; }

        /// <summary>Quota limit value, null if no limit metric found.</summary>
        public double? Limit { get; set; }

        /// <summary>Current usage value within the monitoring window.</summary>
        public double? CurrentUsage { get; set; }

        /// <summary>Remaining = Limit - CurrentUsage, null if either is unknown.</summary>
        public double? Remaining { get; set; }

        /// <summary>Usage as a percentage of the limit (0-100), null if limit is unknown.</summary>
        public double? PercentUsed { get; set; }

        /// <summary>Unit of measurement (e.g. "1/{project}", "1/min/{project}").</summary>
        public string? Unit { get; set; }

        /// <summary>Specific quota metric name being reported.</summary>
        public string? QuotaMetric { get; set; }
    }

    /// <summary>
    /// Wrapper response for the Google quota overview endpoint.
    /// </summary>
    public sealed class GoogleQuotaResponseDto
    {
        /// <summary>The Google Cloud project being monitored.</summary>
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>UTC timestamp of when the query was executed.</summary>
        public DateTime QueriedAt { get; set; }

        /// <summary>List of services with their quota data.</summary>
        public List<GoogleResourceDto> Services { get; set; } = new();
    }
}
