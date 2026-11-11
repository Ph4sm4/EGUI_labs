namespace OutdoorRental.Models.Entities;

public enum TransactionType
{
    Deposit = 0,
    RentalCharge = 1
}

// money in or out of someone's account
public class AccountTransaction
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public TransactionType Type { get; set; }

    // plus for deposits, minus for charges
    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? Description { get; set; }

    public int? RentalId { get; set; }
    public Rental? Rental { get; set; }
}
