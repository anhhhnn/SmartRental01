using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using SmartRental.Data;
using SmartRental.Models;

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

var app = builder.Build();

await IdentitySeed.SeedRolesAsync(app.Services);
if (app.Environment.IsDevelopment())
{
    await DemoDataSeed.SeedAsync(app.Services);
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
