using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaigonRide.App.Data;
using SaigonRide.App.Models.Entities;

var builder = WebApplication.CreateBuilder(args);

// 1. Get the connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// 2. Register your AppDbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString)); // <-- Changed from UseSqlServer

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// 3. Register ASP.NET Identity using your custom ApplicationUser
builder.Services.AddDefaultIdentity<ApplicationUser>(options => 
    {
        options.SignIn.RequireConfirmedAccount = false; // Keep it simple for dev
        options.Password.RequireDigit = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>() // Enables Role-based auth (Admin vs Customer)
    .AddEntityFrameworkStores<AppDbContext>();

// 4. Register MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// CRITICAL: Authentication must come before Authorization
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages(); // Required if you scaffold default Identity UI later

// --- Database Seeding ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbSeeder.SeedDataAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}
// ------------------------

app.Run(); 