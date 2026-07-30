using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.Models.DTOs.Payment
{
    public class CreatePaymentRequestDto
    {
        [Range(1, int.MaxValue)]
        public int PriceId { get; set; }

        // Checkout only supports two choices: no points, or the maximum usable balance.
        public bool UseRewardPoints { get; set; }
    }
}
