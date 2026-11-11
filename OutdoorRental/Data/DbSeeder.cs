using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OutdoorRental.Models.Entities;

namespace OutdoorRental.Data;

public static class DbSeeder
{
    public const string AdminRole = "Admin";

    // so we get admin1@user.com, user1@user.com and so on
    private const string SeedDomain = "user.com";

    private record SeedPerson(string FirstName, string LastName, string Phone);

    private static readonly SeedPerson[] Admins =
    {
        new("Anna", "Nowak", "+48 501 000 001"),
    };

    private static readonly SeedPerson[] Users =
    {
        new("Jan", "Kowalski", "+48 600 100 201"),
        new("Maria", "Wiśniewska", "+48 600 100 202"),
        new("Piotr", "Wójcik", "+48 600 100 203"),
        new("Katarzyna", "Kamińska", "+48 600 100 204"),
        new("Tomasz", "Lewandowski", "+48 600 100 205"),
    };

    // runs on every start, only adds what's missing so it's safe to rerun
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILogger<ApplicationDbContext>>();

        await db.Database.MigrateAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(AdminRole))
            await roleManager.CreateAsync(new IdentityRole(AdminRole));

        var password = config["SeedUsers:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("SeedUsers:Password is not configured - no seed accounts created.");
            return;
        }

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        // everyone shares the same password from appsettings, it's just dev data
        for (var i = 0; i < Admins.Length; i++)
            await EnsureUserAsync(userManager, logger, $"admin{i + 1}@{SeedDomain}", Admins[i], password, isAdmin: true);

        for (var i = 0; i < Users.Length; i++)
            await EnsureUserAsync(userManager, logger, $"user{i + 1}@{SeedDomain}", Users[i], password, isAdmin: false);
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, ILogger logger,
        string email, SeedPerson person, string password, bool isAdmin)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = person.FirstName,
                LastName = person.LastName,
                PhoneNumber = person.Phone
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogError("Could not create seed account {Email}: {Errors}", email,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }
            logger.LogInformation("Created seed account {Email}", email);
        }

        if (isAdmin && !await userManager.IsInRoleAsync(user, AdminRole))
            await userManager.AddToRoleAsync(user, AdminRole);
    }
}
