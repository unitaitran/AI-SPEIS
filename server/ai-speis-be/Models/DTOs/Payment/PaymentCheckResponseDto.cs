namespace ai_speis_be.Models.DTOs.Payment
{
    public class PaymentCheckResponseDto
    {
        public string OrderCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int PackageId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public bool IsExpired { get; set; }
    }
}
