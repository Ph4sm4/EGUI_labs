using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OutdoorRental.Data;
using OutdoorRental.Models.Entities;
using OutdoorRental.Models.ViewModels;

namespace OutdoorRental.Controllers;

// equipment = a batch, units = the actual pieces. anyone can look, only admin can touch
[Authorize(Roles = DbSeeder.AdminRole)]
public class EquipmentController : Controller
{
    private readonly ApplicationDbContext _db;

    public EquipmentController(ApplicationDbContext db)
    {
        _db = db;
    }

    // browsing

    [AllowAnonymous]
    public async Task<IActionResult> Index(string? search, int? categoryId)
    {
        var query = _db.Equipment.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(e => e.Name.Contains(s) || e.InventoryNumber.Contains(s) ||
                                     (e.Description != null && e.Description.Contains(s)));
        }
        if (categoryId.HasValue)
            query = query.Where(e => e.CategoryId == categoryId);

        var items = await query
            .OrderBy(e => e.Name)
            .Select(e => new EquipmentListItemViewModel
            {
                Id = e.Id,
                InventoryNumber = e.InventoryNumber,
                Name = e.Name,
                CategoryName = e.Category!.Name,
                PurchaseYear = e.PurchaseYear,
                DailyRentalCost = e.DailyRentalCost,
                UnitCount = e.Units.Count,
                AvailableCount = e.Units.Count(u => u.Status == UnitStatus.Available)
            })
            .ToListAsync();

        return View(new EquipmentIndexViewModel
        {
            Search = search,
            CategoryId = categoryId,
            Items = items,
            Categories = await CategoryOptionsAsync()
        });
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var equipment = await LoadEquipmentWithUnitsAsync(id.Value);
        if (equipment == null) return NotFound();

