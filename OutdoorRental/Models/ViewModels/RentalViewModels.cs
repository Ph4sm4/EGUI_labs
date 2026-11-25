using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using OutdoorRental.Models.Entities;

namespace OutdoorRental.Models.ViewModels;

// start a rental: pick the rental point, we hand out the first free unit there
public class RentViewModel
{
    public int EquipmentId { get; set; }
    [ValidateNever]
    public string EquipmentName { get; set; } = string.Empty;
    public decimal DailyRate { get; set; }
    public decimal Balance { get; set; }

    [Required(ErrorMessage = "Choose a rental point."), Display(Name = "Pick up at")]
    public int? LocationId { get; set; }

    public IEnumerable<SelectListItem> Locations { get; set; } = Enumerable.Empty<SelectListItem>();
}

// finish a rental: by default back to where it was picked up
public class ReturnViewModel
{
    public int RentalId { get; set; }
    public Rental? Rental { get; set; }
    public int Days { get; set; }
    public decimal Cost { get; set; }

    [Required(ErrorMessage = "Choose a rental point."), Display(Name = "Return to")]
    public int? ReturnLocationId { get; set; }

    public IEnumerable<SelectListItem> Locations { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class RentalsIndexViewModel
{
    public bool IsAdminView { get; set; }
    public decimal Balance { get; set; }
    public List<Rental> Rentals { get; set; } = new();
    public List<AccountTransaction> Transactions { get; set; } = new();
}

// cash desk: admin takes money and tops up a user's balance
public class DepositViewModel
{
    public string UserId { get; set; } = string.Empty;
    [ValidateNever]
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public decimal Balance { get; set; }

    [Required, Range(0.01, 100_000.0), Display(Name = "Amount (PLN)")]
    public decimal? Amount { get; set; }

    public List<AccountTransaction> Transactions { get; set; } = new();
}
