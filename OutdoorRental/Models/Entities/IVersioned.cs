namespace OutdoorRental.Models.Entities;

// optimistic concurrency: the db context gives it a new value on every save,
// so an UPDATE that still carries the old one hits 0 rows and EF throws DbUpdateConcurrencyException
public interface IVersioned
{
    Guid Version { get; set; }
}
