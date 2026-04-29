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

            // 3. Seed Initial E-Bikes for the UI
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
                        DailyRate = 100000m
                    },
                    new Vehicle 
                    { 
                        Name = "Super Electric Dream", 
                        LicensePlate = "11-11 11112", 
                        Grade = VehicleGrade.GradeB, 
                        MarketValue = 1200000m,
                        HourlyRate = 12000m,
                        DailyRate = 80000m
                    }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}