using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonRide.App.Data;
using SaigonRide.App.Models.Entities;
using SaigonRide.App.Models.ViewModels;
using SaigonRide.App.Services;

namespace SaigonRide.App.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RentalsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly SepayService _sepayService; 

        public RentalsController(AppDbContext context, SepayService sepayService) 
        {
            _context = context;
            _sepayService = sepayService;
        }

        // ─── START RENTAL ──────────────────────────────────────────────────────────
        [HttpPost("start")]
        public async Task<IActionResult> StartRental([FromBody] RentalRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            // 1. One active/pending rental limit per user
            var hasActiveRental = await _context.Rentals
                .AnyAsync(r => r.UserId == userId &&
                               (r.Status == RentalStatus.Active || r.Status == RentalStatus.Pending));

            if (hasActiveRental)
                return BadRequest(new { Message = "You already have an active or pending rental." });

            // 2. Availability check
            var vehicle = await _context.Vehicles.FindAsync(request.VehicleId);
            if (vehicle == null) return NotFound(new { Message = "Vehicle not found." });
            
            if (!vehicle.IsActive || vehicle.Status == VehicleStatus.Maintenance ||
                vehicle.Status == VehicleStatus.OutOfService)
                return BadRequest(new { Message = "Vehicle is not available for rent." });

            var vehicleOccupied = await _context.Rentals
                .AnyAsync(r => r.VehicleId == request.VehicleId &&
                               (r.Status == RentalStatus.Active || r.Status == RentalStatus.Pending));
            if (vehicleOccupied)
                return BadRequest(new { Message = "Vehicle is currently unavailable." });

            // 3. Calculate Deposit based on Grade
            decimal depositPercentage = vehicle.Grade switch
            {
                VehicleGrade.GradeA => 0.20m,
                VehicleGrade.GradeB => 0.15m,
                VehicleGrade.GradeC => 0.10m,
                _ => 0.15m
            };
            decimal calculatedDeposit = vehicle.MarketValue * depositPercentage;

            // --- TRANSACTION START ---
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Create Rental in PENDING status. NO StartTime yet!
                var newRental = new Rental
                {
                    UserId = userId,
                    VehicleId = vehicle.Id,
                    Mode = (RentalMode)request.Mode,
                    Status = RentalStatus.Pending 
                };

                var newDeposit = new Deposit
                {
                    Rental = newRental,
                    Amount = calculatedDeposit,
                    Status = DepositStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Rentals.Add(newRental);
                _context.Deposits.Add(newDeposit);
                await _context.SaveChangesAsync();

                // 4. Generate the VietQR Image URL via SepayService
                string qrImageUrl = _sepayService.GenerateQrUrl(calculatedDeposit, newRental.Id);

                await transaction.CommitAsync();

                return Ok(new
                {
                    Message = "Rental initiated. Please scan the QR code on the screen to pay the deposit.",
                    RentalId = newRental.Id,
                    QrUrl = qrImageUrl, 
                    DepositAmount = calculatedDeposit
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { Message = "Error initiating rental.", Details = ex.Message });
            }
        }

        // ─── STATUS FOR KIOSK POLLING ─────────────────────────────────────────────
        [HttpGet("{id}/status")]
        public async Task<IActionResult> GetRentalStatus(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var rental = await _context.Rentals
                .Where(r => r.Id == id && r.UserId == userId)
                .Select(r => new { r.Id, r.Status })
                .FirstOrDefaultAsync();

            if (rental == null) return NotFound();
            
            return Ok(new { status = rental.Status.ToString() });
        }

        // ─── RETURN RENTAL ────────────────────────────────────────────────────────
        [HttpPost("return/{rentalId}")]
        public async Task<IActionResult> ReturnRental(int rentalId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var rental = await _context.Rentals
                .Include(r => r.Vehicle)
                .Include(r => r.Deposit)
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null || rental.UserId != userId)
                return NotFound(new { Message = "Rental record not found." });

            if (rental.Status != RentalStatus.Active)
                return BadRequest(new { Message = "This rental is not currently active." });

            if (rental.StartTime == null)
                return BadRequest(new { Message = "Rental has not started yet." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                rental.EndTime = DateTime.UtcNow;
                var duration = rental.EndTime.Value - rental.StartTime.Value;

                decimal totalCost = 0;
                if (rental.Mode == RentalMode.Hourly)
                {
                    var billedHours = (decimal)Math.Ceiling(duration.TotalHours);
                    if (billedHours < 1) billedHours = 1;
                    totalCost = billedHours * rental.Vehicle.HourlyRate;
                }
                else
                {
                    var billedDays = (decimal)Math.Ceiling(duration.TotalDays);
                    if (billedDays < 1) billedDays = 1;
                    totalCost = billedDays * rental.Vehicle.DailyRate;
                }

                rental.TotalCost = totalCost;
                rental.Status = RentalStatus.Completed;
                rental.Vehicle.Status = VehicleStatus.Available;

                if (rental.Deposit != null)
                {
                    rental.Deposit.Status = DepositStatus.Refunded;
                    rental.Deposit.ProcessedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    Message = "Vehicle returned successfully!",
                    Duration = $"{duration.Hours}h {duration.Minutes}m",
                    FinalCost = totalCost,
                    DepositStatus = "Refunded"
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { Message = "Error processing return." });
            }
        }

        // ─── RENTAL HISTORY ───────────────────────────────────────────────────────
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var history = await _context.Rentals
                .Include(r => r.Vehicle)
                .Include(r => r.Deposit)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.Id) 
                .Select(r => new RentalHistoryViewModel
                {
                    RentalId = r.Id,
                    VehicleName = r.Vehicle.Name,
                    StartTime = r.StartTime.HasValue ? r.StartTime.Value.ToString("yyyy-MM-dd HH:mm") : "Pending",
                    EndTime = r.EndTime.HasValue ? r.EndTime.Value.ToString("yyyy-MM-dd HH:mm") : "Ongoing",
                    Status = r.Status.ToString(),
                    TotalCost = r.TotalCost,
                    DepositAmount = r.Deposit != null ? r.Deposit.Amount : 0
                })
                .ToListAsync();

            return Ok(history);
        }

        // ─── CANCEL PENDING RENTAL ────────────────────────────────────────────────
        [HttpDelete("{id}/cancel")]
        public async Task<IActionResult> CancelRental(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var rental = await _context.Rentals
                .Include(r => r.Deposit)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rental == null) return NotFound(new { Message = "Rental not found." });
            if (rental.UserId != userId) return Forbid();
            if (rental.Status != RentalStatus.Pending)
                return BadRequest(new { Message = "Only pending rentals can be cancelled." });

            rental.Status = RentalStatus.Cancelled;
            if (rental.Deposit != null)
            {
                rental.Deposit.Status = DepositStatus.Cancelled;
                rental.Deposit.ProcessedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Rental cancelled." });
        }
    }
}