namespace SaigonRide.App.Models.Entities
{
    public class Rental
    {
        public int Id { get; set; }

        // Foreign Keys linking the User and the E-Bike
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; } = null!;

        public int VehicleId { get; set; }
        public virtual Vehicle Vehicle { get; set; } = null!;

        // Time tracking
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }

        // Pricing & Billing
        public RentalMode Mode { get; set; } // Hourly or Daily
        public decimal TotalCost { get; set; } // Calculated at the end of the trip

        // Status tracking
        public RentalStatus Status { get; set; } = RentalStatus.Pending;

        // Navigation property: One rental has one deposit record
        public virtual Deposit? Deposit { get; set; }
    }

    public enum RentalMode { Hourly, Daily }
    public enum RentalStatus { Pending, Active, Completed, Cancelled }
}