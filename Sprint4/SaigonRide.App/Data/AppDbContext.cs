using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SaigonRide.App.Models.Entities;

namespace SaigonRide.App.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Your custom tables
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<Rental> Rentals { get; set; }
        public DbSet<Deposit> Deposits { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // CRITICAL: Must call the base method first so Identity tables are configured
            base.OnModelCreating(builder); 

            // 1-to-1: A Rental has exactly one Deposit
            builder.Entity<Rental>()
                .HasOne(r => r.Deposit)
                .WithOne(d => d.Rental)
                .HasForeignKey<Deposit>(d => d.RentalId)
                .OnDelete(DeleteBehavior.Cascade); // If rental is deleted, delete the deposit record

            // 1-to-Many: User to Rentals
            builder.Entity<Rental>()
                .HasOne(r => r.User)
                .WithMany(u => u.Rentals)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting a user if they have active rentals

            // 1-to-Many: Vehicle to Rentals
            builder.Entity<Rental>()
                .HasOne(r => r.Vehicle)
                .WithMany(v => v.Rentals)
                .HasForeignKey(r => r.VehicleId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting an e-bike if it has rental history
        }
    }
}