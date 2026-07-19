using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.Models.DTOs.Payment
{
    public class CreatePaymentRequestDto
    {
        [Range(1, int.MaxValue)]
        public int PackageId { get; set; }
    }
}
