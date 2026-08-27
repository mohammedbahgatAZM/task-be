namespace SupportCrm.Domain.Entities;

public class Holiday
{
    public Guid Id { get; private set; }
    public DateOnly Date { get; private set; }
    public string Name { get; private set; } = default!;

    private Holiday() { } // EF Core

    public Holiday(DateOnly date, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Holiday name is required.", nameof(name));
        Id = Guid.NewGuid();
        Date = date;
        Name = name;
    }
}
