namespace ai_speis_be.Models.Enums
{
    public enum PaymentStatus
    {
        Pending = 0,
        Paid = 1,
        Expired = 2,
        Failed = 3,
        PaidByReward = 4,
        Cancelled = 5,
        Refunded = 6
    }
}
