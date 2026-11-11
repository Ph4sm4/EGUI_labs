using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OutdoorRental.Data;
using OutdoorRental.Models.Entities;
using OutdoorRental.Models.ViewModels;

namespace OutdoorRental.Controllers;

// categories list, admin only
[Authorize(Roles = DbSeeder.AdminRole)]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _db;

    public CategoriesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        await LoadListAsync();
        return View(new Category());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name")] Category category)
    {
        await ValidateUniqueAsync(category);
        if (!ModelState.IsValid)
        {
            await LoadListAsync();
            ViewBag.OpenCreateModal = true; // reopen the modal so they see what went wrong
            return View(nameof(Index), category);
        }

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Category \"{category.Name}\" has been added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var category = await _db.Categories.FindAsync(id);
        return category == null ? NotFound() : View(category);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Category input)
    {
        if (id != input.Id) return NotFound();
        var category = await _db.Categories.FindAsync(id);
        if (category == null) return NotFound();

        await ValidateUniqueAsync(input);
        if (!ModelState.IsValid)
            return View(input);

        category.Name = input.Name;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Category has been renamed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null) return NotFound();

        // still used by some equipment? then no
        if (await _db.Equipment.AnyAsync(e => e.CategoryId == id))
        {
            TempData["Error"] = $"Category \"{category.Name}\" is used by equipment and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Category \"{category.Name}\" has been deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadListAsync()
    {
        ViewBag.Categories = await _db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryListItemViewModel(c.Id, c.Name, c.Equipment.Count))
            .ToListAsync();
    }

    private async Task ValidateUniqueAsync(Category category)
    {
        var name = category.Name?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        category.Name = name;
        if (await _db.Categories.AnyAsync(c => c.Name == name && c.Id != category.Id))
            ModelState.AddModelError(nameof(Category.Name), "This category already exists.");
    }
}
