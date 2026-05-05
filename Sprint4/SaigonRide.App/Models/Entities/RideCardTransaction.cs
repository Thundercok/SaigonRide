namespace SaigonRide.App.Models.Entities
{
    public class RideCardTransaction
    {
        public int Id { get; set; }
        
        // This is the explicit CardId the controller is looking for
        public int CardId { get; set; } 
        public virtual RideCard Card { get; set; } = null!;

        public decimal Amount { get; set; }
        
        public TransactionType Type { get; set; }
        
        public string Reference { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum TransactionType
    {
        TopUp,
        FareDeduction,
        Refund,
        Adjustment
    }
}