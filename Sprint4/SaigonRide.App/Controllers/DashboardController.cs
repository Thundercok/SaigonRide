using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonRide.App.Data;
using SaigonRide.App.Models.Entities;
using SaigonRide.App.Models.ViewModels;

namespace SaigonRide.App.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db          = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

        // Active rental
        var activeRental = await _db.Rentals
            .Include(r => r.Vehicle)
            .Include(r => r.StartStation)
            .Where(r => r.UserId == user.Id && r.Status == RentalStatus.Active)
            .FirstOrDefaultAsync();

        ActiveRentalViewModel? activeVm = null;
        if (activeRental != null && activeRental.StartTime.HasValue)
        {
            var elapsed = DateTime.UtcNow - activeRental.StartTime.Value;
            activeVm = new ActiveRentalViewModel
            {
                RentalId          = activeRental.Id,
                VehicleName       = activeRental.Vehicle.Name,
                LicensePlate      = activeRental.Vehicle.LicensePlate,
                StartStationName  = activeRental.StartStation.Name,
                StartStationLat   = activeRental.StartStation.Latitude,
                StartStationLng   = activeRental.StartStation.Longitude,
                StartTime         = activeRental.StartTime.Value,
                ElapsedTime       = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}",
                HourlyRate        = activeRental.Vehicle.HourlyRate,
            };
        }

        // Last 10 completed/cancelled rentals
        var history = await _db.Rentals
            .Include(r => r.Vehicle)
            .Where(r => r.UserId == user.Id
                     && (r.Status == RentalStatus.Completed || r.Status == RentalStatus.Cancelled))
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .Select(r => new RentalHistoryViewModel
            {
                RentalId    = r.Id,
                VehicleName = r.Vehicle.Name,
                StartTime   = r.StartTime.HasValue
                    ? r.StartTime.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                    : r.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                EndTime     = r.EndTime.HasValue
                    ? r.EndTime.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                    : null,
                Status      = r.Status.ToString(),
                TotalCost   = r.TotalCost,
            })
            .ToListAsync();

        var vm = new DashboardViewModel
        {
            UserName    = user.FullName ?? user.Email ?? "Khách",
            ActiveRental = activeVm,
            History      = history,
        };

        return View(vm);
    }
}