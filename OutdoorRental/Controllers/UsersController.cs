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

    public UsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
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
}
