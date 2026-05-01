using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonRide.App.Data;
using SaigonRide.App.Models.Entities;

namespace SaigonRide.App.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VehiclesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public VehiclesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var vehicles = await _context.Vehicles
                .Where(v => v.IsActive && v.Status == VehicleStatus.Available)
                .Select(v => new {
                    v.Id, v.Name, v.Grade,
                    v.HourlyRate, v.DailyRate, v.MarketValue
                })
                .ToListAsync();

            return Ok(vehicles);
        }
    }
}