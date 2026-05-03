using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SaigonRide.App.Data;
using SaigonRide.App.Models.Entities;
using SaigonRide.App.Services;
using SaigonRide.App.Settings;
using Stripe;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache();

// --- 1. Database Configuration ---
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
                      ?? builder.Configuration["DATABASE_URL"]
                      ?? builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? throw new InvalidOperationException("No connection string found.");
    string connStr;
    if (databaseUrl.StartsWith("postgresql://") || databaseUrl.StartsWith("postgres://"))
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        connStr = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host     = uri.Host,
            Port     = uri.Port == -1 ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = userInfo[0],
            Password = userInfo.Length > 1 ? userInfo[1] : "",
            SslMode  = Npgsql.SslMode.Require,
        }.ConnectionString;
    }
    else
    {
        connStr = databaseUrl;
    }

    options.UseNpgsql(connStr);
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// --- 2. Identity & Security ---
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit           = false;
        options.Password.RequireNonAlphanumeric  = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
});

// --- JWT ---
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSettings["Issuer"],
            ValidAudience            = jwtSettings["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
        };
    });

// --- 3. Stripe ---
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// --- 4. Custom Services ---
builder.Services.AddScoped<SepayService>();
builder.Services.AddHostedService<PendingRentalTimeoutWorker>();
builder.Services.AddHostedService<StationUtilisationWorker>();

// --- 5. MVC ---
builder.Services.AddControllersWithViews();

// ── BUILD ────────────────────────────────────────────────────────────────────
var app = builder.Build();

// --- 6. Middleware Pipeline ---
app.UseDeveloperExceptionPage();

// Raw body buffering for Stripe webhook — before UseRouting
app.Use(async (ctx, next) =>
{
    ctx.Request.EnableBuffering();
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// --- 7. Migrate & Seed ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

using (var scope = app.Services.CreateScope())
{
    try
    {
        await DbSeeder.SeedDataAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

app.Run();