using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OutdoorRental.Data;
using OutdoorRental.Models;
using OutdoorRental.Models.Entities;
using OutdoorRental.Models.ViewModels;

namespace OutdoorRental.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeViewModel
        {
            EquipmentCount = await _db.Equipment.CountAsync(),
            AvailableUnitCount = await _db.EquipmentUnits.CountAsync(u => u.Status == UnitStatus.Available),
            LocationCount = await _db.RentalLocations.CountAsync(),
            CityCount = await _db.RentalLocations.Select(l => l.City).Distinct().CountAsync()
        };

        // extra numbers only admins care about
        if (User.IsInRole(DbSeeder.AdminRole))
        {
            model.CategoryCount = await _db.Categories.CountAsync();
            model.UserCount = await _db.Users.CountAsync();
        }

        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
