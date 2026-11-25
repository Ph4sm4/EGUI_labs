using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OutdoorRental.Data;
using OutdoorRental.Models.Entities;
using OutdoorRental.Models.ViewModels;

namespace OutdoorRental.Controllers;

// renting and returning. users see their own, admin sees (and can return) everything.
// money: you need at least one day's rate on the balance to start, the real cost
// (started days x locked daily rate) is taken from the balance on return
[Authorize]
public class RentalsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public RentalsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private bool IsAdmin => User.IsInRole(DbSeeder.AdminRole);

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        var rentals = _db.Rentals
            .Include(r => r.User)
            .Include(r => r.EquipmentUnit).ThenInclude(u => u!.Equipment)
            .Include(r => r.PickupLocation)
            .Include(r => r.ReturnLocation)
            .AsNoTracking();
        if (!IsAdmin)
            rentals = rentals.Where(r => r.UserId == userId);

        var model = new RentalsIndexViewModel
        {
            IsAdminView = IsAdmin,
            // active ones first, then newest
            Rentals = await rentals
                .OrderBy(r => r.ReturnedAt != null).ThenByDescending(r => r.RentedAt)
                .ToListAsync()
        };

        if (!IsAdmin)
        {
            model.Balance = (await _db.Users.FindAsync(userId))!.Balance;
            model.Transactions = await _db.AccountTransactions.AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        return View(model);
    }

    // renting

    public async Task<IActionResult> Rent(int? id)
    {
        if (id == null) return NotFound();
        var model = new RentViewModel { EquipmentId = id.Value };
        if (!await FillRentAsync(model)) return NotFound();

        if (!model.Locations.Any())
        {
            TempData["Error"] = "No units of this equipment are available right now.";
            return RedirectToAction("Details", "Equipment", new { id });
        }
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rent(RentViewModel model)
    {
        var equipment = await _db.Equipment.FindAsync(model.EquipmentId);
        if (equipment == null) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (user.Balance < equipment.DailyRentalCost)
            ModelState.AddModelError(string.Empty,
                $"Your balance ({user.Balance:N2} PLN) does not cover one day of rental. Deposit money at a cash desk first.");

        EquipmentUnit? unit = null;
        if (ModelState.IsValid)
        {
            unit = await _db.EquipmentUnits.FirstOrDefaultAsync(u =>
                u.EquipmentId == equipment.Id && u.LocationId == model.LocationId && u.Status == UnitStatus.Available);
            if (unit == null)
                ModelState.AddModelError(nameof(model.LocationId), "There are no free units at this rental point any more.");
        }

        if (!ModelState.IsValid)
        {
            await FillRentAsync(model);
            return View(model);
        }

        unit!.Status = UnitStatus.Rented;
        _db.Rentals.Add(new Rental
        {
            UserId = user.Id,
            EquipmentUnitId = unit.Id,
            PickupLocationId = unit.LocationId,
            RentedAt = DateTime.UtcNow,
            DailyRate = equipment.DailyRentalCost
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // someone else rented / moved / disabled this exact unit between our read and write
            TempData["Error"] = "Sorry, that unit was just taken or changed by someone else. Please try again.";
            return RedirectToAction(nameof(Rent), new { id = equipment.Id });
        }

        TempData["Success"] = $"You have rented \"{equipment.Name}\" (unit {unit.SerialNumber}).";
        return RedirectToAction(nameof(Index));
    }

    // returning

    public async Task<IActionResult> Return(int? id)
    {
        if (id == null) return NotFound();
        var rental = await LoadActiveRentalAsync(id.Value);
        if (rental == null) return NotFound();

        var model = new ReturnViewModel { RentalId = rental.Id, ReturnLocationId = rental.PickupLocationId };
        await FillReturnAsync(model, rental);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(ReturnViewModel model)
    {
        var rental = await LoadActiveRentalAsync(model.RentalId);
        if (rental == null)
        {
            TempData["Error"] = "This rental has already been returned.";
            return RedirectToAction(nameof(Index));
        }

        if (model.ReturnLocationId != null && !await _db.RentalLocations.AnyAsync(l => l.Id == model.ReturnLocationId))
            ModelState.AddModelError(nameof(model.ReturnLocationId), "Unknown rental point.");

        if (!ModelState.IsValid)
        {
            await FillReturnAsync(model, rental);
            return View(model);
        }

        var (days, cost) = Cost(rental);
        var now = DateTime.UtcNow;
        var unit = rental.EquipmentUnit!;
        var user = rental.User!;

        rental.ReturnedAt = now;
        rental.ReturnLocationId = model.ReturnLocationId;
        rental.TotalCost = cost;
        unit.Status = UnitStatus.Available;
        unit.LocationId = model.ReturnLocationId!.Value; // the unit now sits wherever it was dropped off
        user.Balance -= cost;
        _db.AccountTransactions.Add(new AccountTransaction
        {
            UserId = user.Id,
            Type = TransactionType.RentalCharge,
            Amount = -cost,
            CreatedAt = now,
            Description = $"{unit.Equipment!.Name} ({unit.SerialNumber}), {days} day(s)",
            Rental = rental
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // e.g. user and admin both pressed return, or a deposit landed at the same moment
            TempData["Error"] = "This rental or the account was changed by someone else at the same time. Please check and try again.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = $"Returned \"{unit.Equipment.Name}\". {cost:N2} PLN for {days} day(s) was charged.";
        return RedirectToAction(nameof(Index));
    }

    // helpers

    // started days count, at least one
    private static (int days, decimal cost) Cost(Rental r)
    {
        var days = Math.Max(1, (int)Math.Ceiling((DateTime.UtcNow - r.RentedAt).TotalDays));
        return (days, days * r.DailyRate);
    }

    // tracked, only if still out and (unless admin) your own
    private async Task<Rental?> LoadActiveRentalAsync(int id)
    {
        var rental = await _db.Rentals
            .Include(r => r.User)
            .Include(r => r.EquipmentUnit).ThenInclude(u => u!.Equipment)
            .Include(r => r.PickupLocation)
            .FirstOrDefaultAsync(r => r.Id == id && r.ReturnedAt == null);
        if (rental == null) return null;
        return IsAdmin || rental.UserId == _userManager.GetUserId(User) ? rental : null;
    }

    private async Task<bool> FillRentAsync(RentViewModel model)
    {
        var equipment = await _db.Equipment.AsNoTracking().FirstOrDefaultAsync(e => e.Id == model.EquipmentId);
        if (equipment == null) return false;

        model.EquipmentName = equipment.Name;
        model.DailyRate = equipment.DailyRentalCost;
        model.Balance = (await _userManager.GetUserAsync(User))?.Balance ?? 0;
        // only points that have a free unit of this equipment
        model.Locations = await _db.EquipmentUnits
            .Where(u => u.EquipmentId == equipment.Id && u.Status == UnitStatus.Available)
            .GroupBy(u => new { u.LocationId, u.Location!.City, u.Location.Name })
            .OrderBy(g => g.Key.City).ThenBy(g => g.Key.Name)
            .Select(g => new SelectListItem($"{g.Key.City} – {g.Key.Name} ({g.Count()} free)", g.Key.LocationId.ToString()))
            .ToListAsync();
        return true;
    }

    private async Task FillReturnAsync(ReturnViewModel model, Rental rental)
    {
        model.Rental = rental;
        (model.Days, model.Cost) = Cost(rental);
        model.Locations = await _db.RentalLocations.OrderBy(l => l.City).ThenBy(l => l.Name)
            .Select(l => new SelectListItem(l.City + " – " + l.Name, l.Id.ToString()))
            .ToListAsync();
    }
}
