using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartRental.Data;
using SmartRental.Models;

namespace SmartRental.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalRooms = await _context.Phongtros.CountAsync();
            ViewBag.TotalFavorites = await _context.YeuThichs.CountAsync();
            ViewBag.ActiveRooms = await _context.Phongtros.CountAsync(p => p.TrangThai);
            ViewBag.FeaturedRooms = await _context.Phongtros.Include(p => p.PhongTienNghis).ThenInclude(pt => pt.TienNghi).Where(p => p.TrangThai).OrderByDescending(p => p.NgayDang).Take(4).ToListAsync();
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
