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
`EquipmentUnits`, each kept at a rental point. `Rentals` and `AccountTransactions` are already in the schema
for the next task (cash-desk deposits, renting, returning to any rental point). They have no UI yet.
