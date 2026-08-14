using ai_speis_be.DTOs.GoogleQuota;

namespace ai_speis_be.Services.GoogleQuotaService
{
    /// <summary>
    /// Service interface for querying Google Cloud Billing Export cost data from BigQuery.
    /// </summary>
    public interface ICloudCostService
    {
        /// <summary>
        /// Retrieves Google Cloud Billing cost information from BigQuery Export table.
        /// Returns HasData = false if table/dataset is not configured or queries fail.
        /// </summary>
        Task<CloudCostDto> GetCloudCostAsync(CancellationToken cancellationToken = default);
    }
}
