using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace OutdoorRental.Models.Entities;

// identity gives us email, password hash and phone, we just add names and the balance
public class ApplicationUser : IdentityUser
{
    [Required, StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    // cash paid in at the desk, rentals come out of this
    public decimal Balance { get; set; }

    public ICollection<Rental> Rentals { get; set; } = new List<Rental>();
    public ICollection<AccountTransaction> Transactions { get; set; } = new List<AccountTransaction>();

    public string FullName => $"{FirstName} {LastName}";
}
