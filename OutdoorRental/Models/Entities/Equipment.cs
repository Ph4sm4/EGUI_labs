namespace OutdoorRental.Models.Entities;

// one entry in the equipment list = one batch, the real pieces are the units
public class Equipment
{
    public int Id { get; set; }

    public string InventoryNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int PurchaseYear { get; set; }

    public decimal PurchasePrice { get; set; }

    public decimal DailyRentalCost { get; set; }

    public ICollection<EquipmentUnit> Units { get; set; } = new List<EquipmentUnit>();
}
