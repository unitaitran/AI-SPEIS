using ai_speis_be.DTOs.GoogleQuota;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.BigQuery.V2;

namespace ai_speis_be.Services.GoogleQuotaService
{
    /// <summary>
    /// Service that queries Google Cloud Billing Export in BigQuery to retrieve cost metrics.
    /// Never throws exceptions — if dataset/table is unconfigured, not found, or fails,
    /// returns HasData = false gracefully.
    /// </summary>
    public sealed class CloudCostService : ICloudCostService
    {
        private readonly GoogleQuotaConfig _config;
        private readonly ILogger<CloudCostService> _logger;

        public CloudCostService(
            GoogleQuotaConfig config,
            ILogger<CloudCostService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<CloudCostDto> GetCloudCostAsync(CancellationToken cancellationToken = default)
        {
            var fallback = new CloudCostDto
            {
                HasData = false,
                TodayCost = 0,
                YesterdayCost = 0,
                MonthlyCost = 0,
                Forecast = 0,
                TopServices = new List<CostByServiceDto>(),
                DailyTrend = new List<DailyCostDto>()
            };

            // If dataset, table, or credentials are not configured, return empty HasData = false
            if (!_config.IsConfigured || string.IsNullOrWhiteSpace(_config.BillingDataset) || string.IsNullOrWhiteSpace(_config.BillingTable))
            {
                _logger.LogInformation("BigQuery Billing Export is not configured. Returning HasData = false.");
                return fallback;
            }

            try
            {
                var jsonText = await File.ReadAllTextAsync(_config.CredentialsPath, cancellationToken);
                var serviceAccountCred = ServiceAccountCredential.FromServiceAccountData(
                    new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonText)));
                var credential = serviceAccountCred.ToGoogleCredential()
                    .CreateScoped("https://www.googleapis.com/auth/cloud-platform");

                var client = await BigQueryClient.CreateAsync(_config.ProjectId, credential);

                var tableReference = $"`{_config.ProjectId}.{_config.BillingDataset}.{_config.BillingTable}`";

                // 1. Query today, yesterday, and MTD cost
                var summarySql = $@"
                    SELECT
                      SUM(CASE WHEN DATE(usage_start_time) = CURRENT_DATE() THEN cost ELSE 0 END) AS today_cost,
                      SUM(CASE WHEN DATE(usage_start_time) = DATE_SUB(CURRENT_DATE(), INTERVAL 1 DAY) THEN cost ELSE 0 END) AS yesterday_cost,
                      SUM(CASE WHEN DATE(usage_start_time) >= DATE_TRUNC(CURRENT_DATE(), MONTH) THEN cost ELSE 0 END) AS monthly_cost
                    FROM {tableReference}
                    WHERE DATE(usage_start_time) >= DATE_TRUNC(CURRENT_DATE(), MONTH)";

                var summaryResults = await client.ExecuteQueryAsync(summarySql, parameters: null, cancellationToken: cancellationToken);
                var summaryRow = summaryResults.FirstOrDefault();

                if (summaryRow == null)
                {
                    return fallback;
                }

                double todayCost = Convert.ToDouble(summaryRow["today_cost"] ?? 0);
                double yesterdayCost = Convert.ToDouble(summaryRow["yesterday_cost"] ?? 0);
                double monthlyCost = Convert.ToDouble(summaryRow["monthly_cost"] ?? 0);

                // Calculate linear forecast for current month
                var now = DateTime.UtcNow;
                int currentDay = now.Day;
                int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
                double forecast = currentDay > 0
                    ? Math.Round((monthlyCost / currentDay) * daysInMonth, 2)
                    : monthlyCost;

                // 2. Query Top Services this month
                var topServicesSql = $@"
                    SELECT
                      COALESCE(service.description, 'Other Services') AS service_name,
                      SUM(cost) AS total_cost
                    FROM {tableReference}
                    WHERE DATE(usage_start_time) >= DATE_TRUNC(CURRENT_DATE(), MONTH)
                    GROUP BY service_name
                    ORDER BY total_cost DESC
                    LIMIT 5";

                var topServicesResults = await client.ExecuteQueryAsync(topServicesSql, parameters: null, cancellationToken: cancellationToken);
                var topServices = new List<CostByServiceDto>();

                foreach (var row in topServicesResults)
                {
                    double svcCost = Math.Round(Convert.ToDouble(row["total_cost"] ?? 0), 2);
                    double pct = monthlyCost > 0 ? Math.Round((svcCost / monthlyCost) * 100, 1) : 0;
                    topServices.Add(new CostByServiceDto
                    {
                        ServiceName = Convert.ToString(row["service_name"]) ?? "Other",
                        Cost = svcCost,
                        Percentage = pct
                    });
                }

                // 3. Query Daily Trend for last 14 days
                var dailyTrendSql = $@"
                    SELECT
                      FORMAT_DATE('%Y-%m-%d', DATE(usage_start_time)) AS day,
                      SUM(cost) AS total_cost
                    FROM {tableReference}
                    WHERE DATE(usage_start_time) >= DATE_SUB(CURRENT_DATE(), INTERVAL 14 DAY)
                    GROUP BY day
                    ORDER BY day ASC";

                var dailyTrendResults = await client.ExecuteQueryAsync(dailyTrendSql, parameters: null, cancellationToken: cancellationToken);
                var dailyTrend = new List<DailyCostDto>();

                foreach (var row in dailyTrendResults)
                {
                    dailyTrend.Add(new DailyCostDto
                    {
                        Date = Convert.ToString(row["day"]) ?? string.Empty,
                        Cost = Math.Round(Convert.ToDouble(row["total_cost"] ?? 0), 2)
                    });
                }

                return new CloudCostDto
                {
                    HasData = true,
                    TodayCost = Math.Round(todayCost, 2),
                    YesterdayCost = Math.Round(yesterdayCost, 2),
                    MonthlyCost = Math.Round(monthlyCost, 2),
                    Forecast = forecast,
                    TopServices = topServices,
                    DailyTrend = dailyTrend
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query BigQuery Billing Export. Returning HasData = false.");
                return fallback;
            }
        }
    }
}
