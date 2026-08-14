using ai_speis_be.DTOs.GoogleQuota;

namespace ai_speis_be.Services.GoogleQuotaService
{
    /// <summary>
    /// Abstraction for querying Google Cloud quota and usage data.
    /// </summary>
    public interface IGoogleQuotaService
    {
        /// <summary>
        /// Retrieves an overview of quota usage for all enabled services in the project.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Unified quota response with per-service breakdown.</returns>
        Task<GoogleQuotaResponseDto> GetQuotaOverviewAsync(
            CancellationToken cancellationToken = default);
    }
}
