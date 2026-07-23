namespace ai_speis_be.Models.DTOs
{
    public sealed class AdminDashboardDto
    {
        // User metrics
        public int TotalUsers { get; init; }
        public int PremiumUsers { get; init; }
        public int FreeUsers { get; init; }
        public int ActiveUsers { get; init; }
        public int LockedUsers { get; init; }

        // Content metrics
        public int TotalQuestions { get; init; }

        // Interview metrics
        public int TotalInterviews { get; init; }
        public int MonthlyInterviews { get; init; }

        // Revenue metrics (VND)
        public decimal TotalRevenue { get; init; }
        public decimal MonthlyRevenue { get; init; }

        // Time-series data (last 6 months) for sparkline charts
        public IReadOnlyList<MonthlyDataPointDto> UserGrowth { get; init; } = Array.Empty<MonthlyDataPointDto>();
        public IReadOnlyList<MonthlyDataPointDto> InterviewActivity { get; init; } = Array.Empty<MonthlyDataPointDto>();
        public IReadOnlyList<MonthlyDataPointDto> RevenueActivity { get; init; } = Array.Empty<MonthlyDataPointDto>();

        public DateTime GeneratedAt { get; init; }
    }

    public sealed class MonthlyDataPointDto
    {
        public string Label { get; init; } = string.Empty; // e.g. "Jan 2026"
        public int Year { get; init; }
        public int Month { get; init; }
        public decimal Value { get; init; }
    }
}
