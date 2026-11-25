using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OutdoorRental.Data;
using OutdoorRental.Models.Entities;
using OutdoorRental.Models.ViewModels;

namespace OutdoorRental.Controllers;

// admin's list of everyone, can edit anyone's details
[Authorize(Roles = DbSeeder.AdminRole)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public UsersController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var adminIds = (await _userManager.GetUsersInRoleAsync(DbSeeder.AdminRole))
            .Select(u => u.Id).ToHashSet();

        var users = await _userManager.Users
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();

        return View(users.Select(u => new UserListItemViewModel
        {
            Id = u.Id,
            Email = u.Email ?? string.Empty,
            FullName = u.FullName,
            PhoneNumber = u.PhoneNumber,
            Balance = u.Balance,
            IsAdmin = adminIds.Contains(u.Id)
        }).ToList());
    }

    public async Task<IActionResult> Edit(string? id)
    {
        if (id == null) return NotFound();
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        return View(new ProfileViewModel
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber ?? string.Empty
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ProfileViewModel model)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        model.Id = user.Id;
        model.Email = user.Email;
        if (!ModelState.IsValid)
            return View(model);

        user.FirstName = model.FirstName.Trim();
        user.LastName = model.LastName.Trim();
        user.PhoneNumber = model.PhoneNumber.Trim();
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        TempData["Success"] = $"Details of {user.FullName} have been saved.";
        return RedirectToAction(nameof(Index));
    }

    // cash desk - admin takes the money and tops up the balance

    public async Task<IActionResult> Deposit(string? id)
    {
        if (id == null) return NotFound();
        var model = new DepositViewModel { UserId = id };
        return await FillDepositAsync(model) ? View(model) : NotFound();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deposit(DepositViewModel model)
    {
        var user = await _db.Users.FindAsync(model.UserId);
        if (user == null) return NotFound();

        if (!ModelState.IsValid)
        {
            await FillDepositAsync(model);
            return View(model);
        }

        var amount = Math.Round(model.Amount!.Value, 2);
        user.Balance += amount;
        _db.AccountTransactions.Add(new AccountTransaction
        {
            UserId = user.Id,
            Type = TransactionType.Deposit,
            Amount = amount,
            CreatedAt = DateTime.UtcNow,
            Description = $"Cash deposit by {User.Identity!.Name}"
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // another admin deposit or a rental return touched this balance at the same moment
            TempData["Error"] = "The balance was changed by someone else at the same time. Nothing was deposited - please try again.";
            return RedirectToAction(nameof(Deposit), new { id = user.Id });
        }

        TempData["Success"] = $"Deposited {amount:N2} PLN for {user.FullName}. New balance: {user.Balance:N2} PLN.";
        return RedirectToAction(nameof(Deposit), new { id = user.Id });
    }

    private async Task<bool> FillDepositAsync(DepositViewModel model)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == model.UserId);
        if (user == null) return false;
        model.FullName = user.FullName;
        model.Email = user.Email;
        model.Balance = user.Balance;
        model.Transactions = await _db.AccountTransactions.AsNoTracking()
            .Where(t => t.UserId == user.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .ToListAsync();
        return true;
    }
}
