namespace SupportCrm.Domain.Entities;

public class Department
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public TicketChannel? DefaultForChannel { get; private set; }

    private Department() { }

    public Department(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Department name is required.", nameof(name));
        Id = Guid.NewGuid();
        Name = name.Trim();
    }

    public void SetDefaultChannel(TicketChannel? channel) => DefaultForChannel = channel;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
