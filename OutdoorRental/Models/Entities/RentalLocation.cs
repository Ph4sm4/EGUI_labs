using System.ComponentModel.DataAnnotations;

namespace OutdoorRental.Models.Entities;

// one rental point
public class RentalLocation : IVersioned
{
    public int Id { get; set; }

    public Guid Version { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Address { get; set; } = string.Empty;

    [StringLength(20), Phone]
    public string? Phone { get; set; }

    public ICollection<EquipmentUnit> Units { get; set; } = new List<EquipmentUnit>();
}
