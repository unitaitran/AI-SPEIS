namespace ai_speis_be.Services.GoogleQuotaService
{
    /// <summary>
    /// Configuration for connecting to Google Cloud APIs.
    /// Reads credentials path and project ID from environment variables
    /// so nothing is hard-coded.
    /// </summary>
    public sealed class GoogleQuotaConfig
    {
        /// <summary>
        /// Google Cloud project ID (e.g. "coastal-sector-465316-u8").
        /// Loaded from env GOOGLE_CLOUD_PROJECT.
        /// </summary>
        public string ProjectId { get; }

        /// <summary>
        /// Absolute path to the Service Account JSON key file.
        /// Loaded from env GOOGLE_QUOTA_CREDENTIALS.
        /// </summary>
        public string CredentialsPath { get; }

        /// <summary>
        /// BigQuery dataset name for Google Cloud Billing export.
        /// Loaded from env GOOGLE_BIGQUERY_BILLING_DATASET.
        /// </summary>
        public string BillingDataset { get; }

        /// <summary>
        /// BigQuery table name for Google Cloud Billing export.
        /// Loaded from env GOOGLE_BIGQUERY_BILLING_TABLE.
        /// </summary>
        public string BillingTable { get; }

        public bool IsConfigured { get; }

        public GoogleQuotaConfig()
        {
            ProjectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? string.Empty;
            var raw = Environment.GetEnvironmentVariable("GOOGLE_QUOTA_CREDENTIALS") ?? string.Empty;

            CredentialsPath = !string.IsNullOrWhiteSpace(raw)
                ? (Path.IsPathRooted(raw) ? raw : Path.GetFullPath(raw))
                : string.Empty;

            BillingDataset = Environment.GetEnvironmentVariable("GOOGLE_BIGQUERY_BILLING_DATASET") ?? string.Empty;
            BillingTable = Environment.GetEnvironmentVariable("GOOGLE_BIGQUERY_BILLING_TABLE") ?? string.Empty;

            IsConfigured = !string.IsNullOrWhiteSpace(ProjectId)
                && !string.IsNullOrWhiteSpace(CredentialsPath)
                && File.Exists(CredentialsPath);
        }
    }
}
