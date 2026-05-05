namespace SaigonRide.App.Models.Entities;

public class RideCard
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser User { get; set; } = null!;

    public decimal Balance { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<RideCardTransaction> Transactions { get; set; } = new List<RideCardTransaction>();
}

public class RideCardTransaction
{
    public int Id { get; set; }

    public int RideCardId { get; set; }
    public virtual RideCard RideCard { get; set; } = null!;

    public int? RentalId { get; set; }
    public virtual Rental? Rental { get; set; }

    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public RideCardTransactionType Type { get; set; }
    public RideCardTransactionStatus Status { get; set; } = RideCardTransactionStatus.Pending;
    public PaymentProvider Provider { get; set; } = PaymentProvider.Wallet;
    public string? ExternalReference { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}

public enum RideCardTransactionType
{
    TopUp,
    FareDeduction,
    Refund,
    Adjustment
}

public enum RideCardTransactionStatus
{
    Pending,
    Completed,
    Failed,
    Cancelled
}

public enum PaymentProvider
{
    Wallet,
    Stripe,
    SePay,
    Admin
}
