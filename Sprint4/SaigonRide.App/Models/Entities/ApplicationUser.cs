using Microsoft.AspNetCore.Identity;

namespace SaigonRide.App.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        // For your UI and reporting
        public string FullName { get; set; } = string.Empty;

        // Crucial for vehicle rental legalities
        public string? LicenseNumber { get; set; } 
        
        // Tracking activity
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property: A user can have many rental records
        public virtual ICollection<Rental> Rentals { get; set; } = new List<Rental>();
    }
}