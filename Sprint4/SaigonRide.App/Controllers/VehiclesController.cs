using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonRide.App.Data;
using SaigonRide.App.Models.Entities;

namespace SaigonRide.App.Controllers
{
    // These attributes tell .NET this is an API, not a traditional web page
    [Route("api/[controller]")]
    [ApiController]
    public class VehiclesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public VehiclesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/vehicles
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Vehicle>>> GetAvailableVehicles()
        {
            // The kiosk only needs to see bikes that are actually available to rent
            var availableBikes = await _context.Vehicles
                .Where(v => v.Status == VehicleStatus.Available && v.IsActive)
                .ToListAsync();

            if (!availableBikes.Any())
            {
                // Returns a 404 status code if the database is empty or all bikes are rented
                return NotFound(new { Message = "No vehicles are currently available." });
            }

            // Returns a 200 OK status code along with the JSON data
            return Ok(availableBikes);
        }
    }
}