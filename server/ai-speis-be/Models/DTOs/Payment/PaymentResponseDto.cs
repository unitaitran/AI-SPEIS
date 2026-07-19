namespace ai_speis_be.Models.DTOs.Payment
{
    public class PaymentResponseDto
    {
        public int PaymentId { get; set; }
        public int UserId { get; set; }
        public int PackageId { get; set; }
        public decimal Amount { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string PayUrl { get; set; } = string.Empty;
    }
}
