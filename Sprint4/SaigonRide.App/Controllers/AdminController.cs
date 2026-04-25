using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonRide.App.Data;
using SaigonRide.App.Models.Entities;
using SaigonRide.App.Models.ViewModels;

namespace SaigonRide.App.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")] // Only the seeded Admin can hit this
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("fleet-overview")]
        public async Task<IActionResult> GetFleetOverview()
        {
            var vehicles = await _context.Vehicles
                .Include(v => v.Rentals.Where(r => r.Status == RentalStatus.Active))
                .ThenInclude(r => r.User)
                .ToListAsync();

            var heldDeposits = await _context.Deposits
                .Where(d => d.Status == DepositStatus.Held)
                .SumAsync(d => d.Amount);

            var overview = new FleetStatusViewModel
            {
                TotalVehicles = vehicles.Count,
                CurrentlyRented = vehicles.Count(v => v.Status == VehicleStatus.Rented),
                Available = vehicles.Count(v => v.Status == VehicleStatus.Available),
                TotalDepositsHeld = heldDeposits,
                VehicleDetails = vehicles.Select(v => new VehicleLocationSummary
                {
                    Name = v.Name,
                    Plate = v.LicensePlate,
                    Status = v.Status.ToString(),
                    CurrentUser = v.Rentals.FirstOrDefault()?.User?.FullName
                }).ToList()
            };

            return Ok(overview);
        }
    }
}