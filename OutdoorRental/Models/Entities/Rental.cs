namespace OutdoorRental.Models.Entities;

// someone renting one unit, the return date stays empty while it's out
public class Rental
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int EquipmentUnitId { get; set; }
    public EquipmentUnit? EquipmentUnit { get; set; }

    public int PickupLocationId { get; set; }
    public RentalLocation? PickupLocation { get; set; }

    // same as pickup by default, but you can drop it off somewhere else
    public int? ReturnLocationId { get; set; }
    public RentalLocation? ReturnLocation { get; set; }

    public DateTime RentedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }

    // price locked in when the rental starts
    public decimal DailyRate { get; set; }

    public decimal? TotalCost { get; set; }
}