        return View(new EquipmentDetailsViewModel
        {
            Equipment = equipment,
            AddUnits = new AddUnitsViewModel { EquipmentId = equipment.Id },
            Locations = await LocationOptionsAsync()
        });
    }

    // create / edit / delete

    public async Task<IActionResult> Create()
    {
        var model = new EquipmentFormViewModel { PurchaseYear = DateTime.Today.Year };
        await FillOptionsAsync(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EquipmentFormViewModel model)
    {
        await ValidateEquipmentAsync(model);
        if (model.InitialUnitCount > 0 && model.InitialLocationId == null)
            ModelState.AddModelError(nameof(model.InitialLocationId), "Choose where the purchased units are delivered.");
        else if (model.InitialLocationId != null &&
                 !await _db.RentalLocations.AnyAsync(l => l.Id == model.InitialLocationId))
            ModelState.AddModelError(nameof(model.InitialLocationId), "Unknown rental point.");

        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model);
            return View(model);
        }

        var equipment = new Equipment();
        CopyToEntity(model, equipment);
        _db.Equipment.Add(equipment);

        if (model.InitialUnitCount > 0)
        {
            var serials = (await _db.EquipmentUnits.Select(u => u.SerialNumber).ToListAsync()).ToHashSet();
            AddUnitsTo(equipment, model.InitialUnitCount, model.InitialLocationId!.Value, serials);
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"\"{equipment.Name}\" has been added with {model.InitialUnitCount} unit(s).";
        return RedirectToAction(nameof(Details), new { id = equipment.Id });
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var e = await _db.Equipment.FindAsync(id);
        if (e == null) return NotFound();

        var model = new EquipmentFormViewModel
        {
            Id = e.Id,
            InventoryNumber = e.InventoryNumber,
            Name = e.Name,
            Description = e.Description,
            CategoryId = e.CategoryId,
            PurchaseYear = e.PurchaseYear,
            PurchasePrice = e.PurchasePrice,
            DailyRentalCost = e.DailyRentalCost
        };
        await FillOptionsAsync(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EquipmentFormViewModel model)
    {
        if (id != model.Id) return NotFound();
        var equipment = await _db.Equipment.FindAsync(id);
        if (equipment == null) return NotFound();

        // units live on the details page, ignore those fields here
        ModelState.Remove(nameof(model.InitialUnitCount));
        ModelState.Remove(nameof(model.InitialLocationId));

        await ValidateEquipmentAsync(model);
        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model);
            return View(model);
        }

        CopyToEntity(model, equipment);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"\"{equipment.Name}\" has been updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var equipment = await LoadEquipmentWithUnitsAsync(id.Value);
        if (equipment == null) return NotFound();
        ViewBag.CanDelete = !await HasRentalHistoryAsync(equipment.Id);
        return View(equipment);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var equipment = await _db.Equipment.FindAsync(id);
        if (equipment == null) return NotFound();

        if (await HasRentalHistoryAsync(id))
        {
            TempData["Error"] = "This equipment has rental history and cannot be deleted. Take its units out of service instead.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        _db.Equipment.Remove(equipment); // units go too (cascade)
        await _db.SaveChangesAsync();
        TempData["Success"] = $"\"{equipment.Name}\" and all its units have been deleted.";
        return RedirectToAction(nameof(Index));
    }

    // single units

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUnits([Bind(Prefix = "AddUnits")] AddUnitsViewModel model)
    {
        var equipment = await _db.Equipment.Include(e => e.Units).FirstOrDefaultAsync(e => e.Id == model.EquipmentId);
        if (equipment == null) return NotFound();

        if (model.LocationId != null && !await _db.RentalLocations.AnyAsync(l => l.Id == model.LocationId))
            ModelState.AddModelError("AddUnits.LocationId", "Unknown rental point.");

        if (!ModelState.IsValid)
        {
            var reloaded = await LoadEquipmentWithUnitsAsync(equipment.Id);
            return View(nameof(Details), new EquipmentDetailsViewModel
            {
                Equipment = reloaded!,
                AddUnits = model,
                Locations = await LocationOptionsAsync()
            });
        }

        var serials = (await _db.EquipmentUnits.Select(u => u.SerialNumber).ToListAsync()).ToHashSet();
        AddUnitsTo(equipment, model.Count, model.LocationId!.Value, serials);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{model.Count} unit(s) added.";
        return RedirectToAction(nameof(Details), new { id = equipment.Id });
    }

    public async Task<IActionResult> EditUnit(int? id)
    {
        if (id == null) return NotFound();
        var unit = await _db.EquipmentUnits.Include(u => u.Equipment).FirstOrDefaultAsync(u => u.Id == id);
        if (unit == null) return NotFound();

        // rented stuff is locked until it comes back
        if (unit.Status == UnitStatus.Rented)
        {
            TempData["Error"] = $"Unit {unit.SerialNumber} is currently rented and cannot be changed.";
            return RedirectToAction(nameof(Details), new { id = unit.EquipmentId });
        }

        return View(new EditUnitViewModel
        {
            Id = unit.Id,
            EquipmentId = unit.EquipmentId,
            EquipmentName = unit.Equipment!.Name,
            SerialNumber = unit.SerialNumber,
            LocationId = unit.LocationId,
            Status = unit.Status,
            Locations = await LocationOptionsAsync()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUnit(int id, EditUnitViewModel model)
    {
        if (id != model.Id) return NotFound();
        var unit = await _db.EquipmentUnits.Include(u => u.Equipment).FirstOrDefaultAsync(u => u.Id == id);
        if (unit == null) return NotFound();

        if (unit.Status == UnitStatus.Rented)
        {
            TempData["Error"] = $"Unit {unit.SerialNumber} is currently rented and cannot be changed.";
            return RedirectToAction(nameof(Details), new { id = unit.EquipmentId });
        }

        model.SerialNumber = model.SerialNumber?.Trim() ?? string.Empty;
        if (model.Status == UnitStatus.Rented)
            ModelState.AddModelError(nameof(model.Status), "A unit becomes rented only through a rental.");
        if (model.LocationId != null && !await _db.RentalLocations.AnyAsync(l => l.Id == model.LocationId))
            ModelState.AddModelError(nameof(model.LocationId), "Unknown rental point.");
        if (await _db.EquipmentUnits.AnyAsync(u => u.SerialNumber == model.SerialNumber && u.Id != id))
            ModelState.AddModelError(nameof(model.SerialNumber), "Another unit already has this serial number.");

        if (!ModelState.IsValid)
        {
            model.EquipmentId = unit.EquipmentId;
            model.EquipmentName = unit.Equipment!.Name;
            model.Locations = await LocationOptionsAsync();
            return View(model);
        }

        unit.SerialNumber = model.SerialNumber;
        unit.LocationId = model.LocationId!.Value; // this is how we move gear between points
        unit.Status = model.Status;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Unit {unit.SerialNumber} has been updated.";
        return RedirectToAction(nameof(Details), new { id = unit.EquipmentId });
    }

    public async Task<IActionResult> DeleteUnit(int? id)
    {
        if (id == null) return NotFound();
        var unit = await _db.EquipmentUnits
            .Include(u => u.Equipment).Include(u => u.Location)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (unit == null) return NotFound();
        ViewBag.CanDelete = unit.Status != UnitStatus.Rented &&
                            !await _db.Rentals.AnyAsync(r => r.EquipmentUnitId == unit.Id);
        return View(unit);
    }

    [HttpPost, ActionName("DeleteUnit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUnitConfirmed(int id)
    {
        var unit = await _db.EquipmentUnits.FindAsync(id);
        if (unit == null) return NotFound();

        if (unit.Status == UnitStatus.Rented || await _db.Rentals.AnyAsync(r => r.EquipmentUnitId == id))
        {
            TempData["Error"] = "This unit is rented or has rental history; mark it out of service instead.";
            return RedirectToAction(nameof(Details), new { id = unit.EquipmentId });
        }

        _db.EquipmentUnits.Remove(unit);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Unit {unit.SerialNumber} has been deleted.";
        return RedirectToAction(nameof(Details), new { id = unit.EquipmentId });
    }

    // helpers

    private Task<Equipment?> LoadEquipmentWithUnitsAsync(int id) =>
        _db.Equipment
            .Include(e => e.Category)
            .Include(e => e.Units).ThenInclude(u => u.Location)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

    private Task<bool> HasRentalHistoryAsync(int equipmentId) =>
        _db.Rentals.AnyAsync(r => r.EquipmentUnit!.EquipmentId == equipmentId);

    private async Task ValidateEquipmentAsync(EquipmentFormViewModel model)
    {
        model.InventoryNumber = model.InventoryNumber?.Trim() ?? string.Empty;
        model.Name = model.Name?.Trim() ?? string.Empty;

        if (!string.IsNullOrEmpty(model.InventoryNumber) &&
            await _db.Equipment.AnyAsync(e => e.InventoryNumber == model.InventoryNumber && e.Id != model.Id))
            ModelState.AddModelError(nameof(model.InventoryNumber), "This inventory number is already in use.");

        if (model.CategoryId != null && !await _db.Categories.AnyAsync(c => c.Id == model.CategoryId))
            ModelState.AddModelError(nameof(model.CategoryId), "Unknown category.");

        if (model.PurchaseYear > DateTime.Today.Year)
            ModelState.AddModelError(nameof(model.PurchaseYear), "The year of purchase cannot be in the future.");
    }

    private static void CopyToEntity(EquipmentFormViewModel model, Equipment e)
    {
        e.InventoryNumber = model.InventoryNumber;
        e.Name = model.Name;
        e.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        e.CategoryId = model.CategoryId!.Value;
        e.PurchaseYear = model.PurchaseYear!.Value;
        e.PurchasePrice = model.PurchasePrice!.Value;
        e.DailyRentalCost = model.DailyRentalCost!.Value;
    }

    // serials look like inv/001, inv/002... skipping any that are taken
    private static void AddUnitsTo(Equipment equipment, int count, int locationId, HashSet<string> existingSerials)
    {
        var next = equipment.Units.Count + 1;
        for (var i = 0; i < count; i++)
        {
            string serial;
            do
            {
                serial = $"{equipment.InventoryNumber}/{next++:D3}";
            } while (!existingSerials.Add(serial));

            equipment.Units.Add(new EquipmentUnit
            {
                SerialNumber = serial,
                LocationId = locationId,
                Status = UnitStatus.Available
            });
        }
    }

    private async Task FillOptionsAsync(EquipmentFormViewModel model)
    {
        model.Categories = await CategoryOptionsAsync();
        model.Locations = await LocationOptionsAsync();
    }

    private async Task<IEnumerable<SelectListItem>> CategoryOptionsAsync() =>
        await _db.Categories.OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();

    private async Task<IEnumerable<SelectListItem>> LocationOptionsAsync() =>
        await _db.RentalLocations.OrderBy(l => l.City).ThenBy(l => l.Name)
            .Select(l => new SelectListItem(l.City + " – " + l.Name, l.Id.ToString()))
            .ToListAsync();
}
