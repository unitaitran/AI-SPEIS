namespace ai_speis_be.Models.Enums
{
    public enum BillingCycle
    {
        Monthly = 1,
        Yearly = 2
    }

    public enum UserSubscriptionStatus
    {
        Pending = 0,
        Active = 1,
        Expired = 2,
        Cancelled = 3
    }

    public enum SubscriptionTermStatus
    {
        Scheduled = 0,
        Active = 1,
        Completed = 2,
        Cancelled = 3
    }

    public enum QuotaTransactionType
    {
        Reserve = 0,
        Consume = 1,
        Refund = 2,
        Reset = 3
    }

    public enum RewardTransactionType
    {
        Earn = 0,
        Reserve = 1,
        Redeem = 2,
        Release = 3,
        Refund = 4
    }
}
