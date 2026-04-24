using System.Security.Claims;
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
    [Authorize] // CRITICAL: This locks the door. Only logged-in users can rent.
    public class RentalsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RentalsController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/rentals/start
        [HttpPost("start")]
        public async Task<IActionResult> StartRental([FromBody] RentalRequest request)
        {
            // 1. Identify who is making the request
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            // 2. Find the requested E-Bike and ensure it's actually available
            var vehicle = await _context.Vehicles.FindAsync(request.VehicleId);
            if (vehicle == null) return NotFound(new { Message = "Vehicle not found." });
            
            if (vehicle.Status != VehicleStatus.Available || !vehicle.IsActive)
            {
                return BadRequest(new { Message = "This vehicle is currently not available for rent." });
            }

            // 3. The Impressive Part: Dynamic Deposit Calculation
            // Grade A = 20%, Grade B = 15%, Grade C = 10%
            decimal depositPercentage = vehicle.Grade switch
            {
                VehicleGrade.GradeA => 0.20m,
                VehicleGrade.GradeB => 0.15m,
                VehicleGrade.GradeC => 0.10m,
                _ => 0.15m // fallback
            };
            
            decimal calculatedDeposit = vehicle.MarketValue * depositPercentage;

            // 4. Create the Rental Record
            var newRental = new Rental
            {
                UserId = userId,
                VehicleId = vehicle.Id,
                StartTime = DateTime.UtcNow,
                Mode = request.Mode,
                Status = RentalStatus.Active
            };

            // 5. Create the linked Deposit Record
            var newDeposit = new Deposit
            {
                Rental = newRental,
                Amount = calculatedDeposit,
                Status = DepositStatus.Held,
                CreatedAt = DateTime.UtcNow
            };

            // 6. Update the Vehicle Status so no one else can rent it
            vehicle.Status = VehicleStatus.Rented;

            // 7. Save everything to the SQLite database in one transaction
            _context.Rentals.Add(newRental);
            _context.Deposits.Add(newDeposit);
            await _context.SaveChangesAsync();

            // 8. Return the receipt to the Kiosk
            return Ok(new
            {
                Message = "Rental started successfully!",
                RentalId = newRental.Id,
                VehicleName = vehicle.Name,
                Mode = request.Mode.ToString(),
                RequiredDeposit = calculatedDeposit
            });
        }
    }
}