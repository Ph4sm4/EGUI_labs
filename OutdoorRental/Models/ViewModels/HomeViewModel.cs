namespace OutdoorRental.Models.ViewModels;

// numbers for the home page tiles
public class HomeViewModel
{
    public int EquipmentCount { get; set; }
    public int AvailableUnitCount { get; set; }
    public int LocationCount { get; set; }
    public int CityCount { get; set; }

    // admin only
    public int CategoryCount { get; set; }
    public int UserCount { get; set; }
}
