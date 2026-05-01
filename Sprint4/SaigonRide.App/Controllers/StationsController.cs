namespace SaigonRide.App.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonRide.App.Data;
using SaigonRide.App.Models.Entities;
using SaigonRide.App.Models.ViewModels;

[ApiController]
[Route("api/stations")]
public class StationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public StationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var stations = await _db.Stations
            .Where(s => s.IsActive)
            .Select(s => new StationViewModel
            {
                Id = s.Id,
                Name = s.Name,
                Address = s.Address,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                Capacity = s.Capacity,
                BikesAvailable = s.Vehicles.Count(v => v.Status == VehicleStatus.Available)
            })
            .ToListAsync();

        return Ok(stations);
    }

    [HttpGet("{id}/utilisation")]
    public async Task<IActionResult> GetUtilisation(int id)
    {
        var station = await _db.Stations
            .Where(s => s.Id == id && s.IsActive)
            .Select(s => new StationViewModel
            {
                Id = s.Id,
                Name = s.Name,
                Address = s.Address,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                Capacity = s.Capacity,
                BikesAvailable = s.Vehicles.Count(v => v.Status == VehicleStatus.Available)
            })
            .FirstOrDefaultAsync();

        if (station == null) return NotFound();

        var since = DateTime.UtcNow.AddHours(-24);
        var logs = await _db.StationUtilisationLogs
            .Where(l => l.StationId == id && l.HourSlot >= since)
            .OrderBy(l => l.HourSlot)
            .Select(l => new HourlyLogViewModel
            {
                HourSlot = l.HourSlot,
                BikesAvailable = l.BikesAvailable,
                Capacity = l.Capacity,
                RentalsStarted = l.RentalsStarted,
                RentalsEnded = l.RentalsEnded
            })
            .ToListAsync();

        return Ok(new StationUtilisationViewModel { Station = station, HourlyLogs = logs });
    }
}