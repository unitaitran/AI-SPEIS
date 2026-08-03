using ai_speis_be.DTOs.GoogleQuota;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Monitoring.V3;
using Google.Cloud.ServiceUsage.V1;
using Google.Protobuf.WellKnownTypes;

namespace ai_speis_be.Services.GoogleQuotaService
{
    /// <summary>
    /// Queries Google Cloud for enabled services (Service Usage API) and their
    /// quota usage/limit (Cloud Monitoring API).
    ///
    /// Metrics used:
    /// - serviceruntime.googleapis.com/quota/rate/net_usage   → current usage
    /// - serviceruntime.googleapis.com/quota/allocation/usage → allocation usage
    /// - serviceruntime.googleapis.com/quota/limit            → configured limit
    ///
    /// Services without quota metrics are silently skipped (no exception thrown).
    /// </summary>
    public sealed class GoogleQuotaService : IGoogleQuotaService
    {
        private readonly ILogger<GoogleQuotaService> _logger;
        private readonly GoogleQuotaConfig _config;
        private readonly ServiceUsageClient _serviceUsageClient;
        private readonly MetricServiceClient _metricClient;

        public GoogleQuotaService(
            GoogleQuotaConfig config,
            ILogger<GoogleQuotaService> logger)
        {
            _config = config;
            _logger = logger;

            // Load service account credentials once (clients are thread-safe singletons).
            var jsonText = File.ReadAllText(_config.CredentialsPath);
            var serviceAccountCred = ServiceAccountCredential.FromServiceAccountData(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonText)));
            var credential = serviceAccountCred.ToGoogleCredential()
                .CreateScoped("https://www.googleapis.com/auth/cloud-platform");

            _serviceUsageClient = new ServiceUsageClientBuilder
            {
                GoogleCredential = credential
            }.Build();

            _metricClient = new MetricServiceClientBuilder
            {
                GoogleCredential = credential
            }.Build();

