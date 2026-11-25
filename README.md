# Outdoorsy – outdoor equipment rental network (ASP.NET Core MVC)

Server-rendered ASP.NET Core 8 MVC app with Bootstrap 5, Entity Framework Core and SQLite.
The only client-side scripts are library ones: jQuery validation (unobtrusive) and the Bootstrap bundle.

## Running

```bash
dotnet run --project OutdoorRental --launch-profile http
```

Open http://localhost:5130. On startup the app applies EF migrations (creates `OutdoorRental/outdoorrental.db`)
and creates any missing seed accounts:

| Email | Role |
|---|---|
| `admin1@user.com` | Admin |
| `user1@user.com` … `user5@user.com` | Customer |

They all share the password in the `SeedUsers` section of `OutdoorRental/appsettings.json` (dev/test only).
The names and phone numbers are in `Data/DbSeeder.cs`; to add another account, add a row to `Admins` or `Users` there.

EF tooling is a local tool: `dotnet tool restore`, then e.g.
`dotnet ef migrations add <Name> --project OutdoorRental -o Data/Migrations`.

## Task 1 scope (implemented)

| Requirement | Where |
|---|---|
| Rental point creation (plus edit/delete) | `LocationsController`, `Views/Locations` |
| Equipment editing (create/edit/delete, units of a batch, moving units between points) | `EquipmentController`, `Views/Equipment` |
| Administrator login | `AccountController.Login` + `[Authorize(Roles = "Admin")]` |
| Registration and login of standard users | `AccountController.Register` / `Login` |
| Users edit their own first/last name and phone; admin edits anyone's | `AccountController.Profile`, `UsersController` |

## Task 2 scope (implemented)

| Requirement | Where |
|---|---|
| Admin as cashier: deposits money into a user's account | `UsersController.Deposit`, `Views/Users/Deposit` |
| Renting (pick a rental point, the first free unit is handed out) | `RentalsController.Rent` |
| Returning to any rental point (pickup point by default); the unit moves there | `RentalsController.Return` |
| Rent paid from the balance | on return: started days × daily rate locked at pickup; at least one day's rate is needed on the balance to start |
| My rentals + account history / all rentals for admin | `RentalsController.Index` |
| Admin moves equipment between rental points | `EquipmentController.EditUnit` |
| Conflict handling | optimistic concurrency, see below |

**Conflict handling.** `ApplicationDbContext.SaveChanges` gives every modified `IVersioned` row a new `Version`
and every modified user a new `ConcurrencyStamp` (Identity's built-in concurrency token). EF puts the old value in the
`WHERE` clause, so a write based on stale data fails with `DbUpdateConcurrencyException`, which is reported to the user:
- two admins editing the same rental point / equipment / unit: the edit forms carry the loaded `Version` in a hidden field;
- two customers renting the last free unit, or an admin moving/disabling a unit while it is being rented;
- a deposit and a rental charge hitting the same balance at once, or the user and an admin both returning the same rental.

## Database design

```
AspNetUsers (ApplicationUser)   Id, Email, PasswordHash, PhoneNumber, FirstName, LastName, Balance
RentalLocations                 Id, Name (unique), City, Address, Phone
Categories                      Id, Name (unique)                        – seeded with 7 categories
Equipment                       Id, InventoryNumber (unique), Name, Description, CategoryId → Categories,
                                PurchaseYear, PurchasePrice, DailyRentalCost
EquipmentUnits                  Id, EquipmentId → Equipment (cascade), SerialNumber (unique),
                                LocationId → RentalLocations (restrict), Status (Available/Rented/OutOfService)
Rentals                         Id, UserId, EquipmentUnitId, PickupLocationId, ReturnLocationId?,
                                RentedAt, ReturnedAt?, DailyRate, TotalCost?
AccountTransactions             Id, UserId, Type (Deposit/RentalCharge), Amount, CreatedAt, Description, RentalId?
```

An `Equipment` row is one entry in the equipment list (a purchased batch). Its physical pieces are
`EquipmentUnits`, each kept at a rental point. `RentalLocations`, `Equipment` and `EquipmentUnits` also have a
`Version` (GUID) concurrency column.
