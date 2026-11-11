using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OutdoorRental.Models.Entities;
using OutdoorRental.Models.ViewModels;

namespace OutdoorRental.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    // login / register - no separate pages, both live in modals in the header

    // old-school urls just bounce to a page with the modal open
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) => OpenModal("login", returnUrl);

    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null) => OpenModal("register", returnUrl);

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([Bind(Prefix = "Login")] LoginViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
                return RedirectAfterSuccess(model.ReturnUrl);

            ModelState.AddModelError(string.Empty, "Invalid email or password.");
        }

        model.Password = string.Empty; // never send the password back
        TempData[AuthModalsViewModel.TempDataLogin] = JsonSerializer.Serialize(model);
        return ReturnWithModal("login", model.Origin);
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register([Bind(Prefix = "Register")] RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            // new folks are always regular customers, no sneaking in as admin
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                PhoneNumber = model.PhoneNumber.Trim()
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                TempData["Success"] = $"Welcome, {user.FirstName}! Your account has been created.";
                return RedirectAfterSuccess(model.ReturnUrl);
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
        }

        model.Password = model.ConfirmPassword = string.Empty;
        TempData[AuthModalsViewModel.TempDataRegister] = JsonSerializer.Serialize(model);
        return ReturnWithModal("register", model.Origin);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private IActionResult OpenModal(string modal, string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        return RedirectToAction("Index", "Home",
            new { auth = modal, returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
    }

    // only local urls, no getting bounced to some random site
    private IActionResult RedirectAfterSuccess(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction("Index", "Home");

    // stash the errors in tempdata and go back, the layout pops the modal open again
    private IActionResult ReturnWithModal(string modal, string? origin)
    {
        var errors = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .Where(m => !string.IsNullOrEmpty(m))
            .Distinct()
            .ToList();
        TempData[AuthModalsViewModel.TempDataModal] = modal;
        TempData[AuthModalsViewModel.TempDataErrors] = JsonSerializer.Serialize(errors);
        return Url.IsLocalUrl(origin) ? LocalRedirect(origin!) : RedirectToAction("Index", "Home");
    }

    // your own profile - always the logged-in user, never an id from the url

    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        ViewBag.Balance = user.Balance;
        return View(new ProfileViewModel
        {
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber ?? string.Empty
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!ModelState.IsValid)
        {
            model.Email = user.Email;
            ViewBag.Balance = user.Balance;
            return View(model);
        }

        user.FirstName = model.FirstName.Trim();
        user.LastName = model.LastName.Trim();
        user.PhoneNumber = model.PhoneNumber.Trim();
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            model.Email = user.Email;
            ViewBag.Balance = user.Balance;
            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Your details have been saved.";
        return RedirectToAction(nameof(Profile));
    }

    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Your password has been changed.";
        return RedirectToAction(nameof(Profile));
    }
}