            _logger.LogInformation(
                "GoogleQuotaService initialized for project {ProjectId}",
                _config.ProjectId);
        }

        /// <inheritdoc />
        public async Task<GoogleQuotaResponseDto> GetQuotaOverviewAsync(
            CancellationToken cancellationToken = default)
        {
            var response = new GoogleQuotaResponseDto
            {
                ProjectId = _config.ProjectId,
                QueriedAt = DateTime.UtcNow,
                Services = new List<GoogleResourceDto>()
            };

            try
            {
                // ── Step 1: List enabled services ──────────────────────────
                var enabledServices = await ListEnabledServicesAsync(cancellationToken);
                _logger.LogInformation("Found {Count} enabled services", enabledServices.Count);

                // ── Step 2: Query quota metrics for all services at once ───
                var usageMap = await QueryQuotaMetricsAsync(
                    "serviceruntime.googleapis.com/quota/rate/net_usage",
                    cancellationToken);

                var allocationMap = await QueryQuotaMetricsAsync(
                    "serviceruntime.googleapis.com/quota/allocation/usage",
                    cancellationToken);

                var limitMap = await QueryQuotaMetricsAsync(
                    "serviceruntime.googleapis.com/quota/limit",
                    cancellationToken);

                // ── Step 3: Merge into unified DTOs ────────────────────────
                foreach (var svc in enabledServices)
                {
                    try
                    {
                        var serviceName = svc.ServiceName;

                        // Try rate usage first, fall back to allocation usage
                        var hasRateUsage = usageMap.TryGetValue(serviceName, out var rateEntries);
                        var hasAllocUsage = allocationMap.TryGetValue(serviceName, out var allocEntries);
                        var hasLimit = limitMap.TryGetValue(serviceName, out var limitEntries);

                        if (!hasRateUsage && !hasAllocUsage && !hasLimit)
                        {
                            // No quota metrics at all — skip this service
                            continue;
                        }

                        // Use rate usage if available, otherwise allocation usage
                        var usageEntries = hasRateUsage ? rateEntries! : allocEntries;
                        var usageType = hasRateUsage
                            ? "quota/rate/net_usage"
                            : "quota/allocation/usage";

                        // If a service has multiple quota metrics, emit one DTO per metric
                        var metricKeys = new HashSet<string>();
                        if (usageEntries != null)
                            foreach (var e in usageEntries) metricKeys.Add(e.MetricLabel);
                        if (limitEntries != null)
                            foreach (var e in limitEntries) metricKeys.Add(e.MetricLabel);

                        foreach (var metricLabel in metricKeys)
                        {
                            var usageValue = usageEntries?
                                .FirstOrDefault(e => e.MetricLabel == metricLabel)?.Value;
                            var limitValue = limitEntries?
                                .FirstOrDefault(e => e.MetricLabel == metricLabel)?.Value;
                            var unit = limitEntries?
                                .FirstOrDefault(e => e.MetricLabel == metricLabel)?.Unit
                                ?? usageEntries?
                                    .FirstOrDefault(e => e.MetricLabel == metricLabel)?.Unit;

                            var actualUsage = usageValue ?? 0;
                            double? remaining = null;
                            double? percentUsed = null;

                            if (limitValue.HasValue && limitValue.Value > 0)
                            {
                                // Int64.MaxValue or values >= 9e15 represent Unlimited quota in Google Cloud
                                if (limitValue.Value >= 9_000_000_000_000_000)
                                {
                                    remaining = null;
                                    percentUsed = 0.0;
                                }
                                else
                                {
                                    remaining = Math.Max(0, limitValue.Value - actualUsage);
                                    percentUsed = Math.Round(actualUsage / limitValue.Value * 100, 2);
                                }
                            }

                            response.Services.Add(new GoogleResourceDto
                            {
                                ServiceName = svc.DisplayName,
                                Enabled = true,
                                Limit = limitValue,
                                CurrentUsage = actualUsage,
                                Remaining = remaining,
                                PercentUsed = percentUsed,
                                Unit = unit,
                                QuotaMetric = metricLabel
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        // Graceful skip — log and continue with next service
                        _logger.LogWarning(ex,
                            "Failed to process quota for service {Service}, skipping",
                            svc.DisplayName);
                    }
                }

                // Sort by PercentUsed descending (highest usage first)
                response.Services = response.Services
                    .OrderByDescending(s => s.PercentUsed ?? 0)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve Google quota overview");
                throw;
            }

            return response;
        }

        // ────────────────────────────────────────────────────────────────────
        // Internal helpers
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Record for holding enabled service info extracted from Service Usage API.
        /// </summary>
        private sealed record EnabledServiceInfo(string ServiceName, string DisplayName);

        /// <summary>
        /// Record for a single metric data point from Cloud Monitoring.
        /// </summary>
        private sealed record MetricEntry(string MetricLabel, double Value, string? Unit);

        /// <summary>
        /// Lists all enabled services in the project using Service Usage API.
        /// </summary>
        private async Task<List<EnabledServiceInfo>> ListEnabledServicesAsync(
            CancellationToken ct)
        {
            var result = new List<EnabledServiceInfo>();

            var request = new Google.Cloud.ServiceUsage.V1.ListServicesRequest
            {
                Parent = $"projects/{_config.ProjectId}",
                Filter = "state:ENABLED"
            };

            var paginator = _serviceUsageClient.ListServicesAsync(request);

            await foreach (var service in paginator.WithCancellation(ct))
            {
                if (service?.Config == null) continue;

                // service.Config.Name is like "speech.googleapis.com"
                var serviceName = service.Config.Name ?? service.Name;
                var displayName = !string.IsNullOrWhiteSpace(service.Config.Title)
                    ? service.Config.Title
                    : serviceName;

                result.Add(new EnabledServiceInfo(serviceName, displayName));
            }

            return result;
        }

        /// <summary>
        /// Queries Cloud Monitoring for a specific quota metric type across all services.
        /// Returns a dictionary keyed by service name (e.g. "speech.googleapis.com").
        /// </summary>
        private async Task<Dictionary<string, List<MetricEntry>>> QueryQuotaMetricsAsync(
            string metricType, CancellationToken ct)
        {
            var result = new Dictionary<string, List<MetricEntry>>(
                StringComparer.OrdinalIgnoreCase);

            try
            {
                var now = DateTime.UtcNow;
                var request = new ListTimeSeriesRequest
                {
                    Name = $"projects/{_config.ProjectId}",
                    Filter = $"metric.type = \"{metricType}\"",
                    Interval = new TimeInterval
                    {
                        StartTime = Timestamp.FromDateTime(now.AddDays(-1)),
                        EndTime = Timestamp.FromDateTime(now)
                    },
                    View = ListTimeSeriesRequest.Types.TimeSeriesView.Full
                };

                var paginator = _metricClient.ListTimeSeriesAsync(request);

                await foreach (var ts in paginator.WithCancellation(ct))
                {
                    // Extract service name from resource labels
                    var serviceName = string.Empty;
                    if (ts.Resource?.Labels != null)
                    {
                        ts.Resource.Labels.TryGetValue("service", out serviceName);
                    }

                    if (string.IsNullOrEmpty(serviceName)) continue;

                    // Extract the quota_metric label (e.g. "apikeys.googleapis.com/read_requests")
                    var quotaMetricLabel = string.Empty;
                    if (ts.Metric?.Labels != null)
                    {
                        ts.Metric.Labels.TryGetValue("quota_metric", out quotaMetricLabel);
                    }

                    if (string.IsNullOrEmpty(quotaMetricLabel))
                    {
                        quotaMetricLabel = metricType;
                    }

                    // Get the most recent data point
                    var lastPoint = ts.Points?.LastOrDefault();
                    if (lastPoint == null) continue;

                    var value = lastPoint.Value?.Int64Value
                        ?? lastPoint.Value?.DoubleValue
                        ?? 0;

                    // Extract unit from metric descriptor if available
                    var unit = ts.Unit;

                    if (!result.ContainsKey(serviceName))
                    {
                        result[serviceName] = new List<MetricEntry>();
                    }

                    // Avoid duplicates — keep the latest value per quota_metric
                    var existing = result[serviceName]
                        .FindIndex(e => e.MetricLabel == quotaMetricLabel);
                    if (existing >= 0)
                    {
                        result[serviceName][existing] = new MetricEntry(
                            quotaMetricLabel, value, unit);
                    }
                    else
                    {
                        result[serviceName].Add(new MetricEntry(
                            quotaMetricLabel, value, unit));
                    }
                }
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound
                || ex.StatusCode == Grpc.Core.StatusCode.PermissionDenied)
            {
                _logger.LogWarning(
                    "Could not query metric {MetricType}: {Status} — {Detail}",
                    metricType, ex.StatusCode, ex.Status.Detail);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Unexpected error querying metric {MetricType}, returning empty",
                    metricType);
            }

            return result;
        }
    }
}
