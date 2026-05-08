using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonRide.App.Data;
using SaigonRide.App.Models.Entities;

namespace SaigonRide.App.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var rideCard = await _db.RideCards
            .FirstOrDefaultAsync(c => c.UserId == userId);

        var activeRental = await _db.Rentals
            .Include(r => r.Vehicle)
            .Include(r => r.StartStation)
            .Where(r => r.UserId == userId && r.Status == RentalStatus.Active)
            .FirstOrDefaultAsync();

        var recentRentals = await _db.Rentals
            .Include(r => r.Vehicle)
            .Include(r => r.StartStation)
            .Include(r => r.EndStation)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.Id)
            .Take(8)
            .ToListAsync();

        var recentTransactions = await _db.RideCardTransactions
            .Where(t => t.RideCard.UserId == userId)
            .OrderByDescending(t => t.Id)
            .Take(5)
            .ToListAsync();

        ViewBag.WalletBalance      = rideCard?.Balance ?? 0m;
        ViewBag.ActiveRental       = activeRental;
        ViewBag.RecentRentals      = recentRentals;
        ViewBag.RecentTransactions = recentTransactions;

        return View();
    }
}