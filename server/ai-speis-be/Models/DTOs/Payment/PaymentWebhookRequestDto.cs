using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.Models.DTOs.Payment
{
    public class PaymentWebhookRequestDto
    {
        [Range(0.01, 999999999)]
        public decimal Amount { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(64)]
        public string? OrderCode { get; set; }
    }
}
