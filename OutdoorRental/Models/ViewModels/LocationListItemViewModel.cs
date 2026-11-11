using OutdoorRental.Models.Entities;

namespace OutdoorRental.Models.ViewModels;

public class LocationListItemViewModel
{
    public RentalLocation Location { get; set; } = null!;
    public int UnitCount { get; set; }
    public int AvailableCount { get; set; }
}
