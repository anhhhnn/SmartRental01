using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using SmartRental.Data;
using SmartRental.Models;
using SmartRental.Services;

var builder = WebApplication.CreateBuilder(args);

// Kết nối Entity Framework với SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.MaxLengthForKeys = 450;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSingleton<GeoDistanceService>();
builder.Services.AddHttpClient<AiSearchService>((sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(
        configuration["AiService:BaseUrl"] ?? "http://127.0.0.1:8000");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient<RecommendationService>((sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(
        configuration["AiService:BaseUrl"] ?? "http://127.0.0.1:8000");
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

if (args.Contains("--seed-room-coordinates-only", StringComparer.OrdinalIgnoreCase))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Room coordinate seeding is only available in Development.");
    }

    await RoomCoordinateSeeder.SeedAsync(app.Services);
    return;
}

if (args.Contains("--seed-demo-data-only", StringComparer.OrdinalIgnoreCase))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Demo data seeding is only available in Development.");
    }

    await IdentitySeed.SeedRolesAsync(app.Services);
    await DemoDataSeed.SeedAsync(app.Services);
    await RoomCoordinateSeeder.SeedAsync(app.Services);
    return;
}

await IdentitySeed.SeedRolesAsync(app.Services);
if (app.Environment.IsDevelopment())
{
    await DiaDiemSeed.SeedAsync(app.Services);
    await DemoDataSeed.SeedAsync(app.Services);
    await RoomCoordinateSeeder.SeedAsync(app.Services);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Phongtro}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages();

app.Run();
