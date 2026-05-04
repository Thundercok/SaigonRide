using Microsoft.AspNetCore.Identity;
using SaigonRide.App.Models.Entities;

namespace SaigonRide.App.Data
{
    public static class DbSeeder
    {
        public static async Task SeedDataAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<AppDbContext>();
            var env = serviceProvider.GetRequiredService<IWebHostEnvironment>();

            // Test user for Playwright
            if (await userManager.FindByNameAsync("test@saigonride.com") == null)
            {
                var testUser = new ApplicationUser
                {
                    UserName    = "test@saigonride.com",
                    Email       = "test@saigonride.com",
                    PhoneNumber = "0901234567",
                    FullName    = "Test User",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(testUser, "Test123!");
            }
            // 1. Seed Roles
            string[] roles = { "Admin", "Customer" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Seed Default Admin User
            if (await userManager.FindByEmailAsync("admin@saigonride.com") == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = "admin@saigonride.com",
                    Email = "admin@saigonride.com",
                    FullName = "System Admin",
                    EmailConfirmed = true // Skips the email verification step for dev
                };
                
                var result = await userManager.CreateAsync(adminUser, "Admin123!"); // The default password
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
            // 3. Seed Kiosk System Account
            string[] kioskRoles = { "Admin", "Customer", "Kiosk" };
            foreach (var role in kioskRoles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            if (await userManager.FindByEmailAsync("kiosk@saigonride.com") == null)
            {
                var kioskUser = new ApplicationUser
                {
                    UserName = "kiosk@saigonride.com",
                    Email = "kiosk@saigonride.com",
                    FullName = "Kiosk System",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(kioskUser, "Kiosk@Internal99!");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(kioskUser, "Kiosk");
            }

            // 4. Seed Stations
            if (!context.Stations.Any())
            {
                context.Stations.AddRange(
                    new Station
                    {
                        Id        = 1,
                        Name      = "Ton Duc Thang Hub",
                        Address   = "7 Nguyen Huu Tho, Phuong Ben Thanh TP.HCM",
                        Latitude  = 10.7725,
                        Longitude = 106.6980,
                        Capacity  = 20,
                        IsActive  = true
                    },
                    new Station
                    {
                        Id        = 2,
                        Name      = "Bình Thạnh Hub",
                        Address   = "Depot Metro Line 1, Bình Thạnh, TP.HCM",
                        Latitude  = 10.8009,
                        Longitude = 106.7157,
                        Capacity  = 15,
                        IsActive  = true
                    }
                );
                await context.SaveChangesAsync();
            }

            // 5. Seed Initial E-Bikes for the UI
            if (!context.Vehicles.Any())
            {
                context.Vehicles.AddRange(
                    new Vehicle 
                    { 
                        Name = "Wave Electric Alpha", 
                        LicensePlate = "11-11 111111", 
                        Grade = VehicleGrade.GradeA, 
                        MarketValue = 1500000m,
                        HourlyRate = 15000m,
                        DailyRate = 100000m,
                        StationId = 1
                    },
                    new Vehicle 
                    { 
                        Name = "Super Electric Dream", 
                        LicensePlate = "11-11 11112", 
                        Grade = VehicleGrade.GradeB, 
                        MarketValue = 1200000m,
                        HourlyRate = 12000m,
                        DailyRate = 80000m,
                        StationId = 1
                    }
                );
                await context.SaveChangesAsync();
            }

            var unassignedVehicles = context.Vehicles.Where(v => v.StationId == null).ToList();
            foreach (var vehicle in unassignedVehicles)
            {
                vehicle.StationId = 1;
            }

            if (unassignedVehicles.Count > 0)
            {
                await context.SaveChangesAsync();
            }

            if (env.IsDevelopment())
            {
                var testUser = await userManager.FindByNameAsync("test@saigonride.com");
                if (testUser != null)
                {
                    var openTestRentals = context.Rentals
                        .Where(r => r.UserId == testUser.Id
                            && (r.Status == RentalStatus.Pending || r.Status == RentalStatus.Active))
                        .ToList();

                    foreach (var rental in openTestRentals)
                    {
                        rental.Status = RentalStatus.Cancelled;
                        rental.EndTime ??= DateTime.UtcNow;

                        var vehicle = context.Vehicles.Find(rental.VehicleId);
                        if (vehicle != null)
                        {
                            vehicle.Status = VehicleStatus.Available;
                            vehicle.StationId ??= 1;
                        }
                    }

                    if (openTestRentals.Count > 0)
                    {
                        await context.SaveChangesAsync();
                    }
                }
            }
        }
    }
}
