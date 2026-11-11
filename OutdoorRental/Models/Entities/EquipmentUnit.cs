using System.ComponentModel.DataAnnotations;

namespace OutdoorRental.Models.Entities;

public enum UnitStatus
{
    Available = 0,
    Rented = 1,
    [Display(Name = "Out of service")]
    OutOfService = 2
}

// one actual piece of gear from a batch
public class EquipmentUnit
{
    public int Id { get; set; }

    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    // e.g. tnt-2p-01/003
    public string SerialNumber { get; set; } = string.Empty;

    // where it sits (or where it's coming back to while rented)
    public int LocationId { get; set; }
    public RentalLocation? Location { get; set; }

    public UnitStatus Status { get; set; } = UnitStatus.Available;

    public ICollection<Rental> Rentals { get; set; } = new List<Rental>();
}
