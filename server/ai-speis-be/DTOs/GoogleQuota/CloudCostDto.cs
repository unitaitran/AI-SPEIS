namespace ai_speis_be.DTOs.GoogleQuota
{
    /// <summary>
    /// Cost data aggregated per service.
    /// </summary>
    public sealed class CostByServiceDto
    {
        public string ServiceName { get; set; } = string.Empty;
        public double Cost { get; set; }
        public double Percentage { get; set; }
    }

    /// <summary>
    /// Daily cost data point for trend analysis.
    /// </summary>
    public sealed class DailyCostDto
    {
        public string Date { get; set; } = string.Empty; // Format: "YYYY-MM-DD"
        public double Cost { get; set; }
    }

    /// <summary>
    /// Model representing Google Cloud Billing cost information retrieved from BigQuery.
    /// </summary>
    public sealed class CloudCostDto
    {
        /// <summary>
        /// Indicates if BigQuery Billing Export data is available and configured.
        /// If false, UI will display Empty State instead of errors.
        /// </summary>
        public bool HasData { get; set; }

        /// <summary>Cost incurred today (USD).</summary>
        public double TodayCost { get; set; }

        /// <summary>Cost incurred yesterday (USD).</summary>
        public double YesterdayCost { get; set; }

        /// <summary>Cost incurred in current month to date (USD).</summary>
        public double MonthlyCost { get; set; }

        /// <summary>Forecasted cost for the full current month (USD).</summary>
        public double Forecast { get; set; }

        /// <summary>Top services by cost in current month.</summary>
        public List<CostByServiceDto> TopServices { get; set; } = new();

        /// <summary>Daily cost trend over recent days (e.g. 14 days).</summary>
        public List<DailyCostDto> DailyTrend { get; set; } = new();
    }

    /// <summary>
    /// Combined response DTO for the unified AI Usage & Google Resource Dashboard endpoint.
    /// GET /api/admin/google/dashboard
    /// </summary>
    public sealed class GoogleDashboardResponseDto
    {
        public string ProjectId { get; set; } = string.Empty;
        public DateTime QueriedAt { get; set; }
        public GoogleQuotaResponseDto Usage { get; set; } = new();
        public CloudCostDto Cost { get; set; } = new();
    }
}
