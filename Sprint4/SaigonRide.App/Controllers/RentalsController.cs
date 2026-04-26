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
        private readonly SepayService _sepayService; // 1. Đổi sang SepayService

        public RentalsController(AppDbContext context, SepayService sepayService) // 2. Inject SepayService
        {
            _context = context;
            _sepayService = sepayService;
        }

        // POST: api/rentals/start
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
            // Double booking guard
            if (!vehicle.IsActive || vehicle.Status == VehicleStatus.Maintenance || vehicle.Status == VehicleStatus.OutOfService)
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
                // Create Rental in PENDING status
                var newRental = new Rental
                {
                    UserId = userId,
                    VehicleId = vehicle.Id,
                    StartTime = DateTime.UtcNow,
                    Mode = (RentalMode)request.Mode,
                    Status = RentalStatus.Pending // Industry Standard: Start as Pending
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

                // We keep the vehicle as Available (or a special 'Pending' state) 
                // until payment is confirmed to prevent "locking" bikes with failed payments.

                await _context.SaveChangesAsync();

                // 4. Generate the VietQR Image URL via SepayService
                // Đưa amount và ID vào để sinh mã QR tĩnh chuẩn bị hiển thị trên Kiosk
                string qrImageUrl = _sepayService.GenerateQrUrl(calculatedDeposit, newRental.Id);

                await transaction.CommitAsync();

                return Ok(new
                {
                    Message = "Rental initiated. Please scan the QR code on the screen to pay the deposit.",
                    RentalId = newRental.Id,
                    QrUrl = qrImageUrl, // 3. Trả về QrUrl thay vì PaymentUrl
                    DepositAmount = calculatedDeposit
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { Message = "Error initiating rental.", Details = ex.Message });
            }
        }

        // POST: api/rentals/return/{rentalId}
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                rental.EndTime = DateTime.UtcNow;
                var duration = rental.EndTime.Value - rental.StartTime;

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

        // GET: api/rentals/history
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var history = await _context.Rentals
                .Include(r => r.Vehicle)
                .Include(r => r.Deposit)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.StartTime)
                .Select(r => new RentalHistoryViewModel
                {
                    RentalId = r.Id,
                    VehicleName = r.Vehicle.Name,
                    StartTime = r.StartTime.ToString("yyyy-MM-dd HH:mm"),
                    EndTime = r.EndTime.HasValue ? r.EndTime.Value.ToString("yyyy-MM-dd HH:mm") : "Ongoing",
                    Status = r.Status.ToString(),
                    TotalCost = r.TotalCost,
                    DepositAmount = r.Deposit != null ? r.Deposit.Amount : 0
                })
                .ToListAsync();

            return Ok(history);
        }
    }
}