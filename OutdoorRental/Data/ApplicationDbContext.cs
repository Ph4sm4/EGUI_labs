using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OutdoorRental.Models.Entities;

namespace OutdoorRental.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<RentalLocation> RentalLocations => Set<RentalLocation>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<EquipmentUnit> EquipmentUnits => Set<EquipmentUnit>();
    public DbSet<Rental> Rentals => Set<Rental>();
    public DbSet<AccountTransaction> AccountTransactions => Set<AccountTransaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.FirstName).HasMaxLength(50).IsRequired();
            e.Property(u => u.LastName).HasMaxLength(50).IsRequired();
            e.Property(u => u.Balance).HasPrecision(12, 2);
            e.Ignore(u => u.FullName);
        });

        builder.Entity<RentalLocation>(e =>
        {
            e.HasIndex(l => l.Name).IsUnique();
        });

        builder.Entity<Category>(e =>
        {
            e.HasIndex(c => c.Name).IsUnique();
            // a few starter categories baked into the first migration
            e.HasData(
                new Category { Id = 1, Name = "Tents" },
                new Category { Id = 2, Name = "Sleeping bags" },
                new Category { Id = 3, Name = "Backpacks" },
                new Category { Id = 4, Name = "Climbing gear" },
                new Category { Id = 5, Name = "Bikes" },
                new Category { Id = 6, Name = "Kayaks & canoes" },
                new Category { Id = 7, Name = "Winter gear" });
        });

        builder.Entity<Equipment>(e =>
        {
            e.Property(x => x.InventoryNumber).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.InventoryNumber).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.PurchasePrice).HasPrecision(12, 2);
            e.Property(x => x.DailyRentalCost).HasPrecision(10, 2);
            e.HasOne(x => x.Category).WithMany(c => c.Equipment)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EquipmentUnit>(e =>
        {
            e.Property(u => u.SerialNumber).HasMaxLength(40).IsRequired();
            e.HasIndex(u => u.SerialNumber).IsUnique();
            // delete the batch and its units go with it
            e.HasOne(u => u.Equipment).WithMany(x => x.Units)
                .HasForeignKey(u => u.EquipmentId).OnDelete(DeleteBehavior.Cascade);
            // can't delete a rental point that still has stuff in it
            e.HasOne(u => u.Location).WithMany(l => l.Units)
                .HasForeignKey(u => u.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Rental>(e =>
        {
            e.Property(r => r.DailyRate).HasPrecision(10, 2);
            e.Property(r => r.TotalCost).HasPrecision(12, 2);
            e.HasOne(r => r.User).WithMany(u => u.Rentals)
                .HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.EquipmentUnit).WithMany(u => u.Rentals)
                .HasForeignKey(r => r.EquipmentUnitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.PickupLocation).WithMany()
                .HasForeignKey(r => r.PickupLocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.ReturnLocation).WithMany()
                .HasForeignKey(r => r.ReturnLocationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountTransaction>(e =>
        {
            e.Property(t => t.Amount).HasPrecision(12, 2);
            e.Property(t => t.Description).HasMaxLength(200);
            e.HasOne(t => t.User).WithMany(u => u.Transactions)
                .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Rental).WithMany()
                .HasForeignKey(t => t.RentalId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
