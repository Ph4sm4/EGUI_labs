using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OutdoorRental.Data;
using OutdoorRental.Models.Entities;
using OutdoorRental.Models.ViewModels;

namespace OutdoorRental.Controllers;

// rental points. anyone can look, only admin can change
[Authorize(Roles = DbSeeder.AdminRole)]
public class LocationsController : Controller
{
    private readonly ApplicationDbContext _db;

    public LocationsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var locations = await _db.RentalLocations
            .OrderBy(l => l.City).ThenBy(l => l.Name)
            .Select(l => new LocationListItemViewModel
            {
                Location = l,
                UnitCount = l.Units.Count,
                AvailableCount = l.Units.Count(u => u.Status == UnitStatus.Available)
            })
            .ToListAsync();
        return View(locations);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var location = await _db.RentalLocations
            .Include(l => l.Units).ThenInclude(u => u.Equipment).ThenInclude(e => e!.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);
        if (location == null) return NotFound();
        return View(location);
    }

    public IActionResult Create() => View(new RentalLocation());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,City,Address,Phone")] RentalLocation location)
    {
        await ValidateUniqueNameAsync(location);
        if (!ModelState.IsValid)
            return View(location);

        _db.RentalLocations.Add(location);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Rental point \"{location.Name}\" has been created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var location = await _db.RentalLocations.FindAsync(id);
        if (location == null) return NotFound();
        return View(location);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,City,Address,Phone")] RentalLocation input)
    {
        if (id != input.Id) return NotFound();

        var location = await _db.RentalLocations.FindAsync(id);
        if (location == null) return NotFound();

        await ValidateUniqueNameAsync(input);
        if (!ModelState.IsValid)
            return View(input);

        location.Name = input.Name;
        location.City = input.City;
        location.Address = input.Address;
        location.Phone = input.Phone;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Rental point \"{location.Name}\" has been updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var location = await _db.RentalLocations
            .Include(l => l.Units)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);
        if (location == null) return NotFound();
        ViewBag.CanDelete = await CanDeleteAsync(location.Id);
        return View(location);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var location = await _db.RentalLocations.FindAsync(id);
        if (location == null) return NotFound();

        if (!await CanDeleteAsync(id))
        {
            TempData["Error"] = $"\"{location.Name}\" still holds equipment or appears in rental history. " +
                                "Move its equipment to another rental point first.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        _db.RentalLocations.Remove(location);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Rental point \"{location.Name}\" has been deleted.";
        return RedirectToAction(nameof(Index));
    }

    // only empty points with no rental history can go
    private async Task<bool> CanDeleteAsync(int id) =>
        !await _db.EquipmentUnits.AnyAsync(u => u.LocationId == id) &&
        !await _db.Rentals.AnyAsync(r => r.PickupLocationId == id || r.ReturnLocationId == id);

    private async Task ValidateUniqueNameAsync(RentalLocation location)
    {
        var name = location.Name?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        location.Name = name;
        if (await _db.RentalLocations.AnyAsync(l => l.Name == name && l.Id != location.Id))
            ModelState.AddModelError(nameof(RentalLocation.Name), "A rental point with this name already exists.");
    }
}
