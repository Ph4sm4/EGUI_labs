using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using OutdoorRental.Models.Entities;

namespace OutdoorRental.Models.ViewModels;

// create/edit form for an equipment entry
public class EquipmentFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(30), Display(Name = "Inventory number")]
    [RegularExpression(@"^[A-Za-z0-9\-/_.]+$", ErrorMessage = "Use letters, digits and - / _ . only.")]
    public string InventoryNumber { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000), DataType(DataType.MultilineText)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose a category."), Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Required, Range(1950, 2100), Display(Name = "Year of purchase")]
    public int? PurchaseYear { get; set; }

    [Required, Range(0.0, 10_000_000.0), Display(Name = "Purchase price (PLN)")]
    public decimal? PurchasePrice { get; set; }

    [Required, Range(0.01, 100_000.0), Display(Name = "Daily rental cost (PLN)")]
    public decimal? DailyRentalCost { get; set; }

    // only used on create - how many units we bought and where they go
    [Range(0, 200), Display(Name = "Number of units purchased")]
    public int InitialUnitCount { get; set; } = 1;

    [Display(Name = "Deliver units to")]
    public int? InitialLocationId { get; set; }

    public IEnumerable<SelectListItem> Categories { get; set; } = Enumerable.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Locations { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class EquipmentListItemViewModel
{
    public int Id { get; set; }
    public string InventoryNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int PurchaseYear { get; set; }
    public decimal DailyRentalCost { get; set; }
    public int UnitCount { get; set; }
    public int AvailableCount { get; set; }
}

public class EquipmentIndexViewModel
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public List<EquipmentListItemViewModel> Items { get; set; } = new();
    public IEnumerable<SelectListItem> Categories { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class EquipmentDetailsViewModel
{
    public Equipment Equipment { get; set; } = null!;
    public AddUnitsViewModel AddUnits { get; set; } = new();
    public IEnumerable<SelectListItem> Locations { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class AddUnitsViewModel
{
    public int EquipmentId { get; set; }

    [Range(1, 200), Display(Name = "Number of units")]
    public int Count { get; set; } = 1;

    [Required(ErrorMessage = "Choose a rental point."), Display(Name = "Rental point")]
    public int? LocationId { get; set; }
}

// edit one unit - move it somewhere else or mark it broken
public class EditUnitViewModel
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentName { get; set; } = string.Empty;

    [Required, StringLength(40), Display(Name = "Serial number")]
    public string SerialNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a rental point."), Display(Name = "Rental point")]
    public int? LocationId { get; set; }

    public UnitStatus Status { get; set; }

    public IEnumerable<SelectListItem> Locations { get; set; } = Enumerable.Empty<SelectListItem>();
}
